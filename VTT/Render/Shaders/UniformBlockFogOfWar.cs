namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class UniformBlockFogOfWar
    {
        [UniformReference("fow_texture")]
        public UniformState<int> Sampler { get; set; }

        [UniformReference("fow_offset")]
        public UniformState<Vector2> Offset { get; set; }

        [UniformReference("fow_scale")]
        public UniformState<Vector2> Scale { get; set; }

        [UniformReference("fow_mod")]
        public UniformState<float> Opacity { get; set; }
    }
}
