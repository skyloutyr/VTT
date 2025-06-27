namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class UniformBlockCustomShader
    {
        [UniformReference("unifiedTexture")]
        public UniformState<int> Sampler { get; set; }

        [UniformReference("unifiedTextureData", Array = true)]
        public UniformState<Vector2> Sizes { get; set; }

        [UniformReference("unifiedTextureFrames", Array = true)]
        public UniformState<Vector4> AnimationFrames { get; set; }
    }
}
