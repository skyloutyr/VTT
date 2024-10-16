namespace VTT.Render
{
    using VTT.GL;

    public class FastAccessShader
    {
        public ShaderProgram Program { get; }
        public MaterialUniforms Material { get; }

        public UniformWrapper this[string s] => this.Program[s];

        public FastAccessShader(ShaderProgram shader)
        {
            this.Program = shader;
            this.Material = new MaterialUniforms(shader);
        }

        public static implicit operator ShaderProgram(FastAccessShader self) => self.Program;

        public readonly struct MaterialUniforms
        {
            public readonly UniformWrapper DiffuseColor { get; init; }
            public readonly UniformWrapper MetallicFactor { get; init; }
            public readonly UniformWrapper RoughnessFactor { get; init; }
            public readonly UniformWrapper AlphaCutoff { get; init; }
            public readonly UniformWrapper DiffuseFrame { get; init; }
            public readonly UniformWrapper NormalFrame { get; init; }
            public readonly UniformWrapper EmissiveFrame { get; init; }
            public readonly UniformWrapper AOMRFrame { get; init; }
            public readonly UniformWrapper MaterialIndex { get; init; }

            public MaterialUniforms(ShaderProgram prog)
            {
                this.DiffuseColor = prog["m_diffuse_color"];
                this.MetallicFactor = prog["m_metal_factor"];
                this.RoughnessFactor = prog["m_roughness_factor"];
                this.AlphaCutoff = prog["m_alpha_cutoff"];
                this.DiffuseFrame = prog["m_diffuse_frame"];
                this.NormalFrame = prog["m_normal_frame"];
                this.EmissiveFrame = prog["m_emissive_frame"];
                this.AOMRFrame = prog["m_aomr_frame"];
                this.MaterialIndex = prog["material_index"];
            }
        }
    }
}
