namespace VTT.Render.Shaders
{
    public class PipelineFinalUniforms
    {
        [UniformReference("g_color")]
        public UniformState<int> ColorSampler { get; set; }

        [UniformReference("g_depth")]
        public UniformState<int> DepthSampler { get; set; }

        [UniformReference("g_fast_light")]
        public UniformState<int> FastLightSampler { get; set; }

        [UniformReference("g_shadows2d")]
        public UniformState<int> Shadows2DSampler { get; set; }

        [UniformReference("gamma")]
        public UniformState<float> Gamma { get; set; }
    }
}
