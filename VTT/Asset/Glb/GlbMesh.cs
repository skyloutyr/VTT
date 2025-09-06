namespace VTT.Asset.Glb
{
    using System;
    using System.Numerics;
    using VTT.Control;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Render;
    using VTT.Render.LightShadow;
    using VTT.Render.Shaders;
    using VTT.Util;

    public unsafe class GlbMesh
    {
        private VertexArray _vao;
        private GPUBuffer _vbo;
        private GPUBuffer _ebo;

        private VertexArray _shadowVao;
        private GPUBuffer _shadowVbo;

        public UnsafeResizeableArray<Vector3> simplifiedTriangles;
        public UnsafeResizeableArray<BoneData> boneData;
        public UnsafeResizeableArray<float> areaSums;

        public BoundingVolumeHierarchy BoundingVolumeHierarchy { get; set; }
        public int AmountToRender { get; set; }
        public float[] VertexBuffer { get; set; }
        public float[] ShadowVertexBuffer { get; set; }
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
#if USE_VTX_COMPRESSION
            this._vao.SetVertexSize<float>(4 + 4 + 4);
            this._vao.PushElement(ElementType.Vec4);
            this._vao.PushElement(ElementType.Vec4);
            this._vao.PushElement(ElementType.Vec4);
#else
            this._vao.SetVertexSize<float>(3 + 2 + 3 + 3 + 3 + 4 + 4 + 2);
            this._vao.PushElement(ElementType.Vec3);
            this._vao.PushElement(ElementType.Vec2);
            this._vao.PushElement(ElementType.Vec3);
            this._vao.PushElement(ElementType.Vec3);
            this._vao.PushElement(ElementType.Vec3);
            this._vao.PushElement(ElementType.Vec4);
            this._vao.PushElement(ElementType.Vec4);
            this._vao.PushElement(ElementType.Vec2);
#endif

            this._shadowVao = new VertexArray();
            this._shadowVbo = new GPUBuffer(BufferTarget.Array);
            this._shadowVao.Bind();
            this._shadowVbo.Bind();
            this._shadowVbo.SetData(this.ShadowVertexBuffer);
            this._ebo.Bind(); // EBO already has data here and is in theory already bound, so this call should be meaningless, but if it is not present the shadow vao sometimes doesn't have an associated EBO on some old drivers
            this._shadowVao.Reset();
            this._shadowVao.SetVertexSize<float>(3 + 4 + 2);
            this._shadowVao.PushElement(ElementType.Vec3);
            this._shadowVao.PushElement(ElementType.Vec4);
            this._shadowVao.PushElement(ElementType.Vec2);

            this.VertexBuffer = null;
            this.ShadowVertexBuffer = null;
            this.IndexBuffer = null;
        }

        public void Render(in GLBRendererUniformCollection uniforms, Matrix4x4 model, Matrix4x4 projection, Matrix4x4 view, double textureAnimationIndex, GlbAnimation animation, float modelAnimationTime, IAnimationStorage animationStorage, Action<GlbMesh> renderer = null)
        {
            // Assume that shader already has base uniforms setup
            uniforms.Model.Set(model);
            if (uniforms.MVP.IsValid) // Check here to avoid matrix multiplication when we don't need to set the uniform
            {
                uniforms.MVP.Set(model * view * projection);
            }

            if (this.IsAnimated && animation != null && this.AnimationArmature != null)
            {
                this.AnimationArmature.CalculateAllTransforms(animation, modelAnimationTime, animationStorage);
                if (animationStorage != null)
                {
                    Client.Instance.Frontend.Renderer.ObjectRenderer.BonesUBO.LoadAll(animationStorage);
                }
                else
                {
                    Client.Instance.Frontend.Renderer.ObjectRenderer.BonesUBO.LoadAll(this.AnimationArmature);
                }

                uniforms.IsAnimated.Set(true);
            }
            else
            {
                uniforms.IsAnimated.Set(false);
            }

            this.Material.Uniform(textureAnimationIndex);
            if (SunShadowRenderer.ShadowPass)
            {
                this._shadowVao.Bind();
            }
            else
            {
                this._vao.Bind();
            }

            if (renderer == null)
            {
                GLState.DrawElements(PrimitiveType.Triangles, this.AmountToRender, ElementsType.UnsignedInt, IntPtr.Zero);
            }
            else
            {
                renderer(this);
            }
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
            this._vao.Dispose();
            this._shadowVao.Dispose();
            this._vbo.Dispose();
            this._shadowVbo.Dispose();
            this._ebo.Dispose();
            this.BoundingVolumeHierarchy?.Free();
            this.BoundingVolumeHierarchy = null;
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
