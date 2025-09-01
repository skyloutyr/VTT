namespace VTT.Asset.Glb
{
    using glTFLoader.Schema;
    using SixLabors.ImageSharp;
    using System;
    using System.Numerics;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Render;
    using VTT.Render.LightShadow;
    using VTT.Render.Shaders;
    using VTT.Render.Shaders.UBO;
    using VTT.Util;

    public class GlbMaterial
    {
        public string Name { get; set; }

        private uint _selfIndex;
        private Vector4 _baseColor;
        private float _metalness;
        private float _roughness;
        private bool _coreParamsChanged;

        public uint Index
        {
            get => this._selfIndex;
            set
            {
                this._selfIndex = value;
                this._coreParamsChanged = true;
            }
        }

        public Vector4 BaseColorFactor
        {
            get => this._baseColor;
            set
            {
                this._baseColor = value;
                this._coreParamsChanged = true;
            }
        }

        public float MetallicFactor
        {
            get => this._metalness;
            set
            {
                this._metalness = value;
                this._coreParamsChanged = true;
            }
        }

        public float RoughnessFactor
        {
            get => this._roughness;
            set
            {
                this._roughness = value;
                this._coreParamsChanged = true;
            }
        }

        public TextureAnimation BaseColorAnimation { get; set; }
        public VTT.GL.Texture BaseColorTexture { get; set; }

        // r - occlusion
        // g - roughness
        // b - metallic
        public TextureAnimation OcclusionMetallicRoughnessAnimation { get; set; }
        public VTT.GL.Texture OcclusionMetallicRoughnessTexture { get; set; }
        public TextureAnimation NormalAnimation { get; set; }
        public VTT.GL.Texture NormalTexture { get; set; }
        public TextureAnimation EmissionAnimation { get; set; }
        public VTT.GL.Texture EmissionTexture { get; set; }
        public Material.AlphaModeEnum AlphaMode { get; set; }
        public float AlphaCutoff { get; set; }
        public bool CullFace { get; set; }

        private bool _uboConstructed;
        public UniformBufferMaterial UBO { get; set; }

        private static GlbMaterial lastMaterial;
        private static double lastAnimationFrameIndex;
        private static UniformBlockMaterial lastProgram;

        public bool GetTexturesAsyncStatus()
        {
            return
                this.BaseColorTexture.IsAsyncReady &&
                this.EmissionTexture.IsAsyncReady &&
                this.NormalTexture.IsAsyncReady &&
                this.OcclusionMetallicRoughnessTexture.IsAsyncReady;
        }

        public static void ResetState()
        {
            lastMaterial = null;
            lastAnimationFrameIndex = 0;
            lastProgram = null;
        }

        public void Uniform(UniformBlockMaterial uniforms, double textureAnimationFrameIndex)
        {
            if (SunShadowRenderer.ShadowPass || uniforms == null)
            {
                return;
            }

            if (!this._uboConstructed)
            {
                this.CreateUBO();
                if (!this._uboConstructed)
                {
                    return; // UBO requires async textures to be loaded first. If they are not loaded, then we can't continue rendering here.
                }
            }

            if (lastMaterial != this)
            {
                lastMaterial = this;
                this.UpdateUBO(textureAnimationFrameIndex);
                GL.BindBuffer(BufferTarget.Uniform, 0);
                this.UBO.BindAsUniformBuffer();
                GLState.ActiveTexture.Set(0);
                this.BaseColorTexture.Bind();
                GLState.ActiveTexture.Set(1);
                this.NormalTexture.Bind();
                GLState.ActiveTexture.Set(2);
                this.EmissionTexture.Bind();
                GLState.ActiveTexture.Set(3);
                this.OcclusionMetallicRoughnessTexture.Bind();
                if (this.CullFace)
                {
                    GLState.CullFace.Set(true);
                    GLState.CullFaceMode.Set(PolygonFaceMode.Back);
                }
                else
                {
                    GLState.CullFace.Set(false);
                }
            }

            /* Old non-ubo code, now obsolete
            {
                // Objects are aligned in memory by their AssetID (ideally). This will unfortunately fail for forward rendering.
                if (lastProgram != uniforms || lastMaterial != this)
                {
                    uniforms.MaterialFactors.Set(new Vector4(
                        VTTMath.UInt32BitsToSingle(this.BaseColorFactor.Rgba()),
                        this.MetallicFactor, this.RoughnessFactor, this.AlphaCutoff
                    ));

                    uniforms.DiffuseAnimationFrame.Set(this.BaseColorAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                    uniforms.NormalAnimationFrame.Set(this.NormalAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                    uniforms.EmissiveAnimationFrame.Set(this.EmissionAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                    uniforms.AOMRAnimationFrame.Set(this.OcclusionMetallicRoughnessAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                    lastProgram = uniforms;
                    lastMaterial = this;
                    lastAnimationFrameIndex = textureAnimationFrameIndex;

                    uniforms.MaterialIndexPackedPadded.Set(new Vector4(VTTMath.UInt32BitsToSingle(this.Index)));
                    GLState.ActiveTexture.Set(0);
                    this.BaseColorTexture.Bind();
                    GLState.ActiveTexture.Set(1);
                    this.NormalTexture.Bind();
                    GLState.ActiveTexture.Set(2);
                    this.EmissionTexture.Bind();
                    GLState.ActiveTexture.Set(3);
                    this.OcclusionMetallicRoughnessTexture.Bind();
                    if (this.CullFace)
                    {
                        GLState.CullFace.Set(true);
                        GLState.CullFaceMode.Set(PolygonFaceMode.Back);
                    }
                    else
                    {
                        GLState.CullFace.Set(false);
                    }
                }
                else
                {
                    if (textureAnimationFrameIndex != lastAnimationFrameIndex)
                    {
                        lastAnimationFrameIndex = textureAnimationFrameIndex;
                        uniforms.DiffuseAnimationFrame.Set(this.BaseColorAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                        uniforms.NormalAnimationFrame.Set(this.NormalAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                        uniforms.EmissiveAnimationFrame.Set(this.EmissionAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                        uniforms.AOMRAnimationFrame.Set(this.OcclusionMetallicRoughnessAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                    }
                }
            }
            */

            this._coreParamsChanged = false;
        }

        private ulong _lastUBOUpdateFrame;
        private double _lastAnimationIndex;

        private unsafe void UpdateUBO(double animationIndex)
        {
            ulong now = Client.Instance.Frontend.FramesExisted;
            if (now != this._lastUBOUpdateFrame) // It is possible that this material was already updated this frame, just bad memory align.
            {
                this._lastUBOUpdateFrame = now;
                if (this._coreParamsChanged)
                {
                    // Need to re-upload the entire UBO
                    this.UBO.UpdateFull(this, animationIndex);
                }
                else
                {
                    // Can potentially get away with just the animations
                    if (this.BaseColorAnimation.IsAnimated || this.EmissionAnimation.IsAnimated || this.NormalAnimation.IsAnimated || this.OcclusionMetallicRoughnessAnimation.IsAnimated)
                    {
                        if (this._lastAnimationIndex != animationIndex)
                        {
                            // Technically could do on a per-animation basis, resulting in less data transfer, but it could result in multiple subData calls if data is badly aligned
                            this._lastAnimationIndex = animationIndex;
                            this.UBO.UpdateAnimationsOnly(this, animationIndex);
                        }
                    }
                }
            }
        }

        private unsafe void CreateUBO()
        {
            if (this.BaseColorAnimation == null || this.EmissionAnimation == null || this.NormalAnimation == null || this.OcclusionMetallicRoughnessAnimation == null)
            {
                return;
            }

            bool anyAnimated = this.BaseColorAnimation.IsAnimated || this.EmissionAnimation.IsAnimated || this.NormalAnimation.IsAnimated || this.OcclusionMetallicRoughnessAnimation.IsAnimated;
            this.UBO = new UniformBufferMaterial(this, anyAnimated);
            this._uboConstructed = true;
        }

        public void Dispose()
        {
            this.BaseColorTexture.Dispose();
            this.OcclusionMetallicRoughnessTexture.Dispose();
            this.NormalTexture.Dispose();
            this.EmissionTexture.Dispose();
            if (this._uboConstructed)
            {
                this.UBO.Dispose();
            }
        }
    }

    public class TextureAnimation
    {
        private uint _totalDuration;
        public Frame[] Frames { get; set; }
        public bool IsAnimated => this.Frames.Length > 1;

        public TextureAnimation(Frame[] frames)
        {
            if (frames == null || frames.Length == 0)
            {
                frames = new Frame[] { new Frame() { Duration = 1, Location = new RectangleF(0, 0, 1, 1) } };
            }

            this.Frames = frames;
            uint i = 0;
            foreach (Frame f in frames)
            {
                f.Index = i++;
                if (f.Duration == 0)
                {
                    f.Duration = 1;
                }

                this._totalDuration += f.Duration;
            }

            double dS = 0;
            foreach (Frame f in frames)
            {
                double fPd = (double)f.Duration / this._totalDuration;
                f._allPrevElementsWeightNoSelf = dS;
                dS += fPd;
                f._allPrevElementsWeight = dS;
            }
        }

        public void SetFrameData(Frame[] frames)
        {
            if (frames == null || frames.Length == 0)
            {
                frames = new Frame[] { new Frame() { Duration = 1, Location = new RectangleF(0, 0, 1, 1) } };
            }

            uint i = 0;
            uint td = 0;
            foreach (Frame f in frames)
            {
                f.Index = i++;
                if (f.Duration == 0)
                {
                    f.Duration = 1;
                }

               td += f.Duration;
            }

            double dS = 0;
            foreach (Frame f in frames)
            {
                double fPd = (double)f.Duration / td;
                f._allPrevElementsWeightNoSelf = dS;
                dS += fPd;
                f._allPrevElementsWeight = dS;
            }

            this._totalDuration = td;
            this.Frames = frames;
        }

        public Frame FindFrameForIndex(double idx)
        {
            if (this.Frames.Length == 1)
            {
                return this.Frames[0];
            }

            if (double.IsNaN(idx))
            {
                uint update = (uint)Client.Instance.Frontend.UpdatesExisted;
                uint tFrames = this._totalDuration;
                idx = (double)(update % tFrames) / tFrames;
            }

            if (idx > 1)
            {
                idx = idx - (int)idx;
            }

            if (idx == 1)
            {
                return this.Frames[Frames.Length - 1];
            }

            if (idx < 0)
            {
                idx = 1.0 - (Math.Abs(idx) - (int)Math.Abs(idx));
            }

            if (idx == 0)
            {
                return this.Frames[0];
            }

            foreach (Frame f in this.Frames)
            {
                if (idx > f._allPrevElementsWeightNoSelf && idx <= f._allPrevElementsWeight)
                {
                    return f;
                }
            }

            return this.Frames[0];
        }

        public class Frame
        {
            public RectangleF Location
            {
                get => this._location;
                set
                {
                    this._location = value;
                    this._locationUniform = new Vector4(this.Location.X, this.Location.Y, this.Location.Width, this.Location.Height);
                }
            }

            public Vector4 LocationUniform => this._locationUniform;
            public uint Duration { get; set; }
            public uint Index { get; set; }

            internal double _allPrevElementsWeight;
            internal double _allPrevElementsWeightNoSelf;
            private RectangleF _location;
            private Vector4 _locationUniform;
        }
    }
}
