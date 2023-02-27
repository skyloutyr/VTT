namespace VTT.Asset.Glb
{
    using glTFLoader.Schema;
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using System;
    using VTT.GL;
    using VTT.Network;
    using VTT.Render.LightShadow;

    public class GlbMaterial
    {
        public string Name { get; set; }
        public uint Index { get; set; }
        public Vector4 BaseColorFactor { get; set; }
        public float MetallicFactor { get; set; }
        public float RoughnessFactor { get; set; }

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

        private static GlbMaterial lastMaterial;
        private static double lastAnimationFrameIndex;
        private static int lastProgram;

        public void Uniform(ShaderProgram shader, double textureAnimationFrameIndex)
        {
            if (SunShadowRenderer.ShadowPass)
            {
                GL.Disable(EnableCap.CullFace);
                return;
            }
            /* Material data:
             * 
                uniform vec4 m_diffuse_color;
                uniform float m_metal_factor;
                uniform float m_roughness_factor;
                uniform float m_alpha_cutoff;
                uniform sampler2D m_texture_diffuse;
                uniform sampler2D m_texture_normal;
                uniform sampler2D m_texture_emissive;
                uniform sampler2D m_texture_aomr;
             * 
             */

            // Objects are aligned in memory by their AssetID.
            if (lastProgram != shader || lastMaterial != this)
            {
                shader["m_diffuse_color"].Set(this.BaseColorFactor);
                shader["m_metal_factor"].Set(this.MetallicFactor);
                shader["m_roughness_factor"].Set(this.RoughnessFactor);
                shader["m_alpha_cutoff"].Set(this.AlphaCutoff);
                shader["m_diffuse_frame"].Set(this.BaseColorAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                shader["m_normal_frame"].Set(this.NormalAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                shader["m_emissive_frame"].Set(this.EmissionAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                shader["m_aomr_frame"].Set(this.OcclusionMetallicRoughnessAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                lastProgram = shader;
                lastMaterial = this;
            }
            else
            {
                if (textureAnimationFrameIndex != lastAnimationFrameIndex)
                {
                    lastAnimationFrameIndex = textureAnimationFrameIndex;
                    shader["m_diffuse_frame"].Set(this.BaseColorAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                    shader["m_normal_frame"].Set(this.NormalAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                    shader["m_emissive_frame"].Set(this.EmissionAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                    shader["m_aomr_frame"].Set(this.OcclusionMetallicRoughnessAnimation.FindFrameForIndex(textureAnimationFrameIndex).LocationUniform);
                }
            }

            shader["material_index"].Set(this.Index);

            GL.ActiveTexture(TextureUnit.Texture0);
            this.BaseColorTexture.Bind();

            GL.ActiveTexture(TextureUnit.Texture1);
            this.NormalTexture.Bind();

            GL.ActiveTexture(TextureUnit.Texture2);
            this.EmissionTexture.Bind();

            GL.ActiveTexture(TextureUnit.Texture3);
            this.OcclusionMetallicRoughnessTexture.Bind();

            if (this.CullFace)
            {
                GL.Enable(EnableCap.CullFace);
                GL.CullFace(CullFaceMode.Back);
            }
            else
            {
                GL.Disable(EnableCap.CullFace);
            }
        }

        public void Dispose()
        {
            this.BaseColorTexture.Dispose();
            this.OcclusionMetallicRoughnessTexture.Dispose();
            this.NormalTexture.Dispose();
            this.EmissionTexture.Dispose();
        }
    }

    public class TextureAnimation
    {
        private readonly uint _totalDuration;
        public Frame[] Frames { get; set; }

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
            public RectangleF Location { get; set; }
            public Vector4 LocationUniform => new Vector4(this.Location.X, this.Location.Y, this.Location.Width, this.Location.Height);
            public uint Duration { get; set; }
            public uint Index { get; set; }

            internal double _allPrevElementsWeight;
            internal double _allPrevElementsWeightNoSelf;
        }
    }
}
