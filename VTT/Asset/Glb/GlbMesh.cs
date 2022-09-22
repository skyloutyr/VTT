namespace VTT.Asset.Glb
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using System;
    using VTT.GL;
    using VTT.Util;

    public unsafe class GlbMesh
    {
        private VertexArray _vao;
        private GPUBuffer _vbo;
        private GPUBuffer _ebo;

        public System.Numerics.Vector3[] simplifiedTriangles;

        public int AmountToRender { get; set; }
        public float[] VertexBuffer { get; set; }
        public uint[] IndexBuffer { get; set; }
        public GlbMaterial Material { get; set; }

        public AABox Bounds { get; set; }

        public void CreateGl()
        {
            this._vao = new VertexArray();
            this._vbo = new GPUBuffer(BufferTarget.ArrayBuffer);
            this._ebo = new GPUBuffer(BufferTarget.ElementArrayBuffer);

            this._vao.Bind();
            this._vbo.Bind();
            this._vbo.SetData(this.VertexBuffer);
            this._ebo.Bind();
            this._ebo.SetData(this.IndexBuffer);

            this._vao.Reset();
            this._vao.SetVertexSize<float>(3 + 2 + 3 + 3 + 3 + 4);
            this._vao.PushElement(ElementType.Vec3);
            this._vao.PushElement(ElementType.Vec2);
            this._vao.PushElement(ElementType.Vec3);
            this._vao.PushElement(ElementType.Vec3);
            this._vao.PushElement(ElementType.Vec3);
            this._vao.PushElement(ElementType.Vec4);

            this.VertexBuffer = null;
            this.IndexBuffer = null;
        }

        public void Render(ShaderProgram shader, MatrixStack matrixStack, Matrix4 projection, Matrix4 view, double textureAnimationIndex, Action<GlbMesh> renderer = null)
        {
            // Assume that shader already has base uniforms setup
            Matrix4 cm = matrixStack.Current;
            shader["model"].Set(cm);
            shader["mvp"].Set(cm * view * projection);
            this.Material.Uniform(shader, textureAnimationIndex);
            this._vao.Bind();
            if (renderer == null)
            {
                GL.DrawElements(PrimitiveType.Triangles, this.AmountToRender, DrawElementsType.UnsignedInt, IntPtr.Zero);
            }
            else
            {
                renderer(this);
            }

            // Reset GL state
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(this._vao);
            GL.DeleteBuffer(this._vbo);
            GL.DeleteBuffer(this._ebo);
        }
    }
}
