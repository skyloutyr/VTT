namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class UniformBlockSkybox
    {
        [UniformReference("tex_skybox")]
        public UniformState<int> Sampler { get; set; }

        [UniformReference("animation_day")]
        public UniformState<Vector4> DayAnimationFrame { get; set; }

        [UniformReference("animation_night")]
        public UniformState<Vector4> NightAnimationFrame { get; set; }

        [UniformReference("daynight_blend")]
        public UniformState<float> BlendingFactor { get; set; }

        [UniformReference("day_color")]
        public UniformState<Vector3> ColorDay { get; set; }

        [UniformReference("night_color")]
        public UniformState<Vector3> ColorNight { get; set; }
    }
}
