namespace VTT.Render.Shaders
{
    using System.Numerics;

    public readonly struct GLBRendererUniformCollection
    {
        public UniformState<Matrix4x4> Model { get; }
        public UniformState<Matrix4x4> MVP { get; }
        public UniformState<bool> IsAnimated { get; }
        public UniformBlockMaterial Material { get; }

        public GLBRendererUniformCollection(UniformState<Matrix4x4> model, UniformState<Matrix4x4> mVP, UniformState<bool> isAnimated, UniformBlockMaterial material)
        {
            this.Model = model;
            this.MVP = mVP;
            this.IsAnimated = isAnimated;
            this.Material = material;
        }
    }
}
