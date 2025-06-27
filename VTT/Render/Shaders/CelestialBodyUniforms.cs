namespace VTT.Render.Shaders
{
    public class CelestialBodyUniforms
    {
        [UniformContainer]
        public CommonMatrixUniforms Transform { get; set; } = new CommonMatrixUniforms();
    }
}
