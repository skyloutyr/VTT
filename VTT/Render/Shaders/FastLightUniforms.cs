namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class FastLightUniforms
    {
        [UniformReference("projection")]
        public UniformState<Matrix4x4> Projection { get; set; }

        [UniformReference("view")]
        public UniformState<Matrix4x4> View { get; set; }

        [UniformReference("model")]
        public UniformState<Vector4> Model { get; set; }

        [UniformReference("g_positions")]
        public UniformState<int> PositionsSampler { get; set; }

        [UniformReference("g_normals")]
        public UniformState<int> NormalsSampler { get; set; }

        [UniformReference("g_albedo")]
        public UniformState<int> AlbedoSampler { get; set; }

        [UniformReference("g_aomrg")]
        public UniformState<int> AOMRGSampler { get; set; }

        [UniformReference("g_emission")]
        public UniformState<int> EmissionSampler { get; set; }

        [UniformReference("g_depth")]
        public UniformState<int> DepthSampler { get; set; }

        [UniformReference("light_color")]
        public UniformState<Vector4> Color { get; set; }

        [UniformReference("viewport_size")]
        public UniformState<Vector2> ViewportSize { get; set; }

        [UniformReference("camera_position")]
        public UniformState<Vector3> CameraPosition { get; set; }
    }
}
