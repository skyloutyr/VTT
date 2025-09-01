namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class UniformBlockDirectionalLight
    {
        [UniformReference("dl_shadow_map")]
        public UniformState<int> DepthSampler { get; set; }
    }
}
