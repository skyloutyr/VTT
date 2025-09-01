namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class SunShadowUniforms
    {
        [UniformReference("model", CheckValue = false)]
        public UniformState<Matrix4x4> Model { get; set; }

        [UniformReference("bones", Array = true, CheckValue = false)]
        public UniformState<Matrix4x4> Bones { get; set; }

        [UniformReference("is_animated")]
        public UniformState<bool> IsAnimated { get; set; }

        [UniformReference("layer_indices", Array = true)]
        public UniformState<int> LayerIndices { get; set; }

        [UniformReference("light_matrices", Array = true, CheckValue = false)]
        public UniformState<Matrix4x4> LightMatrices { get; set; }


        public GLBRendererUniformCollection glbEssentials;

        public void PostConstruct() => glbEssentials = new GLBRendererUniformCollection(this.Model, UniformState<Matrix4x4>.Invalid, this.IsAnimated, null);
    }
}
