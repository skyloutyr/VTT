namespace VTT.Render
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public class FOWRenderer
    {
        public Texture FOWTexture { get; set; }
        public Vector2 FOWOffset { get; set; }
        public Vector2 FOWWorldSize
        {
            get => this.fOWWorldSize;
            set
            {
                this.fOWWorldSize = value;
                this._inverseFowScale = new Vector2(1.0f / value.X, 1.0f / value.Y);
            }
        }
        public bool HasFOW { get; set; }

        public RevealMode CanvasMode { get; set; } = RevealMode.Reveal;
        public SelectionMode PaintMode { get; set; } = SelectionMode.Box;
        public List<Vector3> FowSelectionPoints { get; } = new List<Vector3>();
        public float BrushSize { get; set; } = 1f;

        private VertexArray _vao;
        private GPUBuffer _vbo;
        private GPUBuffer _ebo;
        private float[] _data;
        private int _numVertices;
        private Vector2 _inverseFowScale = Vector2.One;

        public void Create()
        {
            this._vao = new VertexArray();
            this._vbo = new GPUBuffer(BufferTarget.ArrayBuffer, BufferUsageHint.StreamDraw);
            this._ebo = new GPUBuffer(BufferTarget.ElementArrayBuffer, BufferUsageHint.StreamDraw);
            this._vao.Bind();
            this._vbo.Bind();
            this._data = new float[3];
            this._ebo.Bind();
            this._ebo.SetData<uint>(IntPtr.Zero, 4);
            this._vao.Reset();
            this._vao.SetVertexSize<float>(3);
            this._vao.PushElement(ElementType.Vec3);
            this._originalBrushPolygon = new Vector2[] {
                new Vector2(0, 1),
                new Vector2(-0.707107f, 0.707107f),
                new Vector2(-1, 0),
                new Vector2(-0.707107f, -0.707107f),
                new Vector2(0, -1),
                new Vector2(0.707107f, -0.707107f),
                new Vector2(1, 0),
                new Vector2(0.707107f, 0.707107f)
            };

            this._brushRenderData.Add(new Vector3(0, 0, 0));
            this._brushRenderData.AddRange(this._originalBrushPolygon.Select(v => new Vector3(v.X, v.Y, 0.0f)));
            for (uint i = 0; i < 9; ++i)
            {
                this._brushRenderIndices.Add(i);
            }

            this._brushRenderIndices.Add(1);
        }

        public void UploadData(List<Vector3> data, List<uint> indices)
        {
            int size = data.Count * 3;
            if (this._data.Length < size)
            {
                Array.Resize(ref this._data, size);
            }

            for (int i = 0; i < data.Count; i++)
            {
                Vector3 vec = data[i];
                this._data[(i * 3) + 0] = vec.X;
                this._data[(i * 3) + 1] = vec.Y;
                this._data[(i * 3) + 2] = vec.Z;
            }

            this._vao.Bind();
            this._vbo.Bind();
            this._vbo.SetData<float>(IntPtr.Zero, this._data.Length);
            this._vbo.SetSubData(this._data, size, 0);
            this._ebo.Bind();
            this._ebo.SetData<uint>(IntPtr.Zero, indices.Count);
            this._ebo.SetSubData(indices.ToArray(), indices.Count, 0);
            this._numVertices = indices.Count;
        }

        public void DeleteFOW()
        {
            this.FOWTexture?.Dispose();
            this.FOWTexture = new Texture(TextureTarget.Texture2D);
            using Image<Rgba64> img = new Image<Rgba64>(1, 1, new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));
            this.FOWTexture.Bind();
            this.FOWTexture.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this.FOWTexture.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            this.FOWTexture.SetImage(img, PixelInternalFormat.Rgba16ui, 0, PixelType.UnsignedShort);
            this.FOWWorldSize = new Vector2(1, 1);
            this.FOWOffset = new Vector2(-0.5f, -0.5f);
            this.HasFOW = false;
        }

        public void UploadFOW(Vector2 fowSize, Image<Rgba64> texture)
        {
            if (fowSize.Equals(this.FOWWorldSize)) // Update
            {
                this.FOWTexture.Bind();
                this.FOWTexture.SetImage(texture, PixelInternalFormat.Rgba16ui, 0, PixelType.UnsignedShort);
            }
            else
            {
                this.FOWTexture?.Dispose();
                this.FOWTexture = new Texture(TextureTarget.Texture2D);
                this.FOWTexture.Bind();
                this.FOWTexture.SetImage(texture, PixelInternalFormat.Rgba16ui, 0, PixelType.UnsignedShort);
                this.FOWTexture.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
                this.FOWTexture.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
                this.FOWWorldSize = fowSize;
                this.FOWOffset = fowSize / 2f;
                this.FOWOffset = new Vector2(0.5f) + new Vector2(MathF.Floor(this.FOWOffset.X), MathF.Floor(this.FOWOffset.Y));
            }

            this.HasFOW = true;
            texture.Dispose();
        }

        public void Uniform(ShaderProgram shader)
        {
            GL.ActiveTexture(TextureUnit.Texture15);
            this.FOWTexture.Bind();
            shader["fow_texture"].Set(15);
            shader["fow_offset"].Set(this.FOWOffset);
            shader["fow_scale"].Set(this._inverseFowScale);
            shader["fow_mod"].Set(Client.Instance.IsAdmin ? Client.Instance.Settings.FOWAdmin : 1.0f);
        }

        public void UniformBlank(ShaderProgram shader)
        {
            GL.ActiveTexture(TextureUnit.Texture15);
            Client.Instance.Frontend.Renderer.White.Bind();
            shader["fow_texture"].Set(15);
            shader["fow_offset"].Set(Vector2.Zero);
            shader["fow_scale"].Set(Vector2.One);
            shader["fow_mod"].Set(0f);
        }

        private bool _lmbPressed;
        private bool _isDrawingBox;
        private bool _isDrawingPoly;
        private bool _enterPressed;
        private bool _escapePressed;
        private Vector3 _initialWorldXYZ;
        private readonly List<uint> _indicesList = new List<uint>();
        private Vector2 fOWWorldSize = new Vector2(256, 256);

        private Vector2 _brushLastXYZ;
        private Vector2[] _originalBrushPolygon = new Vector2[8];
        private readonly Vector2[] _brushPolygon = new Vector2[8];
        private long _lastBrushRequest;

        private readonly List<Vector3> _brushRenderData = new List<Vector3>();
        private readonly List<uint> _brushRenderIndices = new List<uint>();

        public void Render(double time)
        {
            if (Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode != EditMode.FOW)
            {
                this.FowSelectionPoints.Clear();
                this._isDrawingBox = this._isDrawingPoly = false;
                return;
            }

            if (!this._lmbPressed && Client.Instance.Frontend.GameHandle.IsMouseButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left) && !ImGuiNET.ImGui.GetIO().WantCaptureMouse)
            {
                this._lmbPressed = true;
                if (this.PaintMode == SelectionMode.Box)
                {
                    Vector3? v = Client.Instance.Frontend.Renderer.RulerRenderer.TerrainHit ?? Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld;
                    if (v.HasValue)
                    {
                        this._initialWorldXYZ = v.Value;
                        this._isDrawingBox = true;
                        this.FowSelectionPoints.Clear();
                        this.FowSelectionPoints.Add(this._initialWorldXYZ);
                        this.FowSelectionPoints.Add(this._initialWorldXYZ);
                        this.FowSelectionPoints.Add(this._initialWorldXYZ);
                        this.FowSelectionPoints.Add(this._initialWorldXYZ);
                        this._indicesList.Clear();
                        this._indicesList.Add(0);
                        this._indicesList.Add(1);
                        this._indicesList.Add(2);
                        this._indicesList.Add(0);
                        this._indicesList.Add(2);
                        this._indicesList.Add(3);
                        this.UploadData(this.FowSelectionPoints, this._indicesList);
                    }
                }
                else
                {
                    if (this.PaintMode == SelectionMode.Polygon)
                    {
                        if (!this._isDrawingPoly)
                        {
                            this._isDrawingPoly = true;
                            this.FowSelectionPoints.Clear();
                        }

                        Vector3? v = Client.Instance.Frontend.Renderer.RulerRenderer.TerrainHit ?? Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld;
                        if (v.HasValue)
                        {
                            this.FowSelectionPoints.Add(v.Value);
                            if (this.FowSelectionPoints.Count >= 3)
                            {
                                List<Vector2> pos = new List<Vector2>();
                                Triangulate.Process(this.FowSelectionPoints.Select(v => v.Xy).ToArray(), pos);
                                List<Vector3> v3p = new List<Vector3>(pos.Select(v => new Vector3(v.X, v.Y, 0.0f)));
                                this._indicesList.Clear();
                                for (int i = 0; i < v3p.Count; ++i)
                                {
                                    this._indicesList.Add((uint)i);
                                }

                                this.UploadData(v3p, this._indicesList);
                            }
                        }
                    }
                }
            }

            if (this._isDrawingBox && this._lmbPressed)
            {
                Vector3? v = Client.Instance.Frontend.Renderer.RulerRenderer.TerrainHit ?? Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld;
                if (v.HasValue)
                {
                    Vector3 tl = this._initialWorldXYZ;
                    Vector3 br = v.Value;
                    Vector3 tr = new Vector3(br.X, tl.Y, tl.Z);
                    Vector3 bl = new Vector3(tl.X, br.Y, tl.Z);
                    this.FowSelectionPoints[0] = tl;
                    this.FowSelectionPoints[1] = tr;
                    this.FowSelectionPoints[2] = br;
                    this.FowSelectionPoints[3] = bl;
                    this._indicesList[0] = 0;
                    this._indicesList[1] = 1;
                    this._indicesList[2] = 2;
                    this._indicesList[3] = 0;
                    this._indicesList[4] = 2;
                    this._indicesList[5] = 3;
                    this.UploadData(this.FowSelectionPoints, this._indicesList);

                    GL.Disable(EnableCap.DepthTest);
                    GL.Disable(EnableCap.CullFace);
                    GL.Enable(EnableCap.Blend);
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                    ShaderProgram shader = Client.Instance.Frontend.Renderer.ObjectRenderer.OverlayShader;
                    Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
                    shader.Bind();
                    shader["view"].Set(cam.View);
                    shader["projection"].Set(cam.Projection);
                    shader["model"].Set(Matrix4.Identity);
                    shader["u_color"].Set(Color.RoyalBlue.Vec4() * new Vector4(1, 1, 1, 0.35f));
                    this._vao.Bind();
                    GL.DrawElements(PrimitiveType.Triangles, this._numVertices, DrawElementsType.UnsignedInt, IntPtr.Zero);

                    GL.Disable(EnableCap.Blend);
                    GL.Enable(EnableCap.CullFace);
                    GL.Enable(EnableCap.DepthTest);
                }
            }

            if (this._isDrawingPoly)
            {
                if (this.PaintMode != SelectionMode.Polygon)
                {
                    this.FowSelectionPoints.Clear();
                    this._isDrawingPoly = false;
                }
                else
                {
                    if (this.FowSelectionPoints.Count >= 3)
                    {
                        GL.Disable(EnableCap.DepthTest);
                        GL.Disable(EnableCap.CullFace);
                        GL.Enable(EnableCap.Blend);
                        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                        ShaderProgram shader = Client.Instance.Frontend.Renderer.ObjectRenderer.OverlayShader;
                        Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
                        shader.Bind();
                        shader["view"].Set(cam.View);
                        shader["projection"].Set(cam.Projection);
                        shader["model"].Set(Matrix4.Identity);
                        shader["u_color"].Set(Color.RoyalBlue.Vec4() * new Vector4(1, 1, 1, 0.35f));
                        this._vao.Bind();

                        GL.DrawElements(PrimitiveType.Triangles, this._numVertices, DrawElementsType.UnsignedInt, IntPtr.Zero);
                        GL.Disable(EnableCap.Blend);
                        GL.Enable(EnableCap.CullFace);
                        GL.Enable(EnableCap.DepthTest);
                    }
                }
            }

            if (this.PaintMode == SelectionMode.Brush)
            {
                Vector3? v = Client.Instance.Frontend.Renderer.RulerRenderer.TerrainHit ?? Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld;
                if (v.HasValue)
                {
                    this.UploadData(this._brushRenderData, this._brushRenderIndices);

                    GL.Disable(EnableCap.DepthTest);
                    GL.Disable(EnableCap.CullFace);
                    GL.Enable(EnableCap.Blend);
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                    ShaderProgram shader = Client.Instance.Frontend.Renderer.ObjectRenderer.OverlayShader;
                    Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
                    shader.Bind();
                    shader["view"].Set(cam.View);
                    shader["projection"].Set(cam.Projection);
                    shader["model"].Set(Matrix4.CreateScale(this.BrushSize) * Matrix4.CreateTranslation(v.Value));
                    shader["u_color"].Set(Color.RoyalBlue.Vec4() * new Vector4(1, 1, 1, 0.35f));
                    this._vao.Bind();
                    GL.DrawElements(PrimitiveType.TriangleFan, this._numVertices, DrawElementsType.UnsignedInt, IntPtr.Zero);

                    GL.Disable(EnableCap.Blend);
                    GL.Enable(EnableCap.CullFace);
                    GL.Enable(EnableCap.DepthTest);
                }
            }

            if (this._lmbPressed && this.PaintMode == SelectionMode.Brush)
            {
                Vector3? v = Client.Instance.Frontend.Renderer.RulerRenderer.TerrainHit ?? Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld;
                if (v.HasValue)
                {
                    Vector2 now = new Vector2(v.Value.X, v.Value.Y);
                    if (!MathHelper.ApproximatelyEquivalent(now.X, this._brushLastXYZ.X, 1e-7f) || !MathHelper.ApproximatelyEquivalent(now.Y, this._brushLastXYZ.Y, 1e-7f))
                    {
                        this._brushLastXYZ = now;
                        long unixNow = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        if (unixNow - this._lastBrushRequest >= 100)
                        {
                            for (int i = 0; i < 8; ++i)
                            {
                                this._brushPolygon[i] = (this._originalBrushPolygon[i] * this.BrushSize) + now;
                            }

                            bool action = this.CanvasMode == RevealMode.Reveal;
                            PacketFOWRequest pfowr = new PacketFOWRequest() { Polygon = new List<Vector2>(this._brushPolygon), RequestType = action };
                            pfowr.Send();
                            this._lastBrushRequest = unixNow;
                        }
                    }
                }
            }

            if (this._lmbPressed && !Client.Instance.Frontend.GameHandle.IsMouseButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left))
            {
                if (this._isDrawingBox)
                {
                    bool action = this.CanvasMode == RevealMode.Reveal;
                    PacketFOWRequest pfowr = new PacketFOWRequest() { Polygon = new List<Vector2>(this.FowSelectionPoints.Select(v => v.Xy)), RequestType = action };
                    pfowr.Send();
                }

                this._lmbPressed = this._isDrawingBox = false;
            }

            if (!this._escapePressed && Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Escape))
            {
                this._escapePressed = true;
                this._lmbPressed = false;
                this._isDrawingBox = this._isDrawingPoly = false;
                this.FowSelectionPoints.Clear();
            }

            if (!this._enterPressed && Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Enter))
            {
                this._enterPressed = true;
                if (this.HasFOW && this.FowSelectionPoints.Count > 2)
                {
                    bool action = this.CanvasMode == RevealMode.Reveal;
                    PacketFOWRequest pfowr = new PacketFOWRequest() { Polygon = new List<Vector2>(this.FowSelectionPoints.Select(v => v.Xy)), RequestType = action };
                    pfowr.Send();
                }

                this._lmbPressed = false;
                this._isDrawingBox = this._isDrawingPoly = false;
                this.FowSelectionPoints.Clear();
            }

            if (this._enterPressed && !Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Enter))
            {
                this._enterPressed = false;
            }

            if (this._escapePressed && !Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Escape))
            {
                this._escapePressed = false;
            }
        }

        public enum RevealMode
        {
            Reveal,
            Hide
        }

        public enum SelectionMode
        {
            Box,
            Polygon,
            Brush
        }
    }
}
