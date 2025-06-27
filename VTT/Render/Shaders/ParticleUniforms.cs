namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class ParticleUniforms
    {
        [UniformContainer]
        public CommonMatrixUniforms Transform { get; set; } = new CommonMatrixUniforms();

        [UniformContainer]
        public UniformBlockTime Time { get; set; } = new UniformBlockTime();

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

        [UniformReference("sky_color")]
        public UniformState<Vector3> SkyColor { get; set; }

        [UniformContainer]
        public UniformBlockGamma Gamma { get; set; } = new UniformBlockGamma();

        [UniformReference("cursor_position")]
        public UniformState<Vector3> CursorPosition { get; set; }

        [UniformReference("viewport_size")]
        public UniformState<Vector2> ViewportSize { get; set; }

        [UniformContainer]
        public UniformBlockCustomShader CustomShader { get; set; } = new UniformBlockCustomShader();

        [UniformReference("texture_shadows2d")]
        public UniformState<int> Sampler2DShadows { get; set; }

        [UniformContainer]
        public UniformBlockSkybox Skybox { get; set; } = new UniformBlockSkybox();

        public GLBRendererUniformCollection glbEssentials;

        public void PostConstruct() => glbEssentials = new GLBRendererUniformCollection(this.Transform.Model, UniformState<Matrix4x4>.Invalid, UniformState<bool>.Invalid, this.Material);
    }
}
