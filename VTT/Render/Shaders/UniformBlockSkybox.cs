namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class UniformBlockSkybox
    {
        [UniformReference("tex_skybox")]
        public UniformState<int> Sampler { get; set; }

        [UniformReference("skybox_animation_day")]
        public UniformState<Vector4> DayAnimationFrame { get; set; }

        [UniformReference("skybox_animation_night")]
        public UniformState<Vector4> NightAnimationFrame { get; set; }

        [UniformReference("skybox_colors_blend")]
        public UniformState<Vector4> ColorsBlend { get; set; }
    }
}
