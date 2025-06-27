namespace VTT.Render.Shaders
{
    using System.Numerics;

    // For overlay
    public class OverlayUniforms
    {
        [UniformContainer]
        public CommonMatrixUniforms Transform { get; set; } = new CommonMatrixUniforms();

        [UniformReference("u_color")]
        public UniformState<Vector4> Color { get; set; }

        [UniformReference("texture_image")]
        public UniformState<int> Sampler { get; set; }
    }
}
