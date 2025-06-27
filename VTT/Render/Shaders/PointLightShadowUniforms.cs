namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class PointLightShadowUniforms
    {
        [UniformReference("model")]
        public UniformState<Matrix4x4> Model { get; set; }

        [UniformReference("bones", Array = true, CheckValue = false)]
        public UniformState<Matrix4x4> Bones { get; set; }

        [UniformReference("is_animated")]
        public UniformState<bool> IsAnimated { get; set; }

        [UniformReference("projView", Array = true, CheckValue = false)]
        public UniformState<Matrix4x4> ProjView { get; set; }

        [UniformReference("layer_offset")]
        public UniformState<int> LayerOffset { get; set; }

        [UniformReference("light_pos")]
        public UniformState<Vector3> LightPosition { get; set; }

        [UniformReference("far_plane")]
        public UniformState<float> LightFarPlane { get; set; }

        public GLBRendererUniformCollection glbEssentials;

        public void PostConstruct() => glbEssentials = new GLBRendererUniformCollection(this.Model, UniformState<Matrix4x4>.Invalid, this.IsAnimated, null);
    }
}
