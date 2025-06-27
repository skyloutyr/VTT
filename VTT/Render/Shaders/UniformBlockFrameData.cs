namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class UniformBlockFrameData
    {
        [UniformReference("projection")]
        public UniformState<Matrix4x4> Projection { get; set; }

        [UniformReference("view")]
        public UniformState<Matrix4x4> View { get; set; }

        [UniformReference("sun_view")]
        public UniformState<Matrix4x4> SunView { get; set; }

        [UniformReference("sun_projection")]
        public UniformState<Matrix4x4> SunProjection { get; set; }

        [UniformReference("camera_position")]
        public UniformState<Vector3> CameraPosition { get; set; }

        [UniformReference("camera_direction")]
        public UniformState<Vector3> CameraDirection { get; set; }

        [UniformReference("dl_direction")]
        public UniformState<Vector3> SunDirection { get; set; }

        [UniformReference("dl_color")]
        public UniformState<Vector3> SunColor { get; set; }

        [UniformReference("al_color")]
        public UniformState<Vector3> AmbientColor { get; set; }

        [UniformReference("sky_color")]
        public UniformState<Vector3> SkyColor { get; set; }

        [UniformReference("cursor_position")]
        public UniformState<Vector3> CursorPosition { get; set; }

        [UniformReference("grid_color")]
        public UniformState<Vector4> GridColor { get; set; }

        [UniformReference("dv_data")]
        public UniformState<Vector4> DarkvisionData { get; set; }

        [UniformReference("frame")]
        public UniformState<uint> Frame { get; set; }

        [UniformReference("update")]
        public UniformState<uint> Update { get; set; }

        [UniformReference("grid_size")]
        public UniformState<float> GridScale { get; set; }

        [UniformReference("frame_delta")]
        public UniformState<float> FrameDT { get; set; }

        [UniformReference("viewport_size")]
        public UniformState<Vector2> ViewportSize { get; set; }
    }
}
