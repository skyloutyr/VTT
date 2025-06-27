namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class RTShadowUniforms
    {
        [UniformReference("positions")]
        public UniformState<int> FragmentPositionsSampler { get; set; }

        [UniformReference("boxes")]
        public UniformState<int> BoxesBufferSampler { get; set; }

        [UniformReference("bvh")]
        public UniformState<int> BVHBufferSampler { get; set; }

        [UniformReference("bvhHasData")]
        public UniformState<bool> BVHHasData { get; set; }

        [UniformReference("noCursor")]
        public UniformState<bool> NoCursor { get; set; }

        [UniformReference("cursor_position")]
        public UniformState<Vector2> CursorPosition { get; set; }

        [UniformReference("shadow_opacity")]
        public UniformState<float> ShadowOpacity { get; set; }

        [UniformReference("light_threshold")]
        public UniformState<float> LightThreshold { get; set; }

        [UniformReference("light_dimming")]
        public UniformState<float> LightDimming { get; set; }

        [UniformReference("lights", Array = true, CheckValue = false)]
        public UniformState<Vector4> Lights { get; set; }

        [UniformReference("num_lights")]
        public UniformState<int> LightsAmount { get; set; }
    }
}
