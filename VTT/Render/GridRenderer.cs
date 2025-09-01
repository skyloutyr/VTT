namespace VTT.Render
{
    using SixLabors.ImageSharp;
    using System;
    using System.Diagnostics;
    using System.Numerics;
    using VTT.Control;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Render.Shaders;
    using VTT.Util;

    public class GridRenderer
    {
        private VertexArray _vao;
        private GPUBuffer _vbo;
        private FastAccessShader<GridUniforms> _shader;
        public FastAccessShader<OverlayUniforms> InWorldShader { get; set; }

        private VertexArray _highlightVao;
        private GPUBuffer _highlightVbo;

        public Stopwatch CPUTimer { get; set; }

        public void Create()
        {
            float[] data = new float[] {
                -100, -100, 0,
                100, -100, 0,
                100, 100, 0,
                -100, 100, 0
            };

            this._vao = new VertexArray();
            this._vbo = new GPUBuffer(BufferTarget.Array);
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
            this._highlightVbo = new GPUBuffer(BufferTarget.Array);
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
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, 180 * MathF.PI / 180),  // Down
                Quaternion.CreateFromAxisAngle(Vector3.UnitX, 90 * MathF.PI / 180),   // Front
                Quaternion.CreateFromAxisAngle(Vector3.UnitX, -90 * MathF.PI / 180),  // Back
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, 90 * MathF.PI / 180),   // Left
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, -90 * MathF.PI / 180)   // Right
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
                    Vector4 v = Vector4.Transform(new Vector4(data[j * 5], data[(j * 5) + 1], data[(j * 5) + 2], 1.0f), q[i]) + new Vector4(o[i], 1.0f);
                    nData[idx + 0] = v.X;
                    nData[idx + 1] = v.Y;
                    nData[idx + 2] = v.Z;
                    nData[idx + 3] = data[(j * 5) + 3];
                    nData[idx + 4] = data[(j * 5) + 4];
                }
            }

            this._shader = new FastAccessShader<GridUniforms>(OpenGLUtil.LoadShader("grid", stackalloc ShaderType[2] { ShaderType.Vertex, ShaderType.Fragment }));
            this.InWorldShader = new FastAccessShader<OverlayUniforms>(OpenGLUtil.LoadShader("overlay", stackalloc ShaderType[2] { ShaderType.Vertex, ShaderType.Fragment }));

            this.CPUTimer = new Stopwatch();

            OpenGLUtil.NameObject(GLObjectType.VertexArray, this._vao, "Grid primary vao");
            OpenGLUtil.NameObject(GLObjectType.Buffer, this._vbo, "Grid primary vbo");
            OpenGLUtil.NameObject(GLObjectType.VertexArray, this._highlightVao, "Grid highlight vao");
            OpenGLUtil.NameObject(GLObjectType.Buffer, this._highlightVbo, "Grid highlight vbo");
        }

        public void Render(double deltaTime, Camera cam, Map m, bool renderMisc = true)
        {
            if (m != null && (!m.GridEnabled || !m.GridDrawn))
            {
                return;
            }

            this.CPUTimer.Restart();
            OpenGLUtil.StartSection("World grid");
            GLState.CullFace.Set(true);
            GLState.CullFaceMode.Set(cam.Position.Z < 0 ? PolygonFaceMode.Front : PolygonFaceMode.Back);
            GLState.Blend.Set(true);
            GLState.BlendFunc.Set((BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha));

            Matrix4x4 modelMatrix = Matrix4x4.CreateTranslation((cam.Position * new Vector3(1, 1, 0)) - new Vector3(0, 0, 0.5f));
            this._shader.Bind();
            this._shader.Uniforms.Transform.View.Set(cam.View);
            this._shader.Uniforms.Transform.Projection.Set(cam.Projection);
            this._shader.Uniforms.Transform.Model.Set(modelMatrix);
            this._shader.Uniforms.CameraPosition.Set(cam.Position);
            this._shader.Uniforms.Grid.Color.Set(m == null ? Color.White.Vec4() : m.GridColor.Vec4());
            this._shader.Uniforms.Grid.GridAlpha.Set(1f);
            this._shader.Uniforms.Grid.Scale.Set(m == null ? 1 : m.GridSize);
            this._shader.Uniforms.Grid.GridType.Set(m == null ? 0u : (uint)m.GridType);
            Vector3? cw = Client.Instance.Frontend.Renderer.MapRenderer.GroundHitscanResult;
            this._shader.Uniforms.CursorPosition.Set(cw == null || !renderMisc ? new Vector3(0, 0, 10000) : cw.Value);
            this._vao.Bind();

            GLState.DepthTest.Set(true);
            GLState.DepthFunc.Set(ComparisonMode.LessOrEqual);
            GLState.DepthMask.Set(false);
            GLState.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
            GLState.DepthMask.Set(true);
            GLState.DepthFunc.Set(ComparisonMode.Less);
            GLState.DepthTest.Set(false);

            if (renderMisc)
            {
                Vector3 cwScale = default;
                if (Client.Instance.Frontend.Renderer.ObjectRenderer.ObjectMouseOver != null)
                {
                    cw = Client.Instance.Frontend.Renderer.ObjectRenderer.ObjectMouseOver.Position * new Vector3(1, 1, 0);
                    cwScale = Client.Instance.Frontend.Renderer.ObjectRenderer.ObjectMouseOver.Scale;
                }

                if (cw.HasValue)
                {
                    GLState.DepthFunc.Set(ComparisonMode.Always);
                    GLState.CullFace.Set(false);
                    Vector3 snapped = cw.Value;
                    modelMatrix = Matrix4x4.CreateScale(MathF.Round(cwScale.X), MathF.Round(cwScale.Y), 1) * Matrix4x4.CreateTranslation(snapped + new Vector3(0, 0, 0.001f));
                    this.InWorldShader.Bind();
                    this.InWorldShader.Uniforms.Transform.View.Set(cam.View);
                    this.InWorldShader.Uniforms.Transform.Projection.Set(cam.Projection);
                    this.InWorldShader.Uniforms.Transform.Model.Set(modelMatrix);
                    this.InWorldShader.Uniforms.Color.Set(Color.RoyalBlue.Vec4());
                    GLState.ActiveTexture.Set(0);
                    Client.Instance.Frontend.Renderer.White.Bind();
                    this._highlightVao.Bind();
                    GLState.DrawArrays(PrimitiveType.Triangles, 0, 24);
                    GLState.CullFace.Set(true);
                    GLState.DepthFunc.Set(ComparisonMode.LessOrEqual);
                }

                if (Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Count > 0)
                {
                    OpenGLUtil.StartSection("Selection highlighs");
                    GLState.DepthFunc.Set(ComparisonMode.Always);
                    GLState.CullFace.Set(false);
                    this.InWorldShader.Bind();
                    this.InWorldShader.Uniforms.Transform.View.Set(cam.View);
                    this.InWorldShader.Uniforms.Transform.Projection.Set(cam.Projection);
                    this.InWorldShader.Uniforms.Color.Set(Color.Orange.Vec4());
                    GLState.ActiveTexture.Set(0);
                    Client.Instance.Frontend.Renderer.White.Bind();
                    float gSize = m == null ? 1 : m.GridSize;

                    foreach (MapObject mo in Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects)
                    {
                        float msx = MathF.Abs(mo.Scale.X) % (gSize * 2);
                        float msy = MathF.Abs(mo.Scale.Y) % (gSize * 2);
                        float msz = MathF.Abs(mo.Scale.Z) % (gSize * 2);
                        Vector3 bigScale = new Vector3(
                            msx - 0.075f <= 0 || (gSize * 2) - msx <= 0.075f ? 1 : 0,
                            msy - 0.075f <= 0 || (gSize * 2) - msy <= 0.075f ? 1 : 0,
                            msz - 0.075f <= 0 || (gSize * 2) - msz <= 0.075f ? 1 : 0
                        );

                        Vector3 snapped = mo.Position;
                        modelMatrix = Matrix4x4.CreateScale(MathF.Round(mo.Scale.X), MathF.Round(mo.Scale.Y), 1) * Matrix4x4.CreateTranslation(snapped + new Vector3(0, 0, 0.001f) - new Vector3(0, 0, 0.5f));
                        this.InWorldShader.Uniforms.Transform.Model.Set(modelMatrix);
                        this._highlightVao.Bind();
                        GLState.DrawArrays(PrimitiveType.Triangles, 0, 24);
                    }

                    GLState.CullFace.Set(true);
                    GLState.DepthFunc.Set(ComparisonMode.LessOrEqual);
                    OpenGLUtil.EndSection();
                }
            }

            GLState.Blend.Set(false);
            GLState.CullFaceMode.Set(PolygonFaceMode.Back);
            GLState.CullFace.Set(false);
            this.CPUTimer.Stop();
            OpenGLUtil.EndSection();
        }
    }
}
