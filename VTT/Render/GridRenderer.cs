namespace VTT.Render
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public class GridRenderer
    {
        private VertexArray _vao;
        private GPUBuffer _vbo;
        private ShaderProgram _shader;
        public ShaderProgram InWorldShader { get; set; }

        private VertexArray _highlightVao;
        private VertexArray _quadGridHighlightVao;
        private GPUBuffer _highlightVbo;
        private GPUBuffer _quadGridHighlightVbo;

        public void Create()
        {
            float[] data = new float[] {
                -100, -100, 0,
                100, -100, 0,
                100, 100, 0,
                -100, 100, 0
            };

            this._vao = new VertexArray();
            this._vbo = new GPUBuffer(BufferTarget.ArrayBuffer);
            this._vao.Bind();
            this._vbo.Bind();
            this._vbo.SetData(data);
            this._vao.Reset();
            this._vao.SetVertexSize<float>(3);
            this._vao.PushElement(ElementType.Vec3);

            float width = 0.05f;
            data = new float[] {
                -0.5f, -0.5f, 0, 0, 0,
                0.5f, -0.5f, 0,  1, 0,
                0.5f, -0.5f + width, 0, 1, width,
                -0.5f, -0.5f, 0, 0, 0,
                0.5f, -0.5f + width, 0, 1, width,
                -0.5f, -0.5f + width, 0, 0,width,

                -0.5f, 0.5f, 0, 0, 1,
                0.5f, 0.5f - width, 0, 1, 1f - width,
                0.5f, 0.5f, 0,  1, 1,
                -0.5f, 0.5f, 0, 0, 1,
                -0.5f, 0.5f - width, 0, 0, 1f - width,
                0.5f, 0.5f - width, 0, 1, 1f - width,

                -0.5f, -0.5f + width, 0, 0, width,
                -0.5f + width, -0.5f + width, 0, width, width,
                -0.5f + width, 0.5f - width, 0, width, 1f - width,
                -0.5f, -0.5f + width, 0, 0, width,
                -0.5f + width, 0.5f - width, 0, width, 1f - width,
                -0.5f, 0.5f - width, 0, 0f, 1f - width,

                0.5f, -0.5f + width, 0, 1, width,
                0.5f - width, 0.5f - width, 0, 1f - width, 1f - width,
                0.5f - width, -0.5f + width, 0, 1f - width, width,
                0.5f, -0.5f + width, 0, 1, width,
                0.5f, 0.5f - width, 0, 1f, 1f - width,
                0.5f - width, 0.5f - width, 0, 1f - width, 1f - width
            };

            this._highlightVao = new VertexArray();
            this._highlightVbo = new GPUBuffer(BufferTarget.ArrayBuffer);
            this._highlightVao.Bind();
            this._highlightVbo.Bind();
            this._highlightVbo.SetData(data);
            this._highlightVao.Reset();
            this._highlightVao.SetVertexSize<float>(5);
            this._highlightVao.PushElement(ElementType.Vec3);
            this._highlightVao.PushElement(ElementType.Vec2);

            float[] nData = new float[data.Length * 6];
            Quaternion[] q = new Quaternion[] { 
                Quaternion.Identity,                                                        // Up
                Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(180)),  // Down
                Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(90)),   // Front
                Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(-90)),  // Back
                Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(90)),   // Left
                Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(-90))   // Right
            };

            Vector3[] o = new Vector3[] {
                Vector3.UnitZ,
                Vector3.Zero,
                new Vector3(0, 0.5f, 0.5f),
                new Vector3(0, -0.5f, 0.5f),
                new Vector3(0.5f, 0, 0.5f),
                new Vector3(-0.5f, 0, 0.5f),
            };

            for (int i = 0; i < 6; ++i)
            {
                for (int j = 0; j < 24; ++j)
                {
                    int idx = (i * data.Length) + (j * 5);
                    Vector4 v = (q[i] * new Vector4(data[j * 5], data[(j * 5) + 1], data[(j * 5) + 2], 1.0f)) + new Vector4(o[i], 1.0f);
                    nData[idx + 0] = v.X;
                    nData[idx + 1] = v.Y;
                    nData[idx + 2] = v.Z;
                    nData[idx + 3] = data[(j * 5) + 3];
                    nData[idx + 4] = data[(j * 5) + 4];
                }
            }

            this._quadGridHighlightVao = new VertexArray();
            this._quadGridHighlightVbo = new GPUBuffer(BufferTarget.ArrayBuffer);
            this._quadGridHighlightVao.Bind();
            this._quadGridHighlightVbo.Bind();
            this._quadGridHighlightVbo.SetData(nData);
            this._quadGridHighlightVao.Reset();
            this._quadGridHighlightVao.SetVertexSize<float>(5);
            this._quadGridHighlightVao.PushElement(ElementType.Vec3);
            this._quadGridHighlightVao.PushElement(ElementType.Vec2);

            this._shader = OpenGLUtil.LoadShader("grid", ShaderType.VertexShader, ShaderType.FragmentShader);
            this.InWorldShader = OpenGLUtil.LoadShader("overlay", ShaderType.VertexShader, ShaderType.FragmentShader);
        }

        public void Render(double deltaTime, Camera cam, Map m, bool renderMisc = true)
        {
            if (m == null || !m.GridEnabled || !m.GridDrawn)
            {
                return;
            }

            GL.Disable(EnableCap.FramebufferSrgb);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(cam.Position.Z < 0 ? CullFaceMode.Front : CullFaceMode.Back);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            Matrix4 modelMatrix = Matrix4.CreateTranslation((cam.Position * new Vector3(1, 1, 0)) - new Vector3(0, 0, 0.5f));
            this._shader.Bind();
            this._shader["view"].Set(cam.View);
            this._shader["projection"].Set(cam.Projection);
            this._shader["model"].Set(modelMatrix);
            this._shader["camera_position"].Set(cam.Position);
            this._shader["g_color"].Set(m.GridColor.Vec4());
            this._shader["g_alpha"].Set(1f);
            this._shader["g_size"].Set(m.GridSize);
            Vector3? cw = Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld;
            this._shader["cursor_position"].Set(cw == null || !renderMisc ? new Vector3(0, 0, 10000) : cw.Value);
            this._vao.Bind();
            GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
            if (renderMisc)
            {
                if (Client.Instance.Frontend.Renderer.ObjectRenderer.ObjectMouseOver != null)
                {
                    cw = Client.Instance.Frontend.Renderer.ObjectRenderer.ObjectMouseOver.Position * new Vector3(1, 1, 0);
                }

                if (cw.HasValue)
                {
                    GL.DepthFunc(DepthFunction.Always);
                    GL.Disable(EnableCap.CullFace);
                    Vector3 snapped = MapRenderer.SnapToGrid(cw.Value, m.GridSize) - new Vector3(0, 0, 0.5f);
                    modelMatrix = Matrix4.CreateTranslation(snapped + new Vector3(0, 0, 0.001f));
                    this.InWorldShader.Bind();
                    this.InWorldShader["view"].Set(cam.View);
                    this.InWorldShader["projection"].Set(cam.Projection);
                    this.InWorldShader["model"].Set(modelMatrix);
                    this.InWorldShader["u_color"].Set(Color.RoyalBlue.Vec4());
                    GL.ActiveTexture(TextureUnit.Texture0);
                    Client.Instance.Frontend.Renderer.White.Bind();
                    this._highlightVao.Bind();
                    GL.DrawArrays(PrimitiveType.Triangles, 0, 24);
                    GL.Enable(EnableCap.CullFace);
                    GL.DepthFunc(DepthFunction.Lequal);
                }

                if (Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Count > 0)
                {
                    GL.DepthFunc(DepthFunction.Always);
                    GL.Disable(EnableCap.CullFace);
                    this.InWorldShader.Bind();
                    this.InWorldShader["view"].Set(cam.View);
                    this.InWorldShader["projection"].Set(cam.Projection);
                    this.InWorldShader["u_color"].Set(Color.Orange.Vec4());
                    GL.ActiveTexture(TextureUnit.Texture0);
                    Client.Instance.Frontend.Renderer.White.Bind();

                    foreach (MapObject mo in Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects)
                    {
                        Vector3 snapped = MapRenderer.SnapToGrid(mo.Position * new Vector3(1, 1, 0), m.GridSize);
                        modelMatrix = Matrix4.CreateTranslation(snapped + new Vector3(0, 0, 0.001f) - new Vector3(0, 0, 0.5f));
                        this.InWorldShader["model"].Set(modelMatrix);
                        this._highlightVao.Bind();
                        GL.DrawArrays(PrimitiveType.Triangles, 0, 24);
                    }

                    GL.Enable(EnableCap.CullFace);
                    GL.DepthFunc(DepthFunction.Lequal);
                }
            }

            GL.Disable(EnableCap.Blend);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.CullFace);
        }
    }
}
