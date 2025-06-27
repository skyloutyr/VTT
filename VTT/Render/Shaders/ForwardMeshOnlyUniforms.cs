namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class ForwardMeshOnlyUniforms
    {
        [UniformContainer]
        public CommonMatrixUniforms Transform { get; set; } = new CommonMatrixUniforms();

        [UniformReference("bones", Array = true, CheckValue = false)]
        public UniformState<Matrix4x4> Bones { get; set; }

        [UniformReference("is_animated")]
        public UniformState<bool> IsAnimated { get; set; }

        [UniformContainer]
        public UniformBlockMaterial Material { get; set; } = new UniformBlockMaterial();

        [UniformContainer]
        public UniformBlockGamma Gamma { get; set; } = new UniformBlockGamma();

        [UniformContainer]
        public UniformBlockTime Time { get; set; } = new UniformBlockTime();

        public GLBRendererUniformCollection glbEssentials;

        public void PostConstruct() => glbEssentials = new GLBRendererUniformCollection(this.Transform.Model, UniformState<Matrix4x4>.Invalid, this.IsAnimated, this.Material);
    }
}
