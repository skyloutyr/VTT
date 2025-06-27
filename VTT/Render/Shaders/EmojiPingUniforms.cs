namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class EmojiPingUniforms
    {
        [UniformContainer]
        public CommonMatrixUniforms Transform { get; set; } = new CommonMatrixUniforms();

        [UniformReference("position")]
        public UniformState<Vector4> BillboardPosition { get; set; }

        [UniformReference("screenSize")]
        public UniformState<Vector2> ScreenSize { get; set; }

        [UniformReference("u_color")]
        public UniformState<Vector4> Color { get; set; }

        [UniformReference("texture_image")]
        public UniformState<int> Sampler { get; set; }
    }
}
