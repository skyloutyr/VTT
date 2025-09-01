namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class UniformBlockMaterial
    {
        [UniformReference("albedo_metal_roughness_alpha_cutoff")]
        public UniformState<Vector4> MaterialFactors { get; set; }

        [UniformReference("m_texture_diffuse")]
        public UniformState<int> DiffuseSampler { get; set; }

        [UniformReference("m_texture_normal")]
        public UniformState<int> NormalSampler { get; set; }

        [UniformReference("m_texture_emissive")]
        public UniformState<int> EmissiveSampler { get; set; }

        [UniformReference("m_texture_aomr")]
        public UniformState<int> AOMRSampler { get; set; }

        [UniformReference("m_diffuse_frame")]
        public UniformState<Vector4> DiffuseAnimationFrame { get; set; }

        [UniformReference("m_normal_frame")]
        public UniformState<Vector4> NormalAnimationFrame { get; set; }

        [UniformReference("m_emissive_frame")]
        public UniformState<Vector4> EmissiveAnimationFrame { get; set; }

        [UniformReference("m_aomr_frame")]
        public UniformState<Vector4> AOMRAnimationFrame { get; set; }

        [UniformReference("material_index")]
        public UniformState<Vector4> MaterialIndexPackedPadded { get; set; }
    }
}
