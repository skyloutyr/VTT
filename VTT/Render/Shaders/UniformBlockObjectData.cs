namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class UniformBlockObjectData
    {
        [UniformReference("model", CheckValue = false)]
        public UniformState<Matrix4x4> Model { get; set; }

        [UniformReference("mvp", CheckValue = false)]
        public UniformState<Matrix4x4> MVPMatrix { get; set; }

        [UniformReference("is_animated")]
        public UniformState<bool> IsAnimated { get; set; }

        [UniformReference("bones", Array = true, CheckValue = false)]
        public UniformState<Matrix4x4> Bones { get; set; }
    }
}
