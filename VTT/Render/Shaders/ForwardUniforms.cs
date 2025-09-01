namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class ForwardUniforms
    {
        [UniformContainer]
        public UniformBlockFrameData FrameData { get; set; } = new UniformBlockFrameData();

        [UniformContainer]
        public UniformBlockObjectData PerObjectData { get; set; } = new UniformBlockObjectData();

        [UniformContainer]
        public UniformBlockMaterial Material { get; set; } = new UniformBlockMaterial();

        [UniformReference("tint_color")]
        public UniformState<Vector4> TintColor { get; set; }

        [UniformContainer]
        public UniformBlockDirectionalLight DirectionalLight { get; set; } = new UniformBlockDirectionalLight();

        [UniformContainer]
        public UniformBlockPointLights PointLights { get; set; } = new UniformBlockPointLights();

        [UniformContainer]
        public UniformBlockFogOfWar FOW { get; set; } = new UniformBlockFogOfWar();

        [UniformContainer]
        public UniformBlockGrid Grid { get; set; } = new UniformBlockGrid();

        [UniformContainer]
        public UniformBlockCustomShader CustomShaderExtraTexturesData { get; set; } = new UniformBlockCustomShader();

        [UniformContainer]
        public UniformBlockGamma Gamma { get; set; } = new UniformBlockGamma();

        public GLBRendererUniformCollection glbEssentials;

        public void PostConstruct() => glbEssentials = new GLBRendererUniformCollection(this.PerObjectData.Model, this.PerObjectData.MVPMatrix, this.PerObjectData.IsAnimated, this.Material);
    }
}
