namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class UniformBlockGrid
    {
        [UniformReference("grid_alpha")]
        public UniformState<float> GridAlpha { get; set; }

        [UniformReference("grid_type")]
        public UniformState<uint> GridType { get; set; }

        [UniformReference("grid_color")]
        public UniformState<Vector4> Color { get; set; }

        [UniformReference("grid_size")]
        public UniformState<float> Scale { get; set; }
    }
}
