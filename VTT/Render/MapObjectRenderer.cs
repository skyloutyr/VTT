﻿namespace VTT.Render
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using VTT.Asset;
    using VTT.Asset.Glb;
    using VTT.Asset.Obj;
    using VTT.Control;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Render.LightShadow;
    using VTT.Render.Shaders;
    using VTT.Util;

    public class MapObjectRenderer
    {
        private VertexArray _boxVao;
        private GPUBuffer _boxVbo;

        private VertexArray _noAssetVao;
        private GPUBuffer _noAssetVbo;
        private GPUBuffer _noAssetEbo;

        //public ShaderProgram RenderShader { get; set; }
        public FastAccessShader<HighlightUniforms> HighlightShader { get; set; }
        public FastAccessShader<FOWDependentOverlayUniforms> OverlayShader { get; set; }
        public FastAccessShader<ForwardMeshOnlyUniforms> PreviewShader { get; set; }
        public MapObject ObjectMouseOver { get; set; }
        public MapObject ObjectListObjectMouseOver { get; set; }
        public Vector3 MouseHitWorld { get; set; }

        public EditMode EditMode { get; set; } = EditMode.Select;
        public TranslationMode MovementMode { get; set; } = TranslationMode.Gizmo;

        public WavefrontObject MoveArrow { get; set; }
        public WavefrontObject MoveSide { get; set; }
        public WavefrontObject MoveCenter { get; set; }

        public WavefrontObject ScaleArrow { get; set; }
        public WavefrontObject ScaleSide { get; set; }
        public WavefrontObject ScaleCenter { get; set; }
        public WavefrontObject ArrowMove { get; set; }
        public WavefrontObject ArrowMoveOutline { get; set; }

        public WavefrontObject RotateCircle { get; set; }
        public WavefrontObject Cross { get; set; }

        public WavefrontObject AuraSphere { get; set; }

        public SunShadowRenderer DirectionalLightRenderer { get; set; }
        public FastLightRenderer FastLightRenderer { get; set; }
        public Shadow2DRenderer Shadow2DRenderer { get; set; }
        public PortalHighlightRenderer PortalHightlightRenderer { get; set; }

        private Vector3 _cachedSunDir;
        private Color _cachedSunColor;
        private Vector3 _cachedAmbientColor;
        private Color _cachedSkyColor;
        private readonly ShaderContainerLocalPassthroughData _passthroughData = new ShaderContainerLocalPassthroughData();

        public GlbScene MissingModel { get; set; }

        public Stopwatch CPUTimerMain { get; set; }
        public Stopwatch CPUTimerUBOUpdate { get; set; }
        public Stopwatch CPUTimerAuras { get; set; }
        public Stopwatch CPUTimerGizmos { get; set; }
        public Stopwatch CPUTimerLights { get; set; }
        public Stopwatch CPUTimerDeferred { get; set; }
        public Stopwatch CPUTimerHighlights { get; set; }
        public Stopwatch CPUTimerCompound { get; set; }

        public void Create()
        {
            this._noAssetVao = new VertexArray();
            this._noAssetVbo = new GPUBuffer(BufferTarget.Array);
            this._noAssetEbo = new GPUBuffer(BufferTarget.ElementArray);
            this._noAssetVao.Bind();
            this._noAssetVbo.Bind();
            this._noAssetVbo.SetData(new float[] {
                0.5f, 0.5f, -0.5f, 0, 0,
                0.5f,-0.5f, -0.5f, 0, 0,
                0.5f, 0.5f,  0.5f, 0, 0,
                0.5f,-0.5f,  0.5f, 0, 0,
               -0.5f, 0.5f, -0.5f, 0, 0,
               -0.5f,-0.5f, -0.5f, 0, 0,
               -0.5f, 0.5f,  0.5f, 0, 0,
               -0.5f,-0.5f,  0.5f, 0, 0,
            });

            this._noAssetEbo.Bind();
            this._noAssetEbo.SetData(new uint[] {
                5 - 1, 3 - 1, 1 - 1,
                3 - 1, 8 - 1, 4 - 1,
                7 - 1, 6 - 1, 8 - 1,
                2 - 1, 8 - 1, 6 - 1,
                1 - 1, 4 - 1, 2 - 1,
                5 - 1, 2 - 1, 6 - 1,
                5 - 1, 7 - 1, 3 - 1,
                3 - 1, 7 - 1, 8 - 1,
                7 - 1, 5 - 1, 6 - 1,
                2 - 1, 4 - 1, 8 - 1,
                1 - 1, 3 - 1, 4 - 1,
                5 - 1, 1 - 1, 2 - 1,
            });

            this._noAssetVao.Reset();
            this._noAssetVao.SetVertexSize<float>(5);
            this._noAssetVao.PushElement(ElementType.Vec3);
            this._noAssetVao.PushElement(ElementType.Vec2);

            OpenGLUtil.NameObject(GLObjectType.VertexArray, this._noAssetVao, "Missing asset vao");
            OpenGLUtil.NameObject(GLObjectType.Buffer, this._noAssetVbo, "Missing asset vbo");
            OpenGLUtil.NameObject(GLObjectType.Buffer, this._noAssetEbo, "Missing asset ebo");

            this.OverlayShader = new FastAccessShader<FOWDependentOverlayUniforms>(OpenGLUtil.LoadShader("moverlay", ShaderType.Vertex, ShaderType.Fragment));
            this.OverlayShader.Bind();
            this.OverlayShader.Uniforms.DoFOW.Set(false);
            this.OverlayShader.Uniforms.FOW.Sampler.Set(15);
            this.HighlightShader = new FastAccessShader<HighlightUniforms>(OpenGLUtil.LoadShader("highlight", ShaderType.Vertex, ShaderType.Fragment));
            this.PrecomputeSelectionBox();

            this.MoveArrow = OpenGLUtil.LoadModel("arrow_arrow", VertexFormat.Pos);
            this.MoveSide = OpenGLUtil.LoadModel("arrow_square", VertexFormat.Pos);
            this.MoveCenter = OpenGLUtil.LoadModel("arrow_center_arrow", VertexFormat.Pos);

            this.ArrowMove = OpenGLUtil.LoadModel("arrow_move", VertexFormat.Pos);
            this.ArrowMoveOutline = OpenGLUtil.LoadModel("arrow_move_outline", VertexFormat.Pos);

            this.ScaleArrow = OpenGLUtil.LoadModel("arrow_cube", VertexFormat.Pos);
            this.ScaleSide = OpenGLUtil.LoadModel("arrow_square", VertexFormat.Pos);
            this.ScaleCenter = OpenGLUtil.LoadModel("arrow_center_square", VertexFormat.Pos);

            this.RotateCircle = OpenGLUtil.LoadModel("arrow_ring", VertexFormat.Pos);
            this.Cross = OpenGLUtil.LoadModel("cross", VertexFormat.Pos);

            this.AuraSphere = OpenGLUtil.LoadModel("sphere_uhd", VertexFormat.Pos);

            this.DirectionalLightRenderer = new SunShadowRenderer();
            this.DirectionalLightRenderer.Create();
            this.FastLightRenderer = new FastLightRenderer();
            this.FastLightRenderer.Create();
            this.Shadow2DRenderer = new Shadow2DRenderer();
            this.Shadow2DRenderer.Create();
            this.PortalHightlightRenderer = new PortalHighlightRenderer();
            this.PortalHightlightRenderer.Create();

            this.FrameUBOManager = new FrameUBOManager();
            this.BonesUBOManager = new BonesUBO();

            this.CPUTimerAuras = new Stopwatch();
            this.CPUTimerGizmos = new Stopwatch();
            this.CPUTimerMain = new Stopwatch();
            this.CPUTimerUBOUpdate = new Stopwatch();
            this.CPUTimerLights = new Stopwatch();
            this.CPUTimerDeferred = new Stopwatch();
            this.CPUTimerHighlights = new Stopwatch();
            this.CPUTimerCompound = new Stopwatch();
            this.MissingModel = new GlbScene(new ModelData.Metadata(), IOVTT.ResourceToStream("VTT.Embed.missing.glb"));

            this.PreviewShader = new FastAccessShader<ForwardMeshOnlyUniforms>(OpenGLUtil.LoadShader("object_preview", ShaderType.Vertex, ShaderType.Fragment));
        }

        #region Hightlight Box

        private static readonly double[] vertexData = {
            -1.000000, -1.000000, -1.000000,
            -1.000000, -1.000000, 1.000000 ,
            -1.000000, 1.000000 ,-1.000000 ,
            -1.000000, 1.000000 ,1.000000  ,
            1.000000 ,-1.000000 ,-1.000000 ,
            1.000000 ,-1.000000 ,1.000000  ,
            1.000000 ,1.000000 ,-1.000000  ,
            1.000000 ,1.000000 ,1.000000   ,
            5.000000 ,1.000000 ,1.000000   ,
            5.000000 ,1.000000 ,-1.000000  ,
            5.000000 ,-1.000000, -1.000000 ,
            5.000000 ,-1.000000, 1.000000  ,
            -1.000000, 5.000000, 1.000000  ,
            -1.000000, 5.000000, -1.000000 ,
            1.000000 ,5.000000 ,-1.000000  ,
            1.000000 ,5.000000 ,1.000000   ,
            -1.000000, -1.000000, 5.000000 ,
            -1.000000, 1.000000 ,5.000000  ,
            1.000000 ,1.000000 ,5.000000   ,
            1.000000 ,-1.000000, 5.000000
        };

        private static readonly int[] indexData = {
            2, 3,  1,
            8, 13, 4,
            7, 11, 5,
            6, 1,  5,
            7, 1,  3,
            4, 19, 8,
            8, 10, 7,
            5, 12, 6,
            4, 14, 3,
            7, 16, 8,
            3, 15, 7,
            8, 20, 6,
            2, 18, 4,
            6, 17, 2,
            6, 12, 9,
            6, 9,  8,
            2, 4,  3,
            6, 2,  1,
            7, 5,  1,
            9, 11, 10,
            13,15, 14,
            18,20, 19,
            8, 16, 13,
            7, 10, 11,
            4, 18, 19,
            9, 12, 11,
            5, 11, 12,
            13,16, 15,
            4, 13, 14,
            7, 15, 16,
            3, 14, 15,
            18,17, 20,
            8, 19, 20,
            2, 17, 18,
            6, 20, 17,
            8, 9,  10,
        };

        private static readonly Quaternion[] boxRotationalQuaternions = new Quaternion[] {
            Quaternion.Identity,                                                       // -X, -Y, -Z
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 90 * MathF.PI / 180),  // +X, -Y, -Z
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -90 * MathF.PI / 180), // -X, +Y, -Z
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 180 * MathF.PI / 180), // +X, +Y, -Z
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, 180 * MathF.PI / 180), // -X, +Y, +Z
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 180 * MathF.PI / 180), // +X, -Y, +Z
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 90 * MathF.PI / 180),  // -X, -Y, +Z
            new Quaternion(0.707f, -0.707f, 0f, -0f)                                   // +X, +Y, +Z and we don't talk about the -0f                                  
        };

        private static readonly Vector3[] boxTranslations = new Vector3[] {
            new Vector3(-1f, -1f, -1f),
            new Vector3(1f, -1f, -1f),
            new Vector3(-1f, 1f, -1f),
            new Vector3(1f, 1f, -1f),
            new Vector3(-1f, 1f, 1f),
            new Vector3(1f, -1f, 1f),
            new Vector3(-1f, -1f, 1f),
            new Vector3(1f, 1f, 1f),
        };

        public FrameUBOManager FrameUBOManager { get; private set; }
        public BonesUBO BonesUBOManager { get; private set; }

        public void PrecomputeSelectionBox()
        {
            float[] boxDataArray = new float[5184];
            int aIdx = 0;
            for (int i = 0; i < 8; ++i)
            {
                for (int j = 0; j < indexData.Length; ++j)
                {
                    int idx = indexData[j] - 1;
                    Vector3 v = new Vector3((float)vertexData[(idx * 3) + 0], (float)vertexData[(idx * 3) + 1], (float)vertexData[(idx * 3) + 2]);
                    v = v / 50.0f;
                    v = Vector4.Transform(new Vector4(v, 1.0f), boxRotationalQuaternions[i]).Xyz();
                    v += boxTranslations[i] / 2f;
                    boxDataArray[aIdx++] = v.X;
                    boxDataArray[aIdx++] = v.Y;
                    boxDataArray[aIdx++] = v.Z;

                    boxDataArray[aIdx++] = -boxTranslations[i].X;
                    boxDataArray[aIdx++] = -boxTranslations[i].Y;
                    boxDataArray[aIdx++] = -boxTranslations[i].Z;
                }
            }

            this._boxVao = new VertexArray();
            this._boxVbo = new GPUBuffer(BufferTarget.Array);
            this._boxVao.Bind();
            this._boxVbo.Bind();
            this._boxVbo.SetData(boxDataArray);
            this._boxVao.Reset();
            this._boxVao.SetVertexSize<float>(6);
            this._boxVao.PushElement(ElementType.Vec3);
            this._boxVao.PushElement(ElementType.Vec3);

            OpenGLUtil.NameObject(GLObjectType.VertexArray, this._boxVao, "Selection box vao");
            OpenGLUtil.NameObject(GLObjectType.Buffer, this._boxVbo, "Selection box vbo");
        }

        #endregion


        private readonly List<(Vector3, MapObject)> _mouseOverList = new List<(Vector3, MapObject)>();
        private bool _mouseOverInFow = false;
        public void Update(Map m)
        {
            this.ObjectMouseOver = null;
            if (m != null && this.EditMode != EditMode.FOW)
            {
                Ray r = Client.Instance.Frontend.Renderer.MapRenderer.RayFromCursor();
                this._mouseOverList.Clear();

                RaycastResut rr = RaycastResut.Raycast(r, m, o => o.MapLayer == Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer);
                if (rr.Result)
                {
                    bool fowTest = true;
                    if (rr.ObjectHit != null && Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.HasFOW)
                    {
                        // 0.5, 0.5 offset is needed to bring the tested AABB into pixel-space from world-space, pixels have 0,0 in the bottom left, not the center, while the AABB has 0, 0 in its center/
                        AABox bounds = rr.ObjectHit.CameraCullerBox.Offset(rr.ObjectHit.Position + new Vector3(0.5f, 0.5f, 0));
                        RectangleF projectedRect = new RectangleF(
                            bounds.Start.X, bounds.Start.Y,
                            bounds.End.X - bounds.Start.X, bounds.End.Y - bounds.Start.Y
                        );

                        fowTest = Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.FastTestRect(projectedRect, out bool oob);
                        this._mouseOverInFow = !fowTest;
                        if (oob)
                        {
                            fowTest = true; // handle outside of fow objects as always visible?
                        }
                    }
                    else
                    {
                        this._mouseOverInFow = false;
                    }

                    bool adminObserverOrOwner = Client.Instance.IsAdmin || Client.Instance.IsObserver || (rr.ObjectHit?.CanEdit(Client.Instance.ID) ?? true);
                    if (fowTest || adminObserverOrOwner)
                    {
                        this.ObjectMouseOver = rr.ObjectHit != null ? !rr.ObjectHit.HideFromSelection || adminObserverOrOwner ? rr.ObjectHit : null : rr.ObjectHit;
                    }

                    this.MouseHitWorld = rr.Hit;
                }
            }

            if (this.ObjectMouseOver == null && this.ObjectListObjectMouseOver != null)
            {
                this.ObjectMouseOver = this.ObjectListObjectMouseOver;
            }
        }

        public void RenderEarly(Map m, double delta)
        {
        }

        public void RenderLate(Map m, double delta)
        {
            if (m != null)
            {
                this.RenderAuras(m);
                this.PortalHightlightRenderer.Render(m, delta);
                this.Shadow2DRenderer?.RenderBoxesOverlay(m);
            }
        }

        public void RenderLatest(Map m, double delta)
        {
            if (m != null)
            {
                this.RenderEditMode(m);
            }
        }

        public void Resize(int w, int h) => this.FastLightRenderer.Resize(w, h);

        public void Render(Map m, double delta)
        {
            if (m != null)
            {
                OpenGLUtil.StartSection("Main render pass");
                this._cachedSunDir = Client.Instance.Frontend.Renderer.SkyRenderer.GetCurrentSunDirection();
                this._cachedSunColor = Client.Instance.Frontend.Renderer.SkyRenderer.GetSunColor();
                this._cachedAmbientColor = Client.Instance.Frontend.Renderer.SkyRenderer.GetAmbientColor().Vec3();
                this._cachedSkyColor = Client.Instance.Frontend.Renderer.SkyRenderer.GetSkyColor();
                GlbMaterial.ResetState();
                this.DirectionalLightRenderer.Render(m, delta);
                this.UpdateUBO(m, delta);
                this.RenderDeferred(m, delta);
                this.RenderHighlights(m, delta);
                this.RenderObjectMouseOver(m);
                this.RenderDebug(m);
                OpenGLUtil.EndSection();
            }
        }

        private readonly PreviewAnimationContainer _previewAnimationContainer = new PreviewAnimationContainer();
        public void RenderHighlights(Map m, double delta)
        {
            OpenGLUtil.StartSection("Object highlights");
            this.CPUTimerHighlights.Restart();
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            this.OverlayShader.Bind();
            this.OverlayShader.Uniforms.Color.Set(Color.Red.Vec4());
            this.OverlayShader.Uniforms.Transform.View.Set(cam.View);
            this.OverlayShader.Uniforms.Transform.Projection.Set(cam.Projection);
            this.OverlayShader.Uniforms.SkyColor.Set(Client.Instance.Frontend.Renderer.ObjectRenderer.CachedSkyColor);
            this.OverlayShader.Uniforms.DoFOW.Set(Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.HasFOW);
            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.Uniform(this.OverlayShader.Uniforms.FOW);
            GLState.DepthTest.Set(false);
            foreach (MapObject mo in this._crossedOutObjects)
            {
                Matrix4x4 modelMatrix = mo.ClientAssignedModelBounds
                    ? Matrix4x4.CreateScale(mo.ClientModelRaycastBox.Size * mo.Scale) * Matrix4x4.CreateTranslation(mo.Position)
                    : mo.ClientCachedModelMatrix.ClearRotation();
                this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                this.Cross.Render();
            }

            this.OverlayShader.Uniforms.DoFOW.Set(false);
            GLState.DepthTest.Set(true);
            GLState.CullFace.Set(false);
            FastAccessShader<HighlightUniforms> shader = this.HighlightShader;
            shader.Bind();
            shader.Uniforms.Transform.View.Set(cam.View);
            shader.Uniforms.Transform.Projection.Set(cam.Projection);
            shader.Uniforms.Color.Set(Color.Orange.Vec4());
            foreach (MapObject mo in Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects)
            {
                if (mo.ClientRenderedThisFrame)
                {
                    BBBox cBB = mo.ClientRaycastOOBB;
                    Vector3 size = cBB.Size;
                    Vector3 cAvg = cBB.Center;
                    Matrix4x4 modelMatrix = Matrix4x4.CreateTranslation(cAvg) * Matrix4x4.CreateFromQuaternion(mo.Rotation) * Matrix4x4.CreateTranslation(mo.Position);
                    shader.Uniforms.Transform.Model.Set(modelMatrix);
                    shader.Uniforms.Bounds.Set(size);
                    this._boxVao.Bind();
                    GLState.DrawArrays(PrimitiveType.Triangles, 0, 864);
                    this.PortalHightlightRenderer.AddObject(mo);
                }
            }

            shader.Uniforms.Color.Set(Color.SkyBlue.Vec4());
            foreach (MapObject mo in Client.Instance.Frontend.Renderer.SelectionManager.BoxSelectCandidates)
            {
                if (mo.ClientRenderedThisFrame)
                {
                    BBBox cBB = mo.ClientRaycastOOBB;
                    Vector3 size = cBB.Size;
                    Vector3 cAvg = cBB.Center;
                    Matrix4x4 modelMatrix = Matrix4x4.CreateTranslation(cAvg) * Matrix4x4.CreateFromQuaternion(mo.Rotation) * Matrix4x4.CreateTranslation(mo.Position);
                    shader.Uniforms.Transform.Model.Set(modelMatrix);
                    shader.Uniforms.Bounds.Set(size);
                    this._boxVao.Bind();
                    GLState.DrawArrays(PrimitiveType.Triangles, 0, 864);
                    this.PortalHightlightRenderer.AddObject(mo);
                }
            }

            SelectionManager sm = Client.Instance.Frontend.Renderer.SelectionManager;
            if (this.EditMode == EditMode.Translate && this.MovementMode == TranslationMode.Path && sm.ObjectMovementPath.Count > 0 && sm.IsDraggingObjects && sm.SelectedObjects.Count > 0)
            {
                RulerRenderer rr = Client.Instance.Frontend.Renderer.RulerRenderer;
                this.OverlayShader.Bind();
                this.OverlayShader.Uniforms.Transform.View.Set(cam.View);
                this.OverlayShader.Uniforms.Transform.Projection.Set(cam.Projection);
                this.OverlayShader.Uniforms.Color.Set(Extensions.FromArgb(Client.Instance.Settings.Color).Vec4());
                GLState.DepthMask.Set(false);
                for (int i = 0; i < sm.ObjectMovementPath.Count; ++i)
                {
                    Vector3 start = sm.ObjectMovementPath[i];
                    Vector3 end = i == sm.ObjectMovementPath.Count - 1 ? sm.SelectedObjects[0].Position : sm.ObjectMovementPath[i + 1];

                    this.OverlayShader.Uniforms.Transform.Model.Set(Matrix4x4.CreateScale(0.2f) * Matrix4x4.CreateTranslation(start));
                    this.MoveCenter.Render();

                    this.OverlayShader.Uniforms.Transform.Model.Set(Matrix4x4.Identity);
                    rr.CreateLine(start, end, true);
                    rr.UploadBuffers();
                    rr.RenderBuffers();
                    rr.ClearBuffers();
                }

                this.OverlayShader.Uniforms.Transform.Model.Set(Matrix4x4.CreateScale(0.2f) * Matrix4x4.CreateTranslation(sm.SelectedObjects[0].Position));
                this.MoveCenter.Render();

                GLState.DepthMask.Set(true);
            }

            OpenGLUtil.EndSection();
            OpenGLUtil.StartSection("Dragged object preview");

            GLState.CullFace.Set(true);

            AssetRef draggedRef = Client.Instance.Frontend.Renderer.GuiRenderer.DraggedAssetReference;
            if (draggedRef != null && (draggedRef.Type == AssetType.Model || draggedRef.Type == AssetType.Texture))
            {
                AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(draggedRef.AssetID, AssetType.Model, out Asset a);
                if (status == AssetStatus.Return && a.ModelGlReady)
                {
                    GLState.Blend.Set(true);
                    GLState.BlendFunc.Set((BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha));
                    GLState.DepthMask.Set(false);
                    GLState.CullFace.Set(false);

                    this.PreviewShader.Program.Bind();
                    this.PreviewShader.Uniforms.Transform.View.Set(cam.View);
                    this.PreviewShader.Uniforms.Transform.Projection.Set(cam.Projection);
                    this.PreviewShader.Uniforms.Gamma.Factor.Set(Client.Instance.Settings.Gamma);
                    Vector3? worldVec = Client.Instance.Frontend.Renderer.MapRenderer.TerrainHit;
                    if (!worldVec.HasValue)
                    {
                        Ray ray = Client.Instance.Frontend.Renderer.MapRenderer.RayFromCursor();
                        worldVec = ray.Origin + (ray.Direction * 6.0f);
                    }

                    if (ImGui.IsKeyDown(ImGuiKey.LeftAlt))
                    {
                        worldVec = MapRenderer.SnapToGrid(m.GridType, worldVec.Value, m.GridSize);
                    }

                    Matrix4x4 modelMatrix = Matrix4x4.CreateTranslation(worldVec.Value);
                    a.Model.GLMdl.Render(in this.PreviewShader.Uniforms.glbEssentials, modelMatrix, cam.Projection, cam.View, 0, a.Model.GLMdl.Animations.FirstOrDefault(), 0, this._previewAnimationContainer);

                    GLState.CullFace.Set(true);
                    GLState.DepthMask.Set(true);
                    GLState.Blend.Set(false);
                }
            }

            this.CPUTimerHighlights.Stop();
            OpenGLUtil.EndSection();
        }

        public void RenderDebug(Map m)
        {
        }

        private void RenderEditMode(Map m)
        {
            this.CPUTimerGizmos.Restart();

            if (Client.Instance.Frontend.Renderer.GuiRenderer.ShaderEditorRenderer.popupState || Client.Instance.Frontend.Renderer.GuiRenderer.ParticleEditorRenderer.popupState)
            {
                return;
            }

            SelectionManager sm = Client.Instance.Frontend.Renderer.SelectionManager;
            if (sm.SelectedObjects.Count > 0)
            {
                if (this.EditMode == EditMode.Select)
                {
                    this.CPUTimerGizmos.Stop();
                    return;
                }

                OpenGLUtil.StartSection("Gizmo");
                Vector3 min = sm.SelectedObjects[0].Position;
                Vector3 max = sm.SelectedObjects[0].Position;
                for (int i = 1; i < sm.SelectedObjects.Count; i++)
                {
                    MapObject mo = sm.SelectedObjects[i];
                    min = Vector3.Min(min, mo.Position);
                    max = Vector3.Max(max, mo.Position);
                }

                Vector3 half;
                if (sm.HalfRenderVector.HasValue)
                {
                    half = sm.HalfRenderVector.Value;
                }
                else
                {
                    half = (max - min) / 2;
                    half = min + half;
                }

                Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;

                bool is2d = Client.Instance.Frontend.Renderer.MapRenderer.IsOrtho;
                float orthozoom = Client.Instance.Frontend.Renderer.MapRenderer.ZoomOrtho;
                GLState.CullFace.Set(true);
                GLState.CullFaceMode.Set(PolygonFaceMode.Back);
                GLState.DepthTest.Set(false);
                GLState.Blend.Set(true);
                GLState.BlendFunc.Set((BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha));
                GLState.DepthMask.Set(false);

                switch (this.EditMode)
                {
                    case EditMode.Translate:
                    {
                        if (this.MovementMode == TranslationMode.Arrows)
                        {
                            float dotZ = MathF.Abs(Vector3.Dot(Vector3.UnitZ, cam.Direction));
                            float dotX = MathF.Abs(Vector3.Dot(Vector3.UnitX, cam.Direction));
                            float dotY = MathF.Abs(Vector3.Dot(Vector3.UnitY, cam.Direction));
                            Vector3 majorAxis =
                                dotZ > dotX && dotZ > dotY ? Vector3.UnitZ :
                                dotY > dotX ? Vector3.UnitY : Vector3.UnitX;

                            Vector3 offsetAxis =
                                dotZ > dotX && dotZ > dotY ? new Vector3(0, -1.5f, 0) :
                                dotY > dotX ? new Vector3(0, 0, -1.5f) : new Vector3(0, -1.5f, 0);

                            Matrix4x4 baseRotation =
                                dotZ > dotX && dotZ > dotY ? Matrix4x4.Identity :
                                dotY > dotX ? Matrix4x4.CreateRotationY(MathF.PI * 0.5f) * Matrix4x4.CreateRotationX(MathF.PI * 0.5f) * Matrix4x4.CreateRotationZ(MathF.PI * 0.5f) :
                                Matrix4x4.CreateRotationY(MathF.PI * 0.5f);

                            Vector4 renderClr = (
                                dotZ <= dotX || dotZ <= dotY ? Color.SkyBlue.Vec4() :
                                Color.GreenYellow.Vec4()) * new Vector4(1, 1, 1, 0.75f);
                            Vector4 blue = dotZ <= dotX || dotZ <= dotY ? Color.Blue.Vec4() : Color.Gold.Vec4();

                            for (int i = 0; i < 8; ++i)
                            {
                                Quaternion q = Quaternion.CreateFromAxisAngle(majorAxis, MathF.PI * (i * 0.25f));
                                Matrix4x4 modelMatrix = baseRotation * Matrix4x4.CreateTranslation(offsetAxis) * Matrix4x4.CreateScale(0.5f) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(half);
                                this.OverlayShader.Bind();
                                this.OverlayShader.Uniforms.Transform.View.Set(cam.View);
                                this.OverlayShader.Uniforms.Transform.Projection.Set(cam.Projection);
                                this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                                this.OverlayShader.Uniforms.Color.Set(renderClr);
                                this.ArrowMove.Render();
                                this.OverlayShader.Uniforms.Color.Set(blue);
                                this.ArrowMoveOutline.Render();
                            }

                            break;
                        }
                        else
                        {
                            if (is2d)
                            {
                                Matrix4x4 modelMatrix;
                                this.OverlayShader.Bind();
                                this.OverlayShader.Uniforms.Transform.View.Set(cam.View);
                                this.OverlayShader.Uniforms.Transform.Projection.Set(cam.Projection);

                                modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4x4.CreateScale(150f * orthozoom) * Matrix4x4.CreateRotationY(90 * MathF.PI / 180) * Matrix4x4.CreateTranslation(half);
                                this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                                this.OverlayShader.Uniforms.Color.Set(Color.Red.Vec4());
                                this.MoveArrow.Render();

                                modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4x4.CreateScale(150f * orthozoom) * Matrix4x4.CreateRotationX(-90 * MathF.PI / 180) * Matrix4x4.CreateTranslation(half);
                                this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                                this.OverlayShader.Uniforms.Color.Set(Color.Green.Vec4());
                                this.MoveArrow.Render();

                                modelMatrix = Matrix4x4.CreateScale(150f * orthozoom) * Matrix4x4.CreateTranslation(half);
                                this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                                this.OverlayShader.Uniforms.Color.Set(Color.Blue.Vec4() * new Vector4(1, 1, 1, 0.75f));
                                this.MoveSide.Render();
                            }
                            else
                            {
                                Matrix4x4 viewProj = cam.ViewProj;
                                Vector4 posScreen = Vector4.Transform(new Vector4(half, 1.0f), viewProj);
                                Matrix4x4 modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4x4.CreateScale(0.2f * posScreen.W) * Matrix4x4.CreateTranslation(half);

                                this.OverlayShader.Bind();
                                this.OverlayShader.Uniforms.Transform.View.Set(cam.View);
                                this.OverlayShader.Uniforms.Transform.Projection.Set(cam.Projection);
                                this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                                this.OverlayShader.Uniforms.Color.Set(Color.Blue.Vec4());
                                this.MoveArrow.Render();

                                modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4x4.CreateScale(0.2f * posScreen.W) * Matrix4x4.CreateRotationY(90 * MathF.PI / 180) * Matrix4x4.CreateTranslation(half);
                                this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                                this.OverlayShader.Uniforms.Color.Set(Color.Red.Vec4());
                                this.MoveArrow.Render();

                                modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4x4.CreateScale(0.2f * posScreen.W) * Matrix4x4.CreateRotationX(-90 * MathF.PI / 180) * Matrix4x4.CreateTranslation(half);
                                this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                                this.OverlayShader.Uniforms.Color.Set(Color.Green.Vec4());
                                this.MoveArrow.Render();

                                modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0.5f, 0.5f, 0)) * Matrix4x4.CreateScale(0.2f * posScreen.W) * Matrix4x4.CreateTranslation(half);
                                this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                                this.OverlayShader.Uniforms.Color.Set(Color.Blue.Vec4() * new Vector4(1, 1, 1, 0.75f));
                                this.MoveSide.Render();

                                modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0.5f, 0.5f, 0f)) * Matrix4x4.CreateScale(0.2f * posScreen.W) * Matrix4x4.CreateRotationY(-90 * MathF.PI / 180) * Matrix4x4.CreateTranslation(half);
                                this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                                this.OverlayShader.Uniforms.Color.Set(Color.Red.Vec4() * new Vector4(1, 1, 1, 0.75f));
                                this.MoveSide.Render();

                                modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0.5f, 0.5f, 0f)) * Matrix4x4.CreateScale(0.2f * posScreen.W) * Matrix4x4.CreateRotationX(90 * MathF.PI / 180) * Matrix4x4.CreateTranslation(half);
                                this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                                this.OverlayShader.Uniforms.Color.Set(Color.Green.Vec4() * new Vector4(1, 1, 1, 0.75f));
                                this.MoveSide.Render();

                                modelMatrix = Matrix4x4.CreateScale(0.1f * posScreen.W) * Matrix4x4.CreateTranslation(half);
                                this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                                this.OverlayShader.Uniforms.Color.Set(Color.White.Vec4());
                                this.MoveCenter.Render();
                            }

                            break;
                        }
                    }

                    case EditMode.Scale:
                    {
                        if (is2d)
                        {
                            Matrix4x4 modelMatrix;
                            this.OverlayShader.Bind();
                            this.OverlayShader.Uniforms.Transform.View.Set(cam.View);
                            this.OverlayShader.Uniforms.Transform.Projection.Set(cam.Projection);

                            modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4x4.CreateScale(150f * orthozoom) * Matrix4x4.CreateRotationY(90 * MathF.PI / 180) * Matrix4x4.CreateTranslation(half);
                            this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                            this.OverlayShader.Uniforms.Color.Set(Color.Red.Vec4());
                            this.ScaleArrow.Render();

                            modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4x4.CreateScale(150f * orthozoom) * Matrix4x4.CreateRotationX(-90 * MathF.PI / 180) * Matrix4x4.CreateTranslation(half);
                            this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                            this.OverlayShader.Uniforms.Color.Set(Color.Green.Vec4());
                            this.ScaleArrow.Render();

                            modelMatrix = Matrix4x4.CreateScale(150f * orthozoom) * Matrix4x4.CreateTranslation(half);
                            this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                            this.OverlayShader.Uniforms.Color.Set(Color.Blue.Vec4() * new Vector4(1, 1, 1, 0.75f));
                            this.MoveSide.Render();
                        }
                        else
                        {
                            Matrix4x4 viewProj = cam.ViewProj;
                            Vector4 posScreen = Vector4.Transform(new Vector4(half, 1.0f), viewProj);
                            Matrix4x4 modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4x4.CreateScale(0.2f * posScreen.W) * Matrix4x4.CreateTranslation(half);

                            this.OverlayShader.Bind();
                            this.OverlayShader.Uniforms.Transform.View.Set(cam.View);
                            this.OverlayShader.Uniforms.Transform.Projection.Set(cam.Projection);
                            this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                            this.OverlayShader.Uniforms.Color.Set(Color.Blue.Vec4());
                            this.ScaleArrow.Render();

                            modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4x4.CreateScale(0.2f * posScreen.W) * Matrix4x4.CreateRotationY(90 * MathF.PI / 180) * Matrix4x4.CreateTranslation(half);
                            this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                            this.OverlayShader.Uniforms.Color.Set(Color.Red.Vec4());
                            this.ScaleArrow.Render();

                            modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4x4.CreateScale(0.2f * posScreen.W) * Matrix4x4.CreateRotationX(-90 * MathF.PI / 180) * Matrix4x4.CreateTranslation(half);
                            this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                            this.OverlayShader.Uniforms.Color.Set(Color.Green.Vec4());
                            this.ScaleArrow.Render();

                            modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0.5f, 0.5f, 0f)) * Matrix4x4.CreateScale(0.2f * posScreen.W) * Matrix4x4.CreateTranslation(half);
                            this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                            this.OverlayShader.Uniforms.Color.Set(Color.Blue.Vec4() * new Vector4(1, 1, 1, 0.75f));
                            this.ScaleSide.Render();

                            modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0.5f, 0.5f, 0f)) * Matrix4x4.CreateScale(0.2f * posScreen.W) * Matrix4x4.CreateRotationY(-90 * MathF.PI / 180) * Matrix4x4.CreateTranslation(half);
                            this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                            this.OverlayShader.Uniforms.Color.Set(Color.Red.Vec4() * new Vector4(1, 1, 1, 0.75f));
                            this.ScaleSide.Render();

                            modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0.5f, 0.5f, 0f)) * Matrix4x4.CreateScale(0.2f * posScreen.W) * Matrix4x4.CreateRotationX(90 * MathF.PI / 180) * Matrix4x4.CreateTranslation(half);
                            this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                            this.OverlayShader.Uniforms.Color.Set(Color.Green.Vec4() * new Vector4(1, 1, 1, 0.75f));
                            this.ScaleSide.Render();

                            Vector3 a = Vector3.Cross(Vector3.UnitZ, -cam.Direction);
                            Quaternion q = new Quaternion(a, 1 + Vector3.Dot(Vector3.UnitZ, -cam.Direction));
                            modelMatrix = Matrix4x4.CreateScale(0.5f * posScreen.W) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(half);
                            this.OverlayShader.Bind();
                            this.OverlayShader.Uniforms.Transform.View.Set(cam.View);
                            this.OverlayShader.Uniforms.Transform.Projection.Set(cam.Projection);
                            this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                            this.OverlayShader.Uniforms.Color.Set(Color.White.Vec4());
                            this.RotateCircle.Render();
                        }

                        break;
                    }

                    case EditMode.Rotate:
                    {
                        if (is2d)
                        {
                            Matrix4x4 modelMatrix = Matrix4x4.CreateScale(220f * orthozoom) * Matrix4x4.CreateTranslation(half);
                            this.OverlayShader.Bind();
                            this.OverlayShader.Uniforms.Transform.View.Set(cam.View);
                            this.OverlayShader.Uniforms.Transform.Projection.Set(cam.Projection);
                            this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                            this.OverlayShader.Uniforms.Color.Set(Color.Blue.Vec4());
                            this.RotateCircle.Render();
                        }
                        else
                        {
                            Matrix4x4 viewProj = cam.ViewProj;
                            Vector4 posScreen = Vector4.Transform(new Vector4(half, 1.0f), viewProj);
                            Matrix4x4 modelMatrix = Matrix4x4.CreateScale(0.4f * posScreen.W) * Matrix4x4.CreateTranslation(half);

                            this.OverlayShader.Bind();
                            this.OverlayShader.Uniforms.Transform.View.Set(cam.View);
                            this.OverlayShader.Uniforms.Transform.Projection.Set(cam.Projection);
                            this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                            this.OverlayShader.Uniforms.Color.Set(Color.Blue.Vec4());
                            this.RotateCircle.Render();

                            modelMatrix = Matrix4x4.CreateScale(0.4f * posScreen.W) * Matrix4x4.CreateRotationY(90 * MathF.PI / 180) * Matrix4x4.CreateTranslation(half);
                            this.OverlayShader.Bind();
                            this.OverlayShader.Uniforms.Transform.View.Set(cam.View);
                            this.OverlayShader.Uniforms.Transform.Projection.Set(cam.Projection);
                            this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                            this.OverlayShader.Uniforms.Color.Set(Color.Red.Vec4());
                            this.RotateCircle.Render();

                            modelMatrix = Matrix4x4.CreateScale(0.4f * posScreen.W) * Matrix4x4.CreateRotationX(90 * MathF.PI / 180) * Matrix4x4.CreateTranslation(half);
                            this.OverlayShader.Bind();
                            this.OverlayShader.Uniforms.Transform.View.Set(cam.View);
                            this.OverlayShader.Uniforms.Transform.Projection.Set(cam.Projection);
                            this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                            this.OverlayShader.Uniforms.Color.Set(Color.Green.Vec4());
                            this.RotateCircle.Render();

                            Vector3 a = Vector3.Cross(Vector3.UnitZ, -cam.Direction);
                            Quaternion q = new Quaternion(a, 1 + Vector3.Dot(Vector3.UnitZ, -cam.Direction)).Normalized();

                            modelMatrix = Matrix4x4.CreateScale(0.5f * posScreen.W) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(half);
                            this.OverlayShader.Bind();
                            this.OverlayShader.Uniforms.Transform.View.Set(cam.View);
                            this.OverlayShader.Uniforms.Transform.Projection.Set(cam.Projection);
                            this.OverlayShader.Uniforms.Transform.Model.Set(modelMatrix);
                            this.OverlayShader.Uniforms.Color.Set(Color.White.Vec4());
                            this.RotateCircle.Render();
                        }

                        break;
                    }
                }

                GLState.DepthMask.Set(true);
                GLState.Blend.Set(false);
                GLState.DepthTest.Set(true);
                GLState.CullFace.Set(false);
                OpenGLUtil.EndSection();
            }

            this.CPUTimerGizmos.Stop();
        }

        private void RenderObjectMouseOver(Map m)
        {
            OpenGLUtil.StartSection("Mouse over highlight");
            if (this.ObjectMouseOver != null && Client.Instance.Frontend.Renderer.SelectionManager.BoxSelectCandidates.Count == 0)
            {
                GLState.CullFace.Set(false);
                MapObject mo = this.ObjectMouseOver;
                BBBox cBB = mo.ClientRaycastOOBB;
                Vector3 size = cBB.Size;
                Vector3 cAvg = cBB.Center;
                Matrix4x4 modelMatrix = Matrix4x4.CreateTranslation(cAvg) * Matrix4x4.CreateFromQuaternion(this.ObjectMouseOver.Rotation) * Matrix4x4.CreateTranslation(this.ObjectMouseOver.Position);
                FastAccessShader<HighlightUniforms> shader = this.HighlightShader;
                Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
                shader.Bind();
                shader.Uniforms.Transform.View.Set(cam.View);
                shader.Uniforms.Transform.Projection.Set(cam.Projection);
                shader.Uniforms.Transform.Model.Set(modelMatrix);
                shader.Uniforms.Color.Set(this._mouseOverInFow ? Color.DarkSlateBlue.Vec4() : Color.RoyalBlue.Vec4());
                float mD = Client.Instance.Frontend.UpdatesExisted % 180 / 90.0f;
                mD = mD / 2 % 1 * 2;
                float sineMod = ((MathF.Min(mD, 2 - mD) * 2) - 1) * 0.025f;
                shader.Uniforms.Bounds.Set(size + new Vector3(sineMod));
                this._boxVao.Bind();
                GLState.DrawArrays(PrimitiveType.Triangles, 0, 864);
                GLState.CullFace.Set(true);
                this.PortalHightlightRenderer.AddObject(mo);
            }

            OpenGLUtil.EndSection();
        }

        private void RenderLights(Map m, double delta)
        {
            this.CPUTimerLights.Restart();

            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            plr.Clear();
            Guid selfDarkvision = Guid.Empty;
            float dvLuma = 0;
            if (m.EnableDarkvision)
            {
                if (m.DarkvisionData.TryGetValue(Client.Instance.ID, out (Guid, float) kv))
                {
                    if (m.GetObject(kv.Item1, out MapObject mo))
                    {
                        selfDarkvision = mo.ID;
                        dvLuma = kv.Item2;
                    }
                }
            }

            foreach (MapObject mo in m.IterateObjects(null))
            {
                if (mo.MapLayer > 0 || (!mo.LightsEnabled && !mo.ID.Equals(selfDarkvision)))
                {
                    continue;
                }

                AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(mo.AssetID, AssetType.Model, out Asset a);
                if (status == AssetStatus.Return && a.ModelGlReady)
                {
                    Matrix4x4 modelMatrix = mo.ClientCachedModelMatrix;
                    plr.ProcessScene(modelMatrix, a.Model.GLMdl, mo);
                    if (mo.ID.Equals(selfDarkvision))
                    {
                        plr.AddLightCandidate(new PointLight(
                            mo.Position + (a?.Model.GLMdl?.CombinedBounds.Center ?? Vector3.Zero),
                            Vector3.One, 0, mo, true, false, new GlbLight(Vector4.One, dvLuma, KhrLight.LightTypeEnum.Point)));
                    }
                }
            }

            plr.DrawLights(m, m.EnableDirectionalShadows && Client.Instance.Settings.EnableDirectionalShadows, delta, cam);
            this.CPUTimerLights.Stop();
        }

        private readonly List<MapObject> _crossedOutObjects = new List<MapObject>();
        private readonly List<MapObject>[] _forwardLists = new List<MapObject>[5] { 
            new List<MapObject>(),
            new List<MapObject>(),
            new List<MapObject>(),
            new List<MapObject>(),
            new List<MapObject>()
        };

        private readonly float[] _alphaForLayerDelta = new float[5] { 
            1.0f,
            0.75f,
            0.5f,
            0.25f,
            0.125f
        };

        private void RenderDeferred(Map m, double delta)
        {
            this.CPUTimerDeferred.Restart();
            GLState.DepthTest.Set(true);
            GLState.DepthFunc.Set(ComparisonMode.LessOrEqual);
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;
            OpenGLUtil.StartSection("Point shadows");
            this.RenderLights(m, delta);
            OpenGLUtil.EndSection();

            OpenGLUtil.StartSection("Deferred pass");
            FastAccessShader<DeferredUniforms> shader = Client.Instance.Frontend.Renderer.Pipeline.BeginDeferred(m);

            this._crossedOutObjects.Clear();
            int maxLayer = Client.Instance.IsAdmin ? 2 : 0;
            for (int i = -2; i <= maxLayer; ++i)
            {
                foreach (MapObject mo in m.IterateObjects(i))
                {
                    AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(mo.AssetID, AssetType.Model, out Asset a);
                    bool assetReady = status == AssetStatus.Return && a.ModelGlReady;
                    if (assetReady)
                    {
                        if (!mo.ClientAssignedModelBounds)
                        {
                            mo.ClientBoundingBox = mo.ClientModelRaycastBox = a.Model.GLMdl.RaycastBounds;
                            mo.ClientAssignedModelBounds = true;
                        }
                    }

                    if (!(mo.ClientRenderedThisFrame = Client.Instance.Frontend.Renderer.MapRenderer.IsMapObjectInFrustum(mo)))
                    {
                        continue;
                    }

                    if (mo.IsCrossedOut)
                    {
                        this._crossedOutObjects.Add(mo);
                    }

                    if (mo.DoNotRender)
                    {
                        continue;
                    }

                    if (assetReady && i <= 0 && !(a.Model.GLMdl.HasTransparency || mo.TintColor.Alpha() < (1.0f - float.Epsilon) || !mo.ShaderID.IsEmpty()))
                    {
                        // Suitable for deferred
                        Matrix4x4 modelMatrix = mo.ClientCachedModelMatrix;
                        float ga = m.GridColor.Vec4().W;
                        shader.Uniforms.Grid.GridAlpha.Set(i == -2 && m.GridEnabled ? ga : 0.0f);
                        shader.Uniforms.Grid.GridType.Set((uint)m.GridType);
                        shader.Uniforms.TintColor.Set(mo.TintColor.Vec4());
                        mo.LastRenderModel = a.Model.GLMdl;
                        a.Model.GLMdl.Render(in shader.Uniforms.glbEssentials, modelMatrix, cam.Projection, cam.View, double.NaN, mo.AnimationContainer.CurrentAnimation, mo.AnimationContainer.GetTime(delta), mo.AnimationContainer);
                    }
                    else
                    {
                        // Rejected to forward pass
                        mo.DeferredAssetObjectThisFrame = a;
                        mo.DeferredAssetReadinessThisFrame = assetReady;
                        mo.DeferredAssetStatusThisFrame = status;
                        mo.CameraDistanceToThisFrameForDeferredRejects = this.GetCameraDistanceTo(mo, cam);
                        this._forwardLists[i + 2].Add(mo);
                    }
                }
            }

            Client.Instance.Frontend.Renderer.Pipeline.EndDeferred(m);
            OpenGLUtil.EndSection();
            this.CPUTimerDeferred.Stop();
            this.CPUTimerMain.Restart();

            OpenGLUtil.StartSection("Forward pass");
            FastAccessShader<ForwardUniforms> forwardShader = Client.Instance.Frontend.Renderer.Pipeline.BeginForward(m, delta);
            FastAccessShader<ForwardUniforms> currentShader = forwardShader;

            GLState.Blend.Set(true);
            GL.EnableIndexed(IndexedCapability.Blend, 0);
            GL.DisableIndexed(IndexedCapability.Blend, 1);
            GL.DisableIndexed(IndexedCapability.Blend, 2);
            GL.EnableIndexed(IndexedCapability.Blend, 3);
            GL.DisableIndexed(IndexedCapability.Blend, 4);
            GL.DisableIndexed(IndexedCapability.Blend, 5);
            GLState.BlendFunc.Set((BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha));
            for (int i = -2; i <= maxLayer; ++i) // Still have to iterate from -2 to max layer for better shader uniforms and correct ordering for layers
            {
                if (this._forwardLists[i + 2].Count == 0)
                {
                    continue;
                }

                currentShader = forwardShader;
                currentShader.Program.Bind();
                if (i <= 0)
                {
                    Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.Uniform(currentShader.Uniforms.FOW);
                }
                else
                {
                    Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.UniformBlank(currentShader.Uniforms.FOW);
                }

                int cLayer = Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer;
                this._passthroughData.Alpha = i > 0 ? this._alphaForLayerDelta[Math.Clamp(i - cLayer, 0, 5)] : 1.0f;
                this._passthroughData.GridAlpha = i == -2 && m.GridEnabled ? 1.0f : 0.0f;
                this._passthroughData.GridType = (uint)m.GridType;
                currentShader.Uniforms.Alpha.Set(this._passthroughData.Alpha);
                currentShader.Uniforms.Grid.GridAlpha.Set(this._passthroughData.GridAlpha);
                currentShader.Uniforms.Grid.GridType.Set(this._passthroughData.GridType);
                this._forwardLists[i + 2].Sort((l, r) => r.CameraDistanceToThisFrameForDeferredRejects.CompareTo(l.CameraDistanceToThisFrameForDeferredRejects));
                foreach (MapObject mo in this._forwardLists[i + 2])
                {
                    Asset a = mo.DeferredAssetObjectThisFrame;
                    bool assetReady = mo.DeferredAssetReadinessThisFrame;
                    Matrix4x4 modelMatrix = mo.ClientCachedModelMatrix;
                    currentShader = forwardShader;
                    this._passthroughData.TintColor = mo.TintColor.Vec4();
                    currentShader.Uniforms.TintColor.Set(this._passthroughData.TintColor);
                    FastAccessShader<ForwardUniforms> customShader = null;
                    bool hadCustomRenderShader = !mo.ShaderID.IsEmpty() && CustomShaderRenderer.Render(mo.ShaderID, m, this._passthroughData, double.NaN, delta, out customShader);
                    if (hadCustomRenderShader)
                    {
                        if (i <= 0)
                        {
                            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.Uniform(customShader.Uniforms.FOW);
                        }
                        else
                        {
                            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.UniformBlank(customShader.Uniforms.FOW);
                        }
                    }

                    GlbScene mdl = (assetReady ? a.Model.GLMdl : this.MissingModel);
                    mo.LastRenderModel = mdl;
                    mdl.Render(in (hadCustomRenderShader ? customShader : currentShader).Uniforms.glbEssentials, modelMatrix, cam.Projection, cam.View, double.NaN, mo.AnimationContainer.CurrentAnimation, mo.AnimationContainer.GetTime(delta), mo.AnimationContainer);
                    if (hadCustomRenderShader)
                    {
                        forwardShader.Program.Bind();
                    }
                }

                this._forwardLists[i + 2].Clear();
            }

            GLState.Blend.Set(false);
            this.CPUTimerMain.Stop();
            OpenGLUtil.EndSection();
            this.FastLightRenderer.Render(m);
            this.Shadow2DRenderer.Render(m);
            this.CPUTimerCompound.Restart();
            OpenGLUtil.StartSection("Combine deferred + forward");
            Client.Instance.Frontend.Renderer.Pipeline.FinishRender(m);
            OpenGLUtil.EndSection();
            this.CPUTimerCompound.Stop();
        }

        public void RenderHighlightBox(MapObject mo, Color c, float extraScale = 1.0f)
        {
            GLState.CullFace.Set(false);
            BBBox cBB = mo.ClientRaycastOOBB;
            Vector3 size = cBB.Size;
            Vector3 cAvg = cBB.Center;
            Matrix4x4 modelMatrix = Matrix4x4.CreateTranslation(cAvg) * Matrix4x4.CreateFromQuaternion(mo.Rotation) * Matrix4x4.CreateTranslation(mo.Position);
            FastAccessShader<HighlightUniforms> shader = this.HighlightShader;
            shader.Bind();
            shader.Uniforms.Transform.Model.Set(modelMatrix);
            shader.Uniforms.Color.Set(c.Vec4());
            shader.Uniforms.Bounds.Set(size * extraScale);
            this._boxVao.Bind();
            GLState.DrawArrays(PrimitiveType.Triangles, 0, 864);
            GLState.CullFace.Set(true);
        }

        public void SetDummyUBO(Camera cam, DirectionalLight sun, Vector4 clearColor, UniformBlockFrameData uniforms)
        {
            if (uniforms != null)
            {
                uniforms.View.Set(cam.View);
                uniforms.Projection.Set(cam.Projection);
                uniforms.Frame.Set((uint)Client.Instance.Frontend.FramesExisted);
                uniforms.Update.Set((uint)Client.Instance.Frontend.UpdatesExisted);
                uniforms.CameraPosition.Set(cam.Position);
                uniforms.CameraDirection.Set(cam.Direction);
                uniforms.SunDirection.Set(sun.Direction.Normalized());
                uniforms.SunColor.Set(sun.Color);
                uniforms.AmbientColor.Set(new Vector3(0.03f));
                uniforms.SunView.Set(Matrix4x4.Identity);
                uniforms.SunProjection.Set(Matrix4x4.Identity);
                uniforms.SkyColor.Set(clearColor.Xyz());
                uniforms.GridColor.Set(Vector4.Zero);
                uniforms.GridScale.Set(1.0f);
                uniforms.CursorPosition.Set(Vector3.Zero);
                uniforms.DarkvisionData.Set(Vector4.Zero);
                uniforms.FrameDT.Set(0f);
                uniforms.ViewportSize.Set(new Vector2(Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Width, Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Height));
            }
            else
            {
                unsafe
                {
                    this.FrameUBOManager.memory->view = cam.View;
                    this.FrameUBOManager.memory->projection = cam.Projection;
                    this.FrameUBOManager.memory->frame = (uint)Client.Instance.Frontend.FramesExisted;
                    this.FrameUBOManager.memory->update = (uint)Client.Instance.Frontend.UpdatesExisted;
                    this.FrameUBOManager.memory->camera_position = new Vector4(cam.Position, 0.0f);
                    this.FrameUBOManager.memory->camera_direction = new Vector4(cam.Direction, 0.0f);
                    this.FrameUBOManager.memory->dl_direction = new Vector4(sun.Direction.Normalized(), 0.0f);
                    this.FrameUBOManager.memory->dl_color = new Vector4(sun.Color, 0.0f);
                    this.FrameUBOManager.memory->al_color = new Vector4(0.03f, 0.03f, 0.03f, 0.0f);
                    this.FrameUBOManager.memory->sun_view = Matrix4x4.Identity;
                    this.FrameUBOManager.memory->sun_projection = Matrix4x4.Identity;
                    this.FrameUBOManager.memory->sky_color = clearColor;
                    this.FrameUBOManager.memory->grid_color = Vector4.Zero;
                    this.FrameUBOManager.memory->grid_size = 1.0f;
                    this.FrameUBOManager.memory->cursor_position = Vector4.Zero;
                    this.FrameUBOManager.memory->dv_data = Vector4.Zero;
                    this.FrameUBOManager.memory->viewport_size = new Vector2(Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Width, Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Height);
                    this.FrameUBOManager.memory->frame_delta = 0f;
                }

                this.FrameUBOManager.Upload();
            }
        }

        private void UpdateUBO(Map m, double delta)
        {
            this.CPUTimerUBOUpdate.Restart();
            if (Client.Instance.Settings.UseUBO)
            {
                Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;

                unsafe
                {
                    this.FrameUBOManager.memory->view = cam.View;
                    this.FrameUBOManager.memory->projection = cam.Projection;
                    this.FrameUBOManager.memory->frame = (uint)Client.Instance.Frontend.FramesExisted;
                    this.FrameUBOManager.memory->update = (uint)Client.Instance.Frontend.UpdatesExisted;
                    this.FrameUBOManager.memory->camera_position = new Vector4(cam.Position, 0);
                    this.FrameUBOManager.memory->camera_direction = new Vector4(cam.Direction, 0);
                    this.FrameUBOManager.memory->dl_direction = new Vector4(this._cachedSunDir, 1.0f);
                    this.FrameUBOManager.memory->dl_color = this._cachedSunColor.Vec4() * m.SunIntensity;
                    this.FrameUBOManager.memory->al_color = new Vector4(this._cachedAmbientColor, 1.0f) * m.AmbientIntensity;
                    this.FrameUBOManager.memory->sun_view = this.DirectionalLightRenderer.SunView;
                    this.FrameUBOManager.memory->sun_projection = this.DirectionalLightRenderer.SunProjection;
                    this.FrameUBOManager.memory->sky_color = this._cachedSkyColor.Vec4();
                    this.FrameUBOManager.memory->grid_color = m.GridColor.Vec4();
                    this.FrameUBOManager.memory->grid_size = m.GridSize;
                    this.FrameUBOManager.memory->cursor_position = new Vector4(Client.Instance.Frontend.Renderer.MapRenderer.TerrainHit ?? Vector3.Zero, 1.0f);
                    this.FrameUBOManager.memory->dv_data = Vector4.Zero;
                    this.FrameUBOManager.memory->frame_delta = (float)delta;
                    this.FrameUBOManager.memory->viewport_size = new Vector2(Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Width, Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Height);

                    if (m.EnableDarkvision)
                    {
                        if (m.DarkvisionData.TryGetValue(Client.Instance.ID, out (Guid, float) kv))
                        {
                            if (m.GetObject(kv.Item1, out MapObject mo))
                            {
                                this.FrameUBOManager.memory->dv_data = new Vector4(mo.Position, kv.Item2 / m.GridUnit);
                            }
                        }
                    }
                }

                this.FrameUBOManager.Upload();
            }

            this.CPUTimerUBOUpdate.Stop();
        }

        public void UniformMainShaderData(Map m, FastAccessShader<ForwardUniforms> shader, double delta)
        {
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;
            if (!Client.Instance.Settings.UseUBO) // If UBOs are used all uniforms are in the UBO
            {
                UniformBlockFrameData uniforms = shader.Uniforms.FrameData;
                uniforms.View.Set(cam.View);
                uniforms.Projection.Set(cam.Projection);
                uniforms.Frame.Set((uint)Client.Instance.Frontend.FramesExisted);
                uniforms.Update.Set((uint)Client.Instance.Frontend.UpdatesExisted);
                uniforms.CameraPosition.Set(cam.Position);
                uniforms.CameraDirection.Set(cam.Direction);
                uniforms.SunDirection.Set(this._cachedSunDir);
                uniforms.SunColor.Set(this._cachedSunColor.Vec3() * m.SunIntensity);
                uniforms.AmbientColor.Set(this._cachedAmbientColor * m.AmbientIntensity);
                uniforms.SunView.Set(this.DirectionalLightRenderer.SunView);
                uniforms.SunProjection.Set(this.DirectionalLightRenderer.SunProjection);
                uniforms.SkyColor.Set(this._cachedSkyColor.Vec3());
                uniforms.GridColor.Set(m.GridColor.Vec4());
                uniforms.GridScale.Set(m.GridSize);
                uniforms.CursorPosition.Set(Client.Instance.Frontend.Renderer.MapRenderer.TerrainHit ?? Vector3.Zero);
                uniforms.DarkvisionData.Set(Vector4.Zero);
                uniforms.FrameDT.Set((float)delta);
                uniforms.ViewportSize.Set(new Vector2(Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Width, Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Height));
                if (m.EnableDarkvision)
                {
                    if (m.DarkvisionData.TryGetValue(Client.Instance.ID, out (Guid, float) kv))
                    {
                        if (m.GetObject(kv.Item1, out MapObject mo))
                        {
                            uniforms.DarkvisionData.Set(new Vector4(mo.Position, kv.Item2));
                        }
                    }
                }
            }

            shader.Uniforms.Gamma.EnableCorrection.Set(true);
            shader.Uniforms.Gamma.Factor.Set(Client.Instance.Settings.Gamma);
            plr.UniformLights(shader.Uniforms.PointLights);
            Client.Instance.Frontend.Renderer.SkyRenderer.SkyboxRenderer.UniformShader(shader.Uniforms.Skybox, m);
        }

        public Vector3 CachedSkyColor => this._cachedSkyColor.Vec3();
        public Vector3 CachedSunDirection => this._cachedSunDir;
        public Vector3 CachedSunColor => this._cachedSunColor.Vec3();
        public Vector3 CachedAmbientColor => this._cachedAmbientColor;

        private readonly List<MapObject> _auraCollection = new List<MapObject>();
        private readonly List<(float, Color)> _auraL = new List<(float, Color)>();
        private void RenderAuras(Map m)
        {
            this.CPUTimerAuras.Restart();
            OpenGLUtil.StartSection("Auras");

            this._auraCollection.Clear();
            foreach (MapObject mo in m.IterateObjects(null))
            {
                if (mo.Auras.Count > 0 && (mo.MapLayer <= 0 || Client.Instance.IsAdmin))
                {
                    this._auraCollection.Add(mo);
                }
            }

            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            if (m.Is2D)
            {
                this._auraCollection.Sort((l, r) => l.Auras.Max(x => x.Item1).CompareTo(r.Auras.Max(x => x.Item1)));
            }
            else
            {
                this._auraCollection.Sort((l, r) => (r.Position - cam.Position).LengthSquared().CompareTo((l.Position - cam.Position).LengthSquared()));
            }

            GLState.Multisample.Set(false);
            GLState.Blend.Set(true);
            GLState.CullFace.Set(true);
            GLState.DepthMask.Set(false);
            GLState.DepthTest.Set(true);
            GLState.BlendFunc.Set((BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha));
            FastAccessShader<FOWDependentOverlayUniforms> shader = this.OverlayShader;
            shader.Bind();
            shader.Uniforms.Transform.View.Set(cam.View);
            shader.Uniforms.Transform.Projection.Set(cam.Projection);
            foreach (MapObject mo in this._auraCollection)
            {
                lock (mo.Lock)
                {
                    this._auraL.Clear();
                    this._auraL.AddRange(mo.Auras);
                }

                bool halfAura = Client.Instance.Settings.ComprehensiveAuras && !Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Contains(mo) && this.ObjectMouseOver != mo;
                this._auraL.Sort((l, r) => l.Item1.CompareTo(r.Item1));
                foreach ((float, Color) aData in this._auraL)
                {
                    Matrix4x4 model = Matrix4x4.CreateScale(aData.Item1 * 2.0f / m.GridUnit) * Matrix4x4.CreateTranslation(mo.Position);
                    shader.Uniforms.Transform.Model.Set(model);
                    shader.Uniforms.Color.Set(aData.Item2.Vec4() * new Vector4(1, 1, 1, 0.5f * (halfAura ? Client.Instance.Settings.ComprehensiveAuraAlphaMultiplier : 1.0f)));
                    GLState.CullFaceMode.Set(PolygonFaceMode.Front);
                    this.AuraSphere.Render();
                    GLState.CullFaceMode.Set(PolygonFaceMode.Back);
                    this.AuraSphere.Render();
                }
            }

            GLState.DepthMask.Set(true);
            GLState.DepthTest.Set(false);
            GLState.Blend.Set(false);
            GLState.CullFace.Set(false);
            GLState.Multisample.Set(true);

            this.CPUTimerAuras.Stop();
            OpenGLUtil.EndSection();
        }

        private float GetCameraDistanceTo(MapObject mo, Camera cam) => mo.Container.Is2D ? mo.Container.Camera2DHeight - mo.Position.Z : Vector3.Distance(mo.Position, cam.Position);
    }

    public enum EditMode
    {
        Select,
        Translate,
        Rotate,
        Scale,
        FOW,
        Measure,
        Draw,
        FX,
        Shadows2D
    }

    public enum TranslationMode
    {
        Gizmo,
        Path,
        Arrows
    }

    [StructLayout(LayoutKind.Explicit, Size = 432, Pack = 1)]
    public unsafe struct FrameUBO
    {
        [FieldOffset(0)] public Matrix4x4 view;
        [FieldOffset(64)] public Matrix4x4 projection;
        [FieldOffset(128)] public Matrix4x4 sun_view;
        [FieldOffset(192)] public Matrix4x4 sun_projection;
        [FieldOffset(256)] public Vector4 camera_position;
        [FieldOffset(272)] public Vector4 camera_direction;
        [FieldOffset(288)] public Vector4 dl_direction;
        [FieldOffset(304)] public Vector4 dl_color;
        [FieldOffset(320)] public Vector4 al_color;
        [FieldOffset(336)] public Vector4 sky_color;
        [FieldOffset(352)] public Vector4 cursor_position;
        [FieldOffset(368)] public Vector4 grid_color;
        [FieldOffset(384)] public Vector4 dv_data;
        [FieldOffset(400)] public uint frame;
        [FieldOffset(404)] public uint update;
        [FieldOffset(408)] public float grid_size;
        [FieldOffset(412)] public float frame_delta;
        [FieldOffset(416)] public Vector2 viewport_size;
        [FieldOffset(424)] public int _paddingA;
        [FieldOffset(428)] public int _paddingB;
    }

    public unsafe class FrameUBOManager
    {
        public FrameUBO* memory;

        private readonly GPUBuffer _ubo;

        public FrameUBOManager()
        {
            this.memory = MemoryHelper.Allocate<FrameUBO>(1);
            this._ubo = new GPUBuffer(BufferTarget.Uniform, BufferUsage.StreamDraw);
            this._ubo.Bind();
            this._ubo.SetData(IntPtr.Zero, 432);
            GL.BindBuffer(BufferTarget.Uniform, 0);
            GL.BindBufferBase(BaseBufferTarget.UniformBuffer, 1, this._ubo);
        }

        public void Dispose()
        {
            MemoryHelper.Free(this.memory);
            this._ubo.Dispose();
        }

        public unsafe void Upload()
        {
            this._ubo.Bind();
            this._ubo.SetSubData((IntPtr)this.memory, 432, 0);
            GL.BindBuffer(BufferTarget.Uniform, 0);
        }
    }

    public class BonesUBO
    {
        private readonly GPUBuffer _ubo;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Readonly on a pointer carries ambiguous meaning - the field itself is never modified, but the values at the pointer are.")]
        private unsafe Matrix4x4* _matrixArray;

        public BonesUBO()
        {
            this._ubo = new GPUBuffer(BufferTarget.Uniform, BufferUsage.StreamDraw);
            this._ubo.Bind();
            this._ubo.SetData(IntPtr.Zero, sizeof(float) * 4 * 4 * 256);
            GL.BindBuffer(BufferTarget.Uniform, 0);
            GL.BindBufferBase(BaseBufferTarget.UniformBuffer, 2, this._ubo);
            unsafe
            {
                this._matrixArray = MemoryHelper.Allocate<Matrix4x4>(256);
            }
        }

        public unsafe void LoadAll(IAnimationStorage armature)
        {
            this._ubo.Bind();
            IEnumerable<IAnimationStorage.BoneData> bones = armature.ProvideBones();
            int i = 0;
            foreach (IAnimationStorage.BoneData bone in bones)
            {
                this._matrixArray[i++] = bone.Transform;
            }

            this._ubo.SetSubData((IntPtr)this._matrixArray, sizeof(Matrix4x4) * i, 0);
        }

        public unsafe void LoadAll(GlbArmature armature)
        {
            this._ubo.Bind();
            
            for (int i = 0; i < armature.UnsortedBones.Count; ++i)
            {
                GlbBone bone = armature.UnsortedBones[i];
                this._matrixArray[i] = bone.Transform;
            }

            this._ubo.SetSubData((IntPtr)this._matrixArray, sizeof(Matrix4x4) * armature.UnsortedBones.Count, 0);
        }
    }
}
