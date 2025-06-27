namespace VTT.Render.Shaders
{
    using System.Numerics;

    // For moverlay
    public class FOWDependentOverlayUniforms
    {
        [UniformContainer]
        public CommonMatrixUniforms Transform { get; set; } = new CommonMatrixUniforms();

        [UniformReference("u_color")]
        public UniformState<Vector4> Color { get; set; }

        [UniformContainer]
        public UniformBlockFogOfWar FOW { get; set; } = new UniformBlockFogOfWar();

        [UniformReference("do_fow")]
        public UniformState<bool> DoFOW { get; set; }

        [UniformReference("sky_color")]
        public UniformState<Vector3> SkyColor { get; set; }
    }
}
