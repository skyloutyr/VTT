namespace VTT.Asset.Glb
{
    using System;
    using System.Numerics;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Util;

    public unsafe class GlbMesh
    {
        private VertexArray _vao;
        private GPUBuffer _vbo;
        private GPUBuffer _ebo;

        public UnsafeResizeableArray<Vector3> simplifiedTriangles;
        public UnsafeResizeableArray<BoneData> boneData;
        public UnsafeResizeableArray<float> areaSums;

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
            this._vbo = new GPUBuffer(BufferTarget.Array);
            this._ebo = new GPUBuffer(BufferTarget.ElementArray);

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

        public void Render(ShaderProgram shader, MatrixStack matrixStack, Matrix4x4 projection, Matrix4x4 view, double textureAnimationIndex, GlbAnimation animation, float modelAnimationTime, Action<GlbMesh> renderer = null)
        {
            // Assume that shader already has base uniforms setup
            Matrix4x4 cm = matrixStack.Current;
            shader["model"].Set(cm);
            shader["mvp"].Set(cm * view * projection);
            if (this.IsAnimated && animation != null && this.AnimationArmature != null)
            {
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
                GL.DrawElements(PrimitiveType.Triangles, this.AmountToRender, ElementsType.UnsignedInt, IntPtr.Zero);
            }
            else
            {
                renderer(this);
            }

            // Reset GL state
            GL.ActiveTexture(0);
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

        public struct BoneData
        {
            public uint index0;
            public uint index1;
            public uint index2;
            public uint index3;

            public float weight1;
            public float weight2;
            public float weight3;
            public float weight4;

            public BoneData(Vector4 weights, Vector2 indices)
            {
                this.weight1 = weights.X;
                this.weight2 = weights.Y;
                this.weight3 = weights.Z;
                this.weight4 = weights.W;

                DecomposeSingle(indices.X, out this.index0, out this.index1);
                DecomposeSingle(indices.Y, out this.index2, out this.index3);
            }

            private static void DecomposeSingle(in float f, out uint us1, out uint us2)
            {
                uint ui = VTTMath.SingleBitsToUInt32(f);
                us1 = (ui >> 16);
                us2 = (ui & ushort.MaxValue);
            }
        }
    }
}
