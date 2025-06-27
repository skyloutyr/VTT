namespace VTT.Render.Shaders
{
    public class IndividualColorOverlay
    {
        [UniformContainer]
        public CommonMatrixUniforms Transform { get; set; } = new CommonMatrixUniforms();
    }
}
