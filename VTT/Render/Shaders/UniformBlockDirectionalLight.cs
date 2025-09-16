namespace VTT.Render.Shaders
{
    public class UniformBlockDirectionalLight
    {
        [UniformReference("dl_shadow_map")]
        public UniformState<int> DepthSampler { get; set; }
    }
}
