namespace VTT.Asset.Glb
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using System;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public unsafe class GlbMesh
    {
        private VertexArray _vao;
        private GPUBuffer _vbo;
        private GPUBuffer _ebo;

        public System.Numerics.Vector3[] simplifiedTriangles;
        public float[] areaSums;

        public int AmountToRender { get; set; }
        public float[] VertexBuffer { get; set; }
        public uint[] IndexBuffer { get; set; }
        public GlbMaterial Material { get; set; }
        public bool IsAnimated { get; set; }
        public GlbArmature AnimationArmature { get; set; }

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
            this._vao.SetVertexSize<float>(3 + 2 + 3 + 3 + 3 + 4 + 4 + 2);
            this._vao.PushElement(ElementType.Vec3);
            this._vao.PushElement(ElementType.Vec2);
            this._vao.PushElement(ElementType.Vec3);
            this._vao.PushElement(ElementType.Vec3);
            this._vao.PushElement(ElementType.Vec3);
            this._vao.PushElement(ElementType.Vec4);
            this._vao.PushElement(ElementType.Vec4);
            this._vao.PushElement(ElementType.Vec2);

            this.VertexBuffer = null;
            this.IndexBuffer = null;
        }

        public void Render(ShaderProgram shader, MatrixStack matrixStack, Matrix4 projection, Matrix4 view, double textureAnimationIndex, GlbAnimation animation, float modelAnimationTime, Action<GlbMesh> renderer = null)
        {
            // Assume that shader already has base uniforms setup
            Matrix4 cm = matrixStack.Current;
            shader["model"].Set(cm);
            shader["mvp"].Set(cm * view * projection);
            if (this.IsAnimated && animation != null && this.AnimationArmature != null)
            {
                this.AnimationArmature.ResetAllBones();
                this.AnimationArmature.CalculateAllTransforms(animation, modelAnimationTime);
                Client.Instance.Frontend.Renderer.ObjectRenderer.BonesUBOManager.LoadAll(this.AnimationArmature);
                shader["is_animated"].Set(true);
            }
            else
            {
                shader["is_animated"].Set(false);
            }

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

        public int FindAreaSumIndex(float aSumValue)
        {
            int l = 0;
            int r = this.areaSums.Length - 1;
            while (l <= r)
            {
                int m = (int)Math.Floor((l + r) * 0.5f);
                float cSum = this.areaSums[m];
                float pSum = m == 0 ? 0 : this.areaSums[m - 1];
                if (cSum < aSumValue)
                {
                    l = m + 1;
                }
                else if (pSum > aSumValue)
                {
                    r = m - 1;
                }
                else
                {
                    return m;
                }
            }

            return 0;
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(this._vao);
            GL.DeleteBuffer(this._vbo);
            GL.DeleteBuffer(this._ebo);
        }
    }
}
