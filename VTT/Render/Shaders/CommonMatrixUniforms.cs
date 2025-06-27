namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class CommonMatrixUniforms
    {
        [UniformReference("projection")]
        public UniformState<Matrix4x4> Projection { get; set; }

        [UniformReference("view")]
        public UniformState<Matrix4x4> View { get; set; }

        [UniformReference("model", CheckValue = false)] // Model matrix is expected to be changed each draw, no need to check it
        public UniformState<Matrix4x4> Model { get; set; }
    }
}
