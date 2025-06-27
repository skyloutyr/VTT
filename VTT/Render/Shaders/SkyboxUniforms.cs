namespace VTT.Render.Shaders
{
    public class SkyboxUniforms
    {
        [UniformContainer]
        public CommonMatrixUniforms Transform { get; set; } = new CommonMatrixUniforms();

        [UniformContainer]
        public UniformBlockSkybox Skybox { get; set; } = new UniformBlockSkybox();
    }
}
