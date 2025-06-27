namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class HighlightUniforms
    {
        [UniformContainer]
        public CommonMatrixUniforms Transform { get; set; } = new CommonMatrixUniforms();

        [UniformReference("bounds")]
        public UniformState<Vector3> Bounds { get; set; }

        [UniformReference("u_color")]
        public UniformState<Vector4> Color { get; set; }
    }
}
