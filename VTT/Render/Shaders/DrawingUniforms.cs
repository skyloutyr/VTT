namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class DrawingUniforms
    {
        [UniformContainer]
        public CommonMatrixUniforms Transform { get; set; } = new CommonMatrixUniforms();

        [UniformReference("u_color")]
        public UniformState<Vector4> Color { get; set; }
    }
}
