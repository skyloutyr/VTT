﻿namespace VTT.Render
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Text;
    using VTT.Control;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.GLFW;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Render.Shaders;
    using VTT.Util;

    public class Shadow2DRenderer
    {
        private readonly struct Shadow2DLightSource
        {
            public static readonly string uniformNameStart = "lights[";
            public static readonly string uniformNamePosition = "].position";
            public static readonly string uniformNameThreshold = "].threshold";
            public static readonly string uniformNameDimming = "].dimming";
            public static readonly StringBuilder sb = new StringBuilder();

            public readonly Vector2 position;
            public readonly float threshold;
            public readonly float dimming;

            public Shadow2DLightSource(Vector2 position, float threshold, float dimming)
            {
                this.position = position;
                this.threshold = threshold;
                this.dimming = dimming;
            }

            public readonly void Uniform(FastAccessShader<RTShadowUniforms> shader, int idx) => shader.Uniforms.Lights.Set(new Vector4(this.position, this.threshold, this.dimming), idx);
        }

        public GPUBuffer BoxesDataBuffer { get; set; }
        public GPUBuffer BVHNodesDataBuffer { get; set; }
        public Texture BoxesBufferTexture { get; set; }
        public Texture BVHNodesBufferTexture { get; set; }

        public Texture OutputTexture { get; set; }
        public Texture WhiteSquare { get; set; }
        public uint? FBO { get; set; }
        public uint? RBO { get; set; }
        public FastAccessShader<RTShadowUniforms> Raytracer { get; set; }
        public FastAccessShader<IndividualColorOverlay> BoxesOverlay { get; set; }

        public VertexArray OverlayVAO { get; set; }
        public GPUBuffer OverlayVBO { get; set; }

        public Shadow2DControlMode ControlMode { get; set; } = Shadow2DControlMode.Select;

        public Stopwatch CPUTimer { get; set; } = new Stopwatch();

        public int SimulationWidth { get; set; }
        public int SimulationHeight { get; set; }

        public float LineModeLineWidth
        {
            get => this._lineModeLineWidth;
            set => this._lineModeLineWidth = value;
        }

        public void Create()
        {
            int res = Client.Instance.Settings.Shadow2DPrecision switch
            { 
                ClientSettings.Shadow2DResolution.Low => 256,
                ClientSettings.Shadow2DResolution.Medium => 512,
                ClientSettings.Shadow2DResolution.High => 1024,
                _ => 512
            };

            this.ResizeSimulation(res, res);
            this.WhiteSquare = new Texture(TextureTarget.Texture2D);
            this.WhiteSquare.Bind();
            this.WhiteSquare.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            this.WhiteSquare.SetFilterParameters(FilterParam.Linear, FilterParam.Linear);
            unsafe
            {
                byte* texel = stackalloc byte[1];
                *texel = 255;
                GL.TexImage2D(TextureTarget.Texture2D, 0, SizedInternalFormat.Red8, 1, 1, PixelDataFormat.Red, PixelDataType.Byte, (nint)texel);
            }

            OpenGLUtil.NameObject(GLObjectType.Texture, this.WhiteSquare, "RT shadows white pixel texture");
            this.WhiteSquare.Size = new Size(1, 1);
            this.Raytracer = new FastAccessShader<RTShadowUniforms>(OpenGLUtil.LoadShader("shadowcast", ShaderType.Vertex, ShaderType.Fragment));
            this.Raytracer.Bind();
            this.Raytracer.Uniforms.FragmentPositionsSampler.Set(0);
            this.Raytracer.Uniforms.BoxesBufferSampler.Set(1);
            this.Raytracer.Uniforms.BVHBufferSampler.Set(2);

            this.BoxesOverlay = new FastAccessShader<IndividualColorOverlay>(OpenGLUtil.LoadShader("individual_color_overlay", ShaderType.Vertex, ShaderType.Fragment));

            this.OverlayVAO = new VertexArray();
            this.OverlayVBO = new GPUBuffer(BufferTarget.Array, BufferUsage.StreamDraw);
            this.OverlayVAO.Bind();
            this.OverlayVAO.SetVertexSize<float>(4);
            this.OverlayVBO.Bind();
            this.OverlayVAO.PushElement(ElementType.Vec4);
            this.OverlayVBO.SetData(IntPtr.Zero, sizeof(float) * 4 * 6 * 5);

            this._reusedBuffer = new UnsafeArray<Vector4>(30);
            this._lights = new UnsafeArray<Shadow2DLightSource>(64);

            OpenGLUtil.NameObject(GLObjectType.VertexArray, this.OverlayVAO, "RT shadows boxes overlay vao");
            OpenGLUtil.NameObject(GLObjectType.Buffer, this.OverlayVBO, "RT shadows boxes overlay vbo");
        }

        public void Resize(int w, int h)
        {
            if (Client.Instance.Settings.Shadow2DPrecision == ClientSettings.Shadow2DResolution.Full && w > 0 && h > 0)
            {
                this.ResizeSimulation(w, h);
            }
        }

        public void ResizeSimulation(int w, int h)
        {
            this.OutputTexture?.Dispose();
            if (this.FBO.HasValue)
            {
                GL.DeleteFramebuffer(this.FBO.Value);
            }

            this.OutputTexture = new Texture(TextureTarget.Texture2D);
            this.OutputTexture.Bind();
            this.OutputTexture.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            this.OutputTexture.SetFilterParameters(FilterParam.Linear, FilterParam.Linear);
            GL.TexImage2D(TextureTarget.Texture2D, 0, SizedInternalFormat.Red8, w, h, PixelDataFormat.Red, PixelDataType.Byte, 0);
            this.OutputTexture.Size = new Size(w, h);
            OpenGLUtil.NameObject(GLObjectType.Texture, this.OutputTexture, "RT shadows output texture");

            this.FBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.All, this.FBO.Value);
            OpenGLUtil.NameObject(GLObjectType.Framebuffer, this.FBO.Value, "RT shadows FBO");
            this.RBO = GL.GenRenderbuffer();
            GL.BindRenderbuffer(this.RBO.Value);
            OpenGLUtil.NameObject(GLObjectType.Renderbuffer, this.RBO.Value, "RT shadows RBO");
            GL.RenderbufferStorage(SizedInternalFormat.Depth24Stencil8, w, h);
            GL.FramebufferTexture(FramebufferTarget.All, FramebufferAttachment.Color0, this.OutputTexture, 0);
            GL.FramebufferRenderbuffer(FramebufferTarget.All, FramebufferAttachment.Depth, this.RBO.Value);
            GL.DrawBuffers(new DrawBufferMode[] { DrawBufferMode.Color0 });
            FramebufferStatus status = GL.CheckFramebufferStatus(FramebufferTarget.All);
            if (status != FramebufferStatus.Complete)
            {
                throw new Exception("2D Shadow Renderer FBO could not complete! Error - " + status);
            }

            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GL.BindRenderbuffer(0);

            this.SimulationWidth = w;
            this.SimulationHeight = h;
        }

        private Shadow2DBox _boxMouseOver;
        private Shadow2DBox _boxSelected;
        private bool _lmbDown;
        private Vector2 _initialClickWorldPos;
        private Vector2 _initialBoxStart;
        private Vector2 _initialBoxEnd;
        private Vector2 _cursorWorldLastUpdate;
        private Vector2? _lineModeFirstPoint;
        private float _lineModeLineWidth = 0.01f;
        private float _initialBoxRotation;
        private bool _isBoxDragging;

        public void Update(Map m)
        {
            Shadow2DBox bmover = null;
            EditMode em = Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode;
            Vector2 cursorWorld = this._cursorWorldLastUpdate = Client.Instance.Frontend.Renderer.MapRenderer.GetTerrainCursorOrPointAlongsideView().Xy();
            if (m != null && Client.Instance.IsAdmin && em == EditMode.Shadows2D && m.Is2D)
            {
                List<Shadow2DBox> boxesMouseOver = new List<Shadow2DBox>();
                bool foundSelectedBox = false;
                foreach (Shadow2DBox box in m.ShadowLayer2D.EnumerateBoxes())
                {
                    box.IsMouseOver = false;
                    if (box.Contains(cursorWorld))
                    {
                        boxesMouseOver.Add(box);
                    }

                    if (this._boxSelected != null)
                    {
                        if (Guid.Equals(box.BoxID, this._boxSelected.BoxID))
                        {
                            foundSelectedBox = true;
                        }
                    }
                }

                if (this._boxSelected != null && !foundSelectedBox)
                {
                    this._boxSelected = null;
                }

                float areaCurrent = float.MaxValue;
                foreach (Shadow2DBox b in boxesMouseOver)
                {
                    float area = b.Area;
                    if (area < areaCurrent)
                    {
                        bmover = b;
                        areaCurrent = area;
                    }
                }

                if (bmover != null)
                {
                    bmover.IsMouseOver = true;
                }

                bool imGuiWantsMouse = ImGui.GetIO().WantCaptureMouse;
                bool lmbState = Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Left);
                if (!imGuiWantsMouse && !this._lmbDown && lmbState)
                {
                    this._lmbDown = true;
                    this._isBoxDragging = false;
                    switch (this.ControlMode)
                    {
                        case Shadow2DControlMode.Translate:
                        case Shadow2DControlMode.Rotate:
                        case Shadow2DControlMode.Select:
                        {
                            if (this._boxMouseOver != null)
                            {
                                this._boxSelected = this._boxMouseOver ?? bmover;
                                if (this.ControlMode is Shadow2DControlMode.Rotate or Shadow2DControlMode.Translate)
                                {
                                    this._initialClickWorldPos = cursorWorld;
                                }

                                if (this.ControlMode == Shadow2DControlMode.Translate)
                                {
                                    this._initialBoxStart = this._boxSelected.Start;
                                    this._initialBoxEnd = this._boxSelected.End;
                                }

                                if (this.ControlMode == Shadow2DControlMode.Rotate)
                                {
                                    this._initialBoxRotation = this._boxSelected.Rotation;
                                }
                            }

                            break;
                        }

                        case Shadow2DControlMode.Toggle:
                        {
                            Shadow2DBox box = this._boxMouseOver ?? bmover;
                            if (box != null)
                            {
                                new PacketShadow2DBoxChangeProperty() { MapID = m.ID, BoxID = box.BoxID, ChangeType = PacketShadow2DBoxChangeProperty.PropertyType.IsActive, Property = !this._boxMouseOver.IsActive }.Send();
                            }

                            break;
                        }

                        case Shadow2DControlMode.AddBlocker:
                        case Shadow2DControlMode.AddSunlight:
                        {
                            this._initialClickWorldPos = cursorWorld;
                            this._isBoxDragging = true;
                            break;
                        }

                        case Shadow2DControlMode.AddBlockerPoints:
                        case Shadow2DControlMode.AddBlockerLine:
                        case Shadow2DControlMode.AddSunlightPoints:
                        {
                            // This is handled in the render method as this needs to check every frame for input events to not miss them
                            break;
                        }

                        case Shadow2DControlMode.Delete:
                        {
                            Shadow2DBox box = this._boxMouseOver ?? bmover;
                            if (box  != null)
                            {
                                new PacketDeleteShadow2DBox() { MapID = m.ID, BoxID = box.BoxID }.Send();
                            }

                            break;
                        }

                        default:
                        {
                            break;
                        }
                    }
                }

                if (!this._isBoxDragging)
                {
                    if (this._boxSelected != null)
                    {
                        if (!lmbState && this._lmbDown)
                        {
                            switch (this.ControlMode)
                            {
                                case Shadow2DControlMode.Translate:
                                {
                                    Vector2 d = cursorWorld - this._initialClickWorldPos;
                                    this._boxSelected.Start = this._initialBoxStart;
                                    this._boxSelected.End = this._initialBoxEnd;
                                    Vector2 v1 = this._boxSelected.Start + d;
                                    Vector2 v2 = this._boxSelected.End + d;
                                    new PacketShadow2DBoxChangeProperty() { MapID = m.ID, BoxID = this._boxSelected.BoxID, ChangeType = PacketShadow2DBoxChangeProperty.PropertyType.Position, Property = new Vector4(v1.X, v1.Y, v2.X, v2.Y) }.Send();
                                    this._boxSelected = null;
                                    this._initialBoxStart = this._initialBoxEnd = Vector2.Zero;
                                    break;
                                }

                                case Shadow2DControlMode.Rotate:
                                {
                                    Vector2 v0 = (this._boxSelected.Center - this._initialClickWorldPos).Normalized();
                                    Vector2 v1 = (this._boxSelected.Center - cursorWorld).Normalized();
                                    float angleRad = -MathF.Atan2(
                                        (v1.Y * v0.X) - (v1.X * v0.Y),
                                        (v1.X * v0.X) + (v1.Y * v0.Y)
                                    );

                                    new PacketShadow2DBoxChangeProperty() { MapID = m.ID, BoxID = this._boxSelected.BoxID, ChangeType = PacketShadow2DBoxChangeProperty.PropertyType.Rotation, Property = angleRad }.Send();
                                    this._boxSelected.Rotation = this._initialBoxRotation;
                                    this._boxSelected = null;
                                    this._initialBoxRotation = 0;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            switch (this.ControlMode)
                            {
                                case Shadow2DControlMode.Translate:
                                {
                                    Vector2 d = cursorWorld - this._initialClickWorldPos;
                                    this._boxSelected.Start = this._initialBoxStart + d;
                                    this._boxSelected.End = this._initialBoxEnd + d;
                                    break;
                                }

                                case Shadow2DControlMode.Rotate:
                                {
                                    Vector2 v0 = (this._boxSelected.Center - this._initialClickWorldPos).Normalized();
                                    Vector2 v1 = (this._boxSelected.Center - cursorWorld).Normalized();
                                    float angleRad = MathF.Atan2(
                                        (v1.Y * v0.X) - (v1.X * v0.Y),
                                        (v1.X * v0.X) + (v1.Y * v0.Y)
                                    );

                                    this._boxSelected.Rotation = -angleRad;
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (!lmbState && this._lmbDown)
                    {
                        Vector2 start = Vector2.Min(this._initialClickWorldPos, cursorWorld);
                        Vector2 end = Vector2.Max(this._initialClickWorldPos, cursorWorld);
                        if (end.X - start.X >= 0.01f && end.Y - start.Y >= 0.01f) // Reject boxes that are too tiny
                        {
                            Shadow2DBox.ShadowBoxType type = this.ControlMode == Shadow2DControlMode.AddBlocker ? Shadow2DBox.ShadowBoxType.Blocker : Shadow2DBox.ShadowBoxType.Sunlight;
                            Shadow2DBox box = new Shadow2DBox() { BoxID = Guid.NewGuid(), BoxType = type, Start = start, End = end, IsActive = true, Rotation = 0 };
                            new PacketAddShadow2DBox() { MapID = m.ID, Box = box }.Send();
                        }
                    }
                }
            
                if (!lmbState && this._lmbDown)
                {
                    this._lmbDown = false;
                    this._boxSelected = null;
                    this._initialClickWorldPos = Vector2.Zero;
                    this._initialBoxStart = Vector2.Zero;
                    this._initialBoxEnd = Vector2.Zero;
                    this._initialBoxRotation = 0;
                    this._isBoxDragging = false;
                }
            }

            this._boxMouseOver = bmover;
        }

        private int _numQuadDrawPoints;
        private readonly Vector2[] _shadowSelectionPointsArray = new Vector2[4];

        private void AddQuadDrawPoint(Map m, Vector2 cursorWorld, Shadow2DBox.ShadowBoxType? boxType = null)
        {
            this._shadowSelectionPointsArray[this._numQuadDrawPoints++] = cursorWorld;
            if (this._numQuadDrawPoints == 4)
            {
                Shadow2DBox.ShadowBoxType btype = boxType ?? (this.ControlMode == Shadow2DControlMode.AddBlockerPoints ? Shadow2DBox.ShadowBoxType.Blocker : Shadow2DBox.ShadowBoxType.Sunlight);

                // Step 1 - find convex hull
                List<Vector2> polygon = new List<Vector2>(this._shadowSelectionPointsArray);
                polygon.Sort(static (l, r) => l.X != r.X ? Comparer<float>.Default.Compare(l.X, r.X) : Comparer<float>.Default.Compare(l.Y, r.Y));

                List<Vector2> upper = new List<Vector2>();
                List<Vector2> lower = new List<Vector2>();
                foreach (Vector2 v in polygon)
                {
                    while (upper.Count > 1)
                    {
                        Vector2 q = upper[^1];
                        Vector2 r = upper[^2];
                        if ((q.X - r.X) * (v.Y - r.Y) >= (q.Y - r.Y) * (v.X - r.X))
                        {
                            upper.RemoveAt(upper.Count - 1);
                        }
                        else
                        {
                            break;
                        }
                    }

                    upper.Add(v);
                }

                upper.RemoveAt(upper.Count - 1);
                for (int i = polygon.Count - 1; i >= 0; --i)
                {
                    Vector2 v = polygon[i];
                    while (lower.Count > 1)
                    {
                        Vector2 q = lower[^1];
                        Vector2 r = lower[^2];
                        if ((q.X - r.X) * (v.Y - r.Y) >= (q.Y - r.Y) * (v.X - r.X))
                        {
                            lower.RemoveAt(lower.Count - 1);
                        }
                        else
                        {
                            break;
                        }
                    }

                    lower.Add(v);
                }

                lower.RemoveAt(lower.Count - 1);
                if (!(upper.Count == 1 && upper.SequenceEqual(lower)))
                {
                    upper.AddRange(lower);
                }

                polygon = upper;
                // Step 2 - use the rotating calipers algorithm to find the OBB
                Vector2 center = polygon.Aggregate(static (l, r) => l + r) / polygon.Count;
                Shadow2DBox[] potentialOOBs = new Shadow2DBox[polygon.Count];
                static void Rotate(List<Vector2> polygon, float angleRad, Vector2 pivot)
                {
                    for (int i = 0; i < polygon.Count; ++i)
                    {
                        Vector2 v = polygon[i];
                        float x = v.X;
                        float y = v.Y;
                        x -= pivot.X;
                        y -= pivot.Y;
                        float tx = x;
                        float sin = MathF.Sin(angleRad);
                        float cos = MathF.Cos(angleRad);
                        x = (x * cos) - (y * sin);
                        y = (tx * sin) + (y * cos);
                        x += pivot.X;
                        y += pivot.Y;
                        polygon[i] = new Vector2(x, y);
                    }
                }

                static Vector2 EdgeAt(List<Vector2> polygon, int index)
                {
                    int n = index == polygon.Count - 1 ? 0 : index + 1;
                    return polygon[n] - polygon[index];
                }

                static (Vector2, Vector2) GetAABB(List<Vector2> polygon)
                {
                    float minX = float.MaxValue;
                    float minY = float.MaxValue;
                    float maxX = float.MinValue;
                    float maxY = float.MinValue;
                    foreach (Vector2 v in polygon)
                    {
                        minX = MathF.Min(minX, v.X);
                        maxX = MathF.Max(maxX, v.X);
                        minY = MathF.Min(minY, v.Y);
                        maxY = MathF.Max(maxY, v.Y);
                    }

                    return (new Vector2(minX, minY), new Vector2(maxX, maxY));
                }

                for (int i = 0; i < polygon.Count; ++i)
                {
                    Vector2 edge = EdgeAt(polygon, i);
                    float angle = MathF.Acos(edge.Normalized().Y);
                    Rotate(polygon, angle, center);
                    (Vector2, Vector2) aabb = GetAABB(polygon);
                    potentialOOBs[i] = new Shadow2DBox()
                    {
                        BoxID = Guid.NewGuid(),
                        BoxType = btype,
                        IsActive = true,
                        Rotation = angle,
                        Start = aabb.Item1,
                        End = aabb.Item2
                    };

                    Rotate(polygon, -angle, center);
                }

                float minArea = float.MaxValue;
                Shadow2DBox box = potentialOOBs[0];
                foreach (Shadow2DBox b in potentialOOBs)
                {
                    float area = b.Area;
                    if (area < minArea)
                    {
                        minArea = area;
                        box = b;
                    }
                }

                // Finally have our box!
                new PacketAddShadow2DBox() { MapID = m.ID, Box = box }.Send();
                this._numQuadDrawPoints = 0;
            }
        }

        private readonly List<MapObject> _cursorCandidates = new List<MapObject>();
        private readonly List<MapObject> _nonAdminOwnedCursorCandidates = new List<MapObject>();
        private UnsafeArray<Shadow2DLightSource> _lights;

        private bool _renderLmbDown;
        private bool _renderEscDown;
        public void Render(Map m)
        {
            this.CPUTimer.Restart();
            OpenGLUtil.StartSection("Raytraced 2d shadows");
            this._cursorCandidates.Clear();
            this._nonAdminOwnedCursorCandidates.Clear();

            static Vector2 GetObjectPosition(MapObject mo, Map m)
            {
                return m.Shadows2DObjectVisionStrict
                    ? Client.Instance.Frontend.Renderer.SelectionManager.IsDraggingObjects && !Client.Instance.Frontend.Renderer.PingRenderer.IsUIVisible && Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode != EditMode.Scale 
                        ? mo.ClientDragMoveResetInitialPosition.Xy() 
                        : mo.FinalMovementPosition.Xy()
                    : mo.Position.Xy();
            }

            if (m != null)
            {
                if (Client.Instance.IsAdmin && Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode == EditMode.Shadows2D && m.Is2D)
                {
                    if (this.ControlMode is Shadow2DControlMode.AddBlockerPoints or Shadow2DControlMode.AddSunlightPoints)
                    {
                        bool escState = Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Escape);
                        if (escState && !this._renderEscDown && !ImGui.GetIO().WantCaptureKeyboard)
                        {
                            this._renderEscDown = true;
                            this._numQuadDrawPoints = 0;
                            Client.Instance.Frontend.Renderer.GuiRenderer.NotifyOfEscapeCaptureThisFrame();
                        }

                        if (!escState && this._renderEscDown)
                        {
                            this._renderEscDown = false;
                        }
                    }

                    GLState.DepthMask.Set(true);
                    GLState.Blend.Set(false);
                    GLState.DepthTest.Set(true);
                    GLState.CullFace.Set(false);
                }

                if (m.Has2DShadows && m.Is2D)
                {
                    if (!m.ShadowLayer2D.BVH.WasUploaded)
                    {
                        m.ShadowLayer2D.BVH.Upload(this);
                    }

                    if (this.BoxesBufferTexture != null && this.BVHNodesBufferTexture != null)
                    {
                        GL.BindFramebuffer(FramebufferTarget.All, this.FBO.Value);
                        GL.Viewport(0, 0, SimulationWidth, SimulationHeight);
                        GLState.DepthTest.Set(false);
                        if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
                        {
                            GLState.Multisample.Set(false);
                        }

                        GLState.DepthMask.Set(false);

                        UniversalPipeline pipeline = Client.Instance.Frontend.Renderer.Pipeline;
                        this.Raytracer.Bind();

                        GLState.ActiveTexture.Set(0);
                        pipeline.PositionTex.Bind();
                        GLState.ActiveTexture.Set(1);
                        this.BoxesBufferTexture.Bind();
                        GLState.ActiveTexture.Set(2);
                        this.BVHNodesBufferTexture.Bind();
                        GLState.ActiveTexture.Set(0);

                        this.Raytracer.Uniforms.BVHHasData.Set(m.ShadowLayer2D.BVH.HasAnyBoxes);
                        Vector3 cpos = Client.Instance.Frontend.Renderer.MapRenderer.GetTerrainCursorOrPointAlongsideView();
                        EditMode em = Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode;
                        bool ctrl = !ImGui.GetIO().WantCaptureMouse && !ImGui.GetIO().WantCaptureKeyboard && Client.Instance.Frontend.GameHandle.IsAnyControlDown();
                        this.Raytracer.Uniforms.ShadowOpacity.Set(Client.Instance.IsAdmin ? em == EditMode.Shadows2D ? ctrl ? 0.0f : 1.0f : ctrl ? 0.0f : 1.0f - Client.Instance.Settings.Shadows2DAdmin : 0.0f);

                        float vMax;
                        float vDim;
                        Vector2 lightCursor;
                        SelectionManager sm = Client.Instance.Frontend.Renderer.SelectionManager;
                        MapObject mainSelectCandidate = null;

                        // For lights and shadows purposes observers are treated as admins
                        bool isAdmin = Client.Instance.IsAdmin || Client.Instance.IsObserver; 
                        bool noCursor = false;
                        if (isAdmin)
                        {
                            // For admins, the selection candidate is the one they have selected
                            foreach (MapObject mo in sm.SelectedObjects)
                            {
                                if (mo.IsShadow2DViewpoint || mo.IsShadow2DLightSource)
                                {
                                    mainSelectCandidate = mo;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // For non-admins first build a list of all possible candidates - those being the objects the clients can control
                            foreach (MapObject mo in m.IterateObjects(0))
                            {
                                if (mo.IsShadow2DViewpoint && mo.CanEdit(Client.Instance.ID))
                                {
                                    this._cursorCandidates.Add(mo);
                                    if (Guid.Equals(mo.OwnerID, Client.Instance.ID))
                                    {
                                        // Put objects we own directly into a second list
                                        this._nonAdminOwnedCursorCandidates.Add(mo);
                                    }
                                }
                            }

                            foreach (MapObject mo in this._cursorCandidates)
                            {
                                // If we can control an object and have it selected - this is our candidate
                                if (sm.SelectedObjects.Contains(mo))
                                {
                                    mainSelectCandidate = mo;
                                    break;
                                }
                            }

                            // Don't have any object selected
                            if (mainSelectCandidate == null)
                            {
                                // Check for direct ownership objects
                                // For shadow viewing purposes we give priority first to objects
                                // Which are owned by the client directly rather than owned by nobody, and can be edited too

                                // Have objects we own directly
                                if (this._nonAdminOwnedCursorCandidates.Count > 0)
                                {
                                    // We have multiple object we own, pick one that's closest to the cursor
                                    if (this._nonAdminOwnedCursorCandidates.Count > 1)
                                    {
                                        float dstMin = float.MaxValue;
                                        MapObject c = null;
                                        foreach (MapObject mo in this._nonAdminOwnedCursorCandidates)
                                        {
                                            Vector2 p = GetObjectPosition(mo, m);
                                            float d = (p - this._cursorWorldLastUpdate).Length();
                                            if (d < dstMin)
                                            {
                                                dstMin = d;
                                                c = mo;
                                            }
                                        }

                                        mainSelectCandidate = c;
                                    }
                                    else
                                    {
                                        // We have exactly one owned object
                                        mainSelectCandidate = this._nonAdminOwnedCursorCandidates[0];
                                    }
                                }
                            }

                            // Have no object selected and no objects directly owned
                            if (mainSelectCandidate == null)
                            {
                                // Try picking an object that is closest to the cursor atm
                                float dstMin = float.MaxValue;
                                MapObject c = null;
                                foreach (MapObject mo in this._cursorCandidates)
                                {
                                    Vector2 p = GetObjectPosition(mo, m);
                                    float d = (p - this._cursorWorldLastUpdate).Length();
                                    if (d < dstMin)
                                    {
                                        dstMin = d;
                                        c = mo;
                                    }
                                }

                                mainSelectCandidate = c;
                            }
                        }

                        if (mainSelectCandidate != null)
                        {
                            vMax = mainSelectCandidate.Shadow2DViewpointData.Y;
                            vDim = mainSelectCandidate.Shadow2DViewpointData.X;
                            if (mainSelectCandidate.IsShadow2DLightSource)
                            {
                                vMax = MathF.Max(vMax, mainSelectCandidate.Shadow2DLightSourceData.Y);
                                vDim = MathF.Max(vDim, mainSelectCandidate.Shadow2DLightSourceData.X);
                            }

                            lightCursor = GetObjectPosition(mainSelectCandidate, m);
                            // In addition to this
                            if (Client.Instance.Frontend.GameHandle.IsAnyShiftDown())
                            {
                                // Clients can move the cursor within their object's bounds
                                if (mainSelectCandidate.ClientAssignedModelBounds) // Have bounds
                                {
                                    AABox cBB = mainSelectCandidate.ClientModelRaycastBox.Scale(mainSelectCandidate.Scale);
                                    Vector3 cAvg = cBB.Center;
                                    cBB = cBB.Offset(cAvg).Offset(new Vector3(lightCursor, 0));
                                    Vector2 v0 = cBB.Start.Xy();
                                    Vector2 v1 = cBB.End.Xy();
                                    Vector2 p = Vector2.Min(Vector2.Max(this._cursorWorldLastUpdate, v0), v1);
                                    lightCursor = p;
                                }
                            }

                            foreach (Shadow2DBox box in m.ShadowLayer2D.EnumerateBoxes())
                            {
                                if (box.BoxType == Shadow2DBox.ShadowBoxType.Sunlight && box.IsActive)
                                {
                                    if (box.Contains(lightCursor))
                                    {
                                        vMax = float.MaxValue;
                                        vDim = float.MaxValue;
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // For admins with no candidates we allow full vision in this case
                            if (isAdmin)
                            {
                                vMax = float.MaxValue;
                                vDim = float.MaxValue;
                                // Emitted from the cursor for preview purposes
                                lightCursor = new Vector2(cpos.X, cpos.Y);
                            }
                            else
                            {
                                // The whole screen of the client will be black in this case!
                                vMax = vDim = 0;
                                lightCursor = Vector2.Zero;
                                noCursor = true;
                            }
                        }

                        this.Raytracer.Uniforms.LightThreshold.Set(vMax);
                        this.Raytracer.Uniforms.LightDimming.Set(vDim);
                        this.Raytracer.Uniforms.CursorPosition.Set(lightCursor);
                        this.Raytracer.Uniforms.NoCursor.Set(noCursor);

                        int l_amt = 0;
                        for (int i = -2; i <= 0; ++i)
                        {
                            foreach (MapObject mo in m.IterateObjects(i))
                            {
                                if (mo.IsShadow2DLightSource && mo != mainSelectCandidate)
                                {
                                    this._lights[l_amt++] = new Shadow2DLightSource(mo.Position.Xy(), mo.Shadow2DLightSourceData.Y, mo.Shadow2DLightSourceData.X);
                                    if (l_amt >= 64)
                                    {
                                        break;
                                    }
                                }
                            }
                        }

                        this.Raytracer.Uniforms.LightsAmount.Set(l_amt);
                        for (int i = 0; i < l_amt; ++i)
                        {
                            this._lights[i].Uniform(this.Raytracer, i);
                        }

                        pipeline.DrawFullScreenQuad();

                        GLState.DepthMask.Set(true);
                        GLState.DepthTest.Set(true);
                        if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
                        {
                            GLState.Multisample.Set(true);
                        }

                        GL.BindFramebuffer(FramebufferTarget.All, 0);
                        GL.Viewport(0, 0, Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
                    }
                }
            }

            this.CPUTimer.Stop();
            OpenGLUtil.EndSection();
        }
    
        public void RenderBoxesOverlay(Map m)
        {
            OpenGLUtil.StartSection("2D Shadow overlay");
            EditMode em = Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode;

            bool emState = em == EditMode.Shadows2D;
            bool lmbState = Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Left);
            bool handleLmbStateNow = false;
            if (lmbState && !this._renderLmbDown && !ImGui.GetIO().WantCaptureMouse)
            {
                this._renderLmbDown = true;
                handleLmbStateNow = true;
            }

            if (!lmbState && this._renderLmbDown)
            {
                this._renderLmbDown = false;
            }

            if (Client.Instance.IsAdmin && em == EditMode.Shadows2D && m.Is2D)
            {
                if (this.ControlMode is Shadow2DControlMode.AddBlockerPoints or Shadow2DControlMode.AddSunlightPoints)
                {
                    if (handleLmbStateNow)
                    {
                        this.AddQuadDrawPoint(m, Client.Instance.Frontend.Renderer.MapRenderer.GetTerrainCursorOrPointAlongsideView().Xy());
                    }
                }
                else
                {
                    this._numQuadDrawPoints = 0; // Reset if we are not drawing points
                }

                if (this.ControlMode == Shadow2DControlMode.AddBlockerLine)
                {
                    if (handleLmbStateNow)
                    {
                        if (this._lineModeFirstPoint == null)
                        {
                            this._lineModeFirstPoint = Client.Instance.Frontend.Renderer.MapRenderer.GetTerrainCursorOrPointAlongsideView().Xy();
                        }
                        else
                        {
                            if (this._lineModeLineWidth >= 0.01f)
                            {
                                Vector2 here = Client.Instance.Frontend.Renderer.MapRenderer.GetTerrainCursorOrPointAlongsideView().Xy();
                                Vector2 prev = this._lineModeFirstPoint.Value;
                                Vector2 normal = Vector2.Normalize(here - prev);
                                Vector2 right = normal.Rotate(MathF.PI * 0.5f) * this._lineModeLineWidth * 0.5f;

                                // Somewhat inefficient, can be done in a better way with atan2 as normal is known, but default to existing method for better code modularity
                                this._numQuadDrawPoints = 0;
                                this.AddQuadDrawPoint(m, prev - right, Shadow2DBox.ShadowBoxType.Blocker);
                                this.AddQuadDrawPoint(m, here - right, Shadow2DBox.ShadowBoxType.Blocker);
                                this.AddQuadDrawPoint(m, here + right, Shadow2DBox.ShadowBoxType.Blocker);
                                this.AddQuadDrawPoint(m, prev + right, Shadow2DBox.ShadowBoxType.Blocker);
                                this._lineModeFirstPoint = here;
                            }
                        }
                    }

                    bool escState = Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Escape);
                    if (escState && !this._renderEscDown && !ImGui.GetIO().WantCaptureKeyboard)
                    {
                        this._renderEscDown = true;
                        this._lineModeFirstPoint = null;
                        Client.Instance.Frontend.Renderer.GuiRenderer.NotifyOfEscapeCaptureThisFrame();
                    }

                    if (!escState && this._renderEscDown)
                    {
                        this._renderEscDown = false;
                    }
                }
                else
                {
                    this._lineModeFirstPoint = null;
                }

                ShaderProgram overlayShader = this.BoxesOverlay;
                Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;

                GLState.CullFace.Set(true);
                GLState.CullFaceMode.Set(PolygonFaceMode.Back);
                GLState.DepthTest.Set(false);
                GLState.Blend.Set(true);
                GLState.BlendFunc.Set((BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha));
                GLState.DepthMask.Set(false);

                overlayShader.Bind();
                overlayShader["view"].Set(cam.View);
                overlayShader["projection"].Set(cam.Projection);

                this.OverlayVAO.Bind();
                this.OverlayVBO.Bind();

                foreach (Shadow2DBox box in m.ShadowLayer2D.EnumerateBoxes().OrderByDescending(x => (int)x.BoxType))
                {
                    Color clr = box.BoxType == Shadow2DBox.ShadowBoxType.Blocker
                        ? box.IsActive ? Color.SlateBlue : Color.DarkGoldenrod
                        : box.IsActive ? Color.LightCyan : Color.LightGray;

                    Matrix4x4 transform = Matrix4x4.Identity * Matrix4x4.CreateRotationZ(-box.Rotation) * Matrix4x4.CreateTranslation(new Vector3(box.Center, 0));
                    Vector2 halfExtent = (box.End - box.Start) * 0.5f;
                    overlayShader["model"].Set(transform);
                    this.CreateRectangle(halfExtent, clr.Argb(), box == this._boxSelected ? Color.MediumBlue.Argb() : box.IsMouseOver ? Color.RoyalBlue.Argb() : clr.Argb());
                    GLState.DrawArrays(PrimitiveType.Triangles, 0, 6 * 5);
                }

                if (this._isBoxDragging)
                {
                    Vector2 start = Vector2.Min(this._initialClickWorldPos, this._cursorWorldLastUpdate);
                    Vector2 end = Vector2.Max(this._initialClickWorldPos, this._cursorWorldLastUpdate);

                    Vector2 c = start + ((end - start) * 0.5f);

                    bool allowed = end.X - start.X >= 0.01f && end.Y - start.Y >= 0.01f;

                    Color clr = !allowed ? Color.Red : this.ControlMode is Shadow2DControlMode.AddBlocker or Shadow2DControlMode.AddBlockerPoints ? Color.SlateBlue : Color.LightCyan;

                    Matrix4x4 transform = Matrix4x4.Identity * Matrix4x4.CreateTranslation(new Vector3(c, 0));
                    Vector2 halfExtent = (end - start) * 0.5f;
                    overlayShader["model"].Set(transform);
                    this.CreateRectangle(halfExtent, clr.Argb(), Color.RoyalBlue.Argb());
                    GLState.DrawArrays(PrimitiveType.Triangles, 0, 6 * 5);
                }

                if (this._lineModeFirstPoint.HasValue)
                {
                    Vector2 start = this._lineModeFirstPoint.Value;
                    Vector2 end = this._cursorWorldLastUpdate;
                    Vector2 n = Vector2.Normalize(end - start);
                    Vector2 r = n.Rotate(MathF.PI * 0.5f) * this._lineModeLineWidth * 0.5f;
                    Vector2 c = start + ((end - start) * 0.5f);
                    Matrix4x4 transform = Matrix4x4.Identity;
                    overlayShader["model"].Set(transform);
                    this.CreateRectangleFromPoints(start - r, start + r, end + r, end - r, Color.SlateBlue.Argb(), Color.RoyalBlue.Argb());
                    GLState.DrawArrays(PrimitiveType.Triangles, 0, 6 * 5);
                }

                if (this._numQuadDrawPoints > 0)
                {
                    this.OverlayVBO.SetData(IntPtr.Zero, sizeof(float) * 4 * 6 * 5);
                    int idx = 0;
                    Color clr = this.ControlMode is Shadow2DControlMode.AddBlockerPoints ? Color.SlateBlue : Color.LightCyan;
                    for (int i = 0; i < this._numQuadDrawPoints; ++i)
                    {
                        float s1 = MathF.Sin(2 * MathF.PI / 5) * 0.05f;
                        float c1 = MathF.Cos(2 * MathF.PI / 5) * 0.05f;
                        float s2 = MathF.Sin(4 * MathF.PI / 5) * 0.05f;
                        float c2 = MathF.Cos(MathF.PI / 5) * 0.05f;

                        Vector2 center = this._shadowSelectionPointsArray[i];

                        this._reusedBuffer[idx++] = new Vector4(center.X, center.Y + 0.05f, 0, VTTMath.UInt32BitsToSingle(clr.Argb()));
                        this._reusedBuffer[idx++] = new Vector4(center.X + s2, center.Y - c2, 0, VTTMath.UInt32BitsToSingle(clr.Argb()));
                        this._reusedBuffer[idx++] = new Vector4(center.X + s1, center.Y + c1, 0, VTTMath.UInt32BitsToSingle(clr.Argb()));
                        this._reusedBuffer[idx++] = new Vector4(center.X, center.Y + 0.05f, 0, VTTMath.UInt32BitsToSingle(clr.Argb()));
                        this._reusedBuffer[idx++] = new Vector4(center.X - s2, center.Y - c2, 0, VTTMath.UInt32BitsToSingle(clr.Argb()));
                        this._reusedBuffer[idx++] = new Vector4(center.X + s2, center.Y - c2, 0, VTTMath.UInt32BitsToSingle(clr.Argb()));
                        this._reusedBuffer[idx++] = new Vector4(center.X, center.Y + 0.05f, 0, VTTMath.UInt32BitsToSingle(clr.Argb()));
                        this._reusedBuffer[idx++] = new Vector4(center.X - s1, center.Y + c1, 0, VTTMath.UInt32BitsToSingle(clr.Argb()));
                        this._reusedBuffer[idx++] = new Vector4(center.X - s2, center.Y - c2, 0, VTTMath.UInt32BitsToSingle(clr.Argb()));
                    }

                    unsafe
                    {
                        this.OverlayVBO.SetSubData((IntPtr)this._reusedBuffer.GetPointer(), sizeof(float) * 4 * 3 * 3 * this._numQuadDrawPoints, 0);
                    }

                    overlayShader["model"].Set(Matrix4x4.Identity);
                    GLState.DrawArrays(PrimitiveType.Triangles, 0, 3 * 3 * this._numQuadDrawPoints);
                }

                /* BVH debug overlay
                overlayShader["model"].Set(Matrix4x4.Identity);
                this.RenderBVHDebug(m.ShadowLayer2D, this._bvhDebugDepth, 0xffaa00aa);
                bool periodState = Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Period);
                bool commaState = Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Comma);
                if (!ImGui.GetIO().WantCaptureKeyboard)
                {
                    if (periodState && !this._bvhDebugPeriodDown)
                    {
                        this._bvhDebugPeriodDown = true;
                        this._bvhDebugDepth += 1;
                    }

                    if (commaState && !this._bvhDebugCommaDown)
                    {
                        this._bvhDebugCommaDown = true;
                        this._bvhDebugDepth -= 1;
                        if (this._bvhDebugDepth < 0)
                        {
                            this._bvhDebugDepth = 0;
                        }
                    }
                }

                if (!periodState && this._bvhDebugPeriodDown)
                {
                    this._bvhDebugPeriodDown = false;
                }

                if (!commaState && this._bvhDebugCommaDown)
                {
                    this._bvhDebugCommaDown = false;
                }
                */

                GLState.DepthMask.Set(true);
                GLState.Blend.Set(false);
                GLState.DepthTest.Set(true);
                GLState.CullFace.Set(false);
            }
            else
            {
                this._numQuadDrawPoints = 0; // Reset if we are not the active mode
                this._lineModeFirstPoint = null;
            }

            OpenGLUtil.EndSection();
        }

        private int _bvhDebugDepth = 0;
        private bool _bvhDebugPeriodDown = false;
        private bool _bvhDebugCommaDown = false;
        private void RenderBVHDebug(Map2DShadowLayer m, int targetDepth, uint color)
        {
            foreach (Shadow2DBVH.ShadowOBB2D box in m.BVH.EnumerateBoxesAtDepth(targetDepth))
            {
                box.GetCorners(out Vector2 tl, out Vector2 tr, out Vector2 br, out Vector2 bl);
                this.CreateRectangleFromPoints(tl, tr, br, bl, color, color);
                GLState.DrawArrays(PrimitiveType.Triangles, 0, 6 * 5);
            }
        }

        private UnsafeArray<Vector4> _reusedBuffer;

        private unsafe void CreateRectangleFromPoints(Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl, uint boxColor, uint borderColor)
        {
            boxColor &= 0x77ffffffu;
            this.OverlayVBO.SetData(IntPtr.Zero, sizeof(float) * 4 * 6 * 5);
            float minX = Math.Clamp(MathF.Abs((tr - tl).X * 0.5f) * 0.2f, 0.01f, 0.075f);
            float minY = Math.Clamp(MathF.Abs((tl - bl).Y * 0.5f) * 0.2f, 0.01f, 0.075f);
            float sideWH = MathF.Min(minX, minY);
            int idx = 0;
            this.AddQuad(tl, tr, br, bl, ref idx, boxColor);
            Vector2 rightY = Vector2.Normalize(tl - bl).Rotate(MathF.PI * 0.5f) * sideWH; // * 0.5f already baked in by minX/Y
            Vector2 rightX = Vector2.Normalize(tr - tl).Rotate(MathF.PI * 0.5f) * sideWH; // * 0.5f already baked in by minX/Y
            this.AddQuad(tl, tl + rightY, bl + rightY, bl, ref idx, borderColor);
            this.AddQuad(tr - rightY, tr, br, br - rightY, ref idx, borderColor);
            this.AddQuad(tl + rightY, tr - rightY, tr - rightY + rightX, tl + rightY + rightX, ref idx, borderColor);
            this.AddQuad(bl + rightY - rightX, br - rightY - rightX, br - rightY, bl + rightY, ref idx, borderColor);
            this.OverlayVBO.SetSubData((IntPtr)this._reusedBuffer.GetPointer(), sizeof(float) * 120, 0);
        }

        private unsafe void CreateRectangle(Vector2 halfExtent, uint boxColor, uint borderColor)
        {
            boxColor &= 0x77ffffffu;
            this.OverlayVBO.SetData(IntPtr.Zero, sizeof(float) * 4 * 6 * 5);
            float minX = MathF.Min(0.075f, halfExtent.X * 0.2f);
            float minY = MathF.Min(0.075f, halfExtent.Y * 0.2f);
            float sideWH = MathF.Min(minX, minY);
            int idx = 0;
            this.AddQuad(-halfExtent, halfExtent, ref idx, boxColor);

            this.AddQuad(-halfExtent, new Vector2(-halfExtent.X + sideWH, halfExtent.Y), ref idx, borderColor);
            this.AddQuad(new Vector2(halfExtent.X - sideWH, -halfExtent.Y), halfExtent, ref idx, borderColor);
            this.AddQuad(new Vector2(-halfExtent.X + sideWH, -halfExtent.Y), new Vector2(halfExtent.X - sideWH, -halfExtent.Y + sideWH), ref idx, borderColor);
            this.AddQuad(new Vector2(-halfExtent.X + sideWH, halfExtent.Y - sideWH), new Vector2(halfExtent.X - sideWH, halfExtent.Y), ref idx, borderColor);
            this.OverlayVBO.SetSubData((IntPtr)this._reusedBuffer.GetPointer(), sizeof(float) * 120, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddTriangle(Vector2 p1, Vector2 p2, Vector2 p3, ref int index, uint color)
        {
            this._reusedBuffer[index++] = new Vector4(p1, 0, VTTMath.UInt32BitsToSingle(color));
            this._reusedBuffer[index++] = new Vector4(p2, 0, VTTMath.UInt32BitsToSingle(color));
            this._reusedBuffer[index++] = new Vector4(p3, 0, VTTMath.UInt32BitsToSingle(color));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddQuad(Vector2 start, Vector2 end, ref int index, uint color)
        {
            Vector2 tl = start;
            Vector2 br = end;
            Vector2 tr = new Vector2(end.X, start.Y);
            Vector2 bl = new Vector2(start.X, end.Y);
            this.AddQuad(tl, tr, br, bl, ref index, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddQuad(Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl, ref int index, uint color)
        {
            this.AddTriangle(tl, tr, br, ref index, color);
            this.AddTriangle(tl, br, bl, ref index, color);
        }
    }

    public enum Shadow2DControlMode
    {
        Select,
        Translate,
        Rotate,
        Toggle,
        AddBlocker,
        AddBlockerPoints,
        AddBlockerLine,
        AddSunlight,
        AddSunlightPoints,
        Delete
    }
}
