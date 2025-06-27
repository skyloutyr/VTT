namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class GridUniforms
    {
        [UniformContainer]
        public CommonMatrixUniforms Transform { get; set; } = new CommonMatrixUniforms();

        [UniformReference("camera_position")]
        public UniformState<Vector3> CameraPosition { get; set; }

        [UniformReference("cursor_position")]
        public UniformState<Vector3> CursorPosition { get; set; }

        [UniformContainer]
        public UniformBlockGrid Grid { get; set; } = new UniformBlockGrid();
    }
}
