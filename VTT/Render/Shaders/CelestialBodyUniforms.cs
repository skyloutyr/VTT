namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class CelestialBodyUniforms
    {
        [UniformReference("mvp", CheckValue = false)]
        public UniformState<Matrix4x4> MVP { get; set; }

        [UniformReference("u_color")]
        public UniformState<Vector4> Color { get; set; }

        [UniformReference("m_texture_diffuse")]
        public UniformState<int> DiffuseSampler { get; set; }

        public GLBRendererUniformCollection glbEssentials;
        public void PostConstruct() => glbEssentials = new GLBRendererUniformCollection(UniformState<Matrix4x4>.Invalid, this.MVP, UniformState<bool>.Invalid, null);
    }
}
