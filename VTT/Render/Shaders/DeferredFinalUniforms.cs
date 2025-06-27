namespace VTT.Render.Shaders
{
    public class DeferredFinalUniforms
    {
        [UniformContainer]
        public UniformBlockFrameData FrameData { get; set; } = new UniformBlockFrameData();

        [UniformReference("g_positions")]
        public UniformState<int> PositionsSampler { get; set; }

        [UniformReference("g_normals")]
        public UniformState<int> NormalsSampler { get; set; }

        [UniformReference("g_albedo")]
        public UniformState<int> AlbedoSampler { get; set; }

        [UniformReference("g_aomrg")]
        public UniformState<int> AOMRGSampler { get; set; }

        [UniformReference("g_emission")]
        public UniformState<int> EmissionSampler { get; set; }

        [UniformReference("g_depth")]
        public UniformState<int> DepthSampler { get; set; }

        [UniformContainer]
        public UniformBlockDirectionalLight DirectionalLight { get; set; } = new UniformBlockDirectionalLight();

        [UniformContainer]
        public UniformBlockPointLights PointLights { get; set; } = new UniformBlockPointLights();

        [UniformContainer]
        public UniformBlockFogOfWar FOW { get; set; } = new UniformBlockFogOfWar();

        [UniformContainer]
        public UniformBlockSkybox Skybox { get; set; } = new UniformBlockSkybox();

        [UniformContainer]
        public UniformBlockGamma Gamma { get; set; } = new UniformBlockGamma();
    }
}
