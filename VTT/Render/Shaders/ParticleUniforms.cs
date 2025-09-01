namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class ParticleUniforms
    {
        [UniformReference("model", CheckValue = false)]
        public UniformState<Matrix4x4> Model { get; set; }

        [UniformContainer]
        public UniformBlockFrameData FrameData { get; set; } = new UniformBlockFrameData();

        [UniformReference("dataBuffer")]
        public UniformState<int> DataBufferSampler { get; set; }

        [UniformContainer]
        public UniformBlockFogOfWar FOW { get; set; } = new UniformBlockFogOfWar();

        [UniformReference("do_fow")]
        public UniformState<bool> DoFOW { get; set; }

        [UniformReference("billboard")]
        public UniformState<bool> DoBillboard { get; set; }

        [UniformReference("is_sprite_sheet")]
        public UniformState<bool> IsSpriteSheet { get; set; }

        [UniformReference("sprite_sheet_data")]
        public UniformState<Vector2> SpriteSheetData { get; set; }

        [UniformContainer]
        public UniformBlockMaterial Material { get; set; } = new UniformBlockMaterial();

        [UniformContainer]
        public UniformBlockGamma Gamma { get; set; } = new UniformBlockGamma();

        [UniformContainer]
        public UniformBlockCustomShader CustomShader { get; set; } = new UniformBlockCustomShader();

        [UniformReference("texture_shadows2d")]
        public UniformState<int> Sampler2DShadows { get; set; }

        public GLBRendererUniformCollection glbEssentials;

        public void PostConstruct() => glbEssentials = new GLBRendererUniformCollection(this.Model, UniformState<Matrix4x4>.Invalid, UniformState<bool>.Invalid, this.Material);
    }
}
