namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class DeferredUniforms
    {
        [UniformContainer]
        public UniformBlockFrameData FrameData { get; set; } = new UniformBlockFrameData();

        [UniformContainer]
        public UniformBlockObjectData PerObjectData { get; set; } = new UniformBlockObjectData();

        [UniformContainer]
        public UniformBlockMaterial Material { get; set; } = new UniformBlockMaterial();

        [UniformReference("tint_color")]
        public UniformState<Vector4> TintColor { get; set; }

        [UniformContainer]
        public UniformBlockGrid Grid { get; set; } = new UniformBlockGrid();

        public GLBRendererUniformCollection glbEssentials;

        public void PostConstruct() => glbEssentials = new GLBRendererUniformCollection(this.PerObjectData.Model, this.PerObjectData.MVPMatrix, this.PerObjectData.IsAnimated, this.Material);
    }
}
