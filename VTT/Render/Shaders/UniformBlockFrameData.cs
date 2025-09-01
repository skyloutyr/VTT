namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class UniformBlockFrameData
    {
        [UniformReference("projection")]
        public UniformState<Matrix4x4> Projection { get; set; }

        [UniformReference("view")]
        public UniformState<Matrix4x4> View { get; set; }

        [UniformReference("sun_matrix")]
        public UniformState<Matrix4x4> SunMatrix { get; set; }

        [UniformReference("camera_position_sundir")]
        public UniformState<Vector4> CameraPositionSunDirection { get; set; }

        [UniformReference("camera_direction_sunclr")]
        public UniformState<Vector4> CameraDirectionSunColor { get; set; }

        [UniformReference("al_sky_colors_viewportsz")]
        public UniformState<Vector4> AmbientSkyColorsViewportSize { get; set; }

        [UniformReference("cursor_position_gridclr")]
        public UniformState<Vector4> CursorPositionGridColor { get; set; }

        [UniformReference("frame_update_updatedt_gridsz")]
        public UniformState<Vector4> FrameUpdateDTGridSZ { get; set; }

        [UniformContainer]
        public UniformBlockSkybox Skybox { get; set; } = new UniformBlockSkybox();
    }
}
