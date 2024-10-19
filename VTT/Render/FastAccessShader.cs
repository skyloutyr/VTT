namespace VTT.Render
{
    using System;
    using System.Numerics;
    using VTT.GL;

    public class FastAccessShader
    {
        public ShaderProgram Program { get; }

        public MaterialUniforms Material { get; init; }
        public CommonUniforms Essentials { get; init; }
        public ParticleUniforms Particle { get; init; }

        public UniformWrapper this[string s] => this.Program[s];

        public FastAccessShader(ShaderProgram shader)
        {
            this.Program = shader;
            this.Material = new MaterialUniforms(shader);
            this.Essentials = new CommonUniforms(shader);
            this.Particle = new ParticleUniforms(shader);
        }

        public static implicit operator ShaderProgram(FastAccessShader self) => self.Program;

        public class CommonUniforms
        {
            public UniformWrapper Transform { get; init; }
            public UniformWrapper MVP { get; init; }
            public UniformWrapper Projection { get; init; }
            public UniformWrapper View { get; init; }
            public BooleanUniformWrapper IsAnimated { get; init; }
            public Vector4UniformWrapper TintColor { get; init; }
            public FloatUniformWrapper Alpha { get; init; }
            public FloatUniformWrapper GridAlpha { get; init; }

            public CommonUniforms(ShaderProgram prog)
            {
                this.Transform = prog["model"];
                this.MVP = prog["mvp"];
                this.Projection = prog["projection"];
                this.View = prog["view"];
                this.IsAnimated = new BooleanUniformWrapper(prog["is_animated"]);
                this.TintColor = new Vector4UniformWrapper(prog["tint_color"]);
                this.Alpha = new FloatUniformWrapper(prog["alpha"]);
                this.GridAlpha = new FloatUniformWrapper(prog["grid_alpha"]);
            }
        }

        public class MaterialUniforms
        {
            public Vector4UniformWrapper DiffuseColor { get; init; }
            public FloatUniformWrapper MetallicFactor { get; init; }
            public FloatUniformWrapper RoughnessFactor { get; init; }
            public FloatUniformWrapper AlphaCutoff { get; init; }
            public Vector4UniformWrapper DiffuseFrame { get; init; }
            public Vector4UniformWrapper NormalFrame { get; init; }
            public Vector4UniformWrapper EmissiveFrame { get; init; }
            public Vector4UniformWrapper AOMRFrame { get; init; }
            public UnsignedIntegerUniformWrapper MaterialIndex { get; init; }

            public MaterialUniforms(ShaderProgram prog)
            {
                this.DiffuseColor = new Vector4UniformWrapper(prog["m_diffuse_color"]);
                this.MetallicFactor = new FloatUniformWrapper(prog["m_metal_factor"]);
                this.RoughnessFactor = new FloatUniformWrapper(prog["m_roughness_factor"]);
                this.AlphaCutoff = new FloatUniformWrapper(prog["m_alpha_cutoff"]);
                this.DiffuseFrame = new Vector4UniformWrapper(prog["m_diffuse_frame"]);
                this.NormalFrame = new Vector4UniformWrapper(prog["m_normal_frame"]);
                this.EmissiveFrame = new Vector4UniformWrapper(prog["m_emissive_frame"]);
                this.AOMRFrame = new Vector4UniformWrapper(prog["m_aomr_frame"])    ;
                this.MaterialIndex = new UnsignedIntegerUniformWrapper(prog["material_index"]);
            }
        }

        public class ParticleUniforms
        {
            public BooleanUniformWrapper DoBillboard { get; init; }
            public BooleanUniformWrapper DoFOW { get; init; }
            public BooleanUniformWrapper IsSpriteSheet { get; init; }
            public Vector2UniformWrapper SpriteSheetData { get; init; }

            public ParticleUniforms(ShaderProgram prog)
            {
                this.DoBillboard = new BooleanUniformWrapper(prog["billboard"]);
                this.DoFOW = new BooleanUniformWrapper(prog["do_fow"]);
                this.IsSpriteSheet = new BooleanUniformWrapper(prog["is_sprite_sheet"]);
                this.SpriteSheetData = new Vector2UniformWrapper(prog["sprite_sheet_data"]);
            }
        }

        public class BooleanUniformWrapper
        {
            private UniformWrapper _uniform;
            private bool _state;

            public BooleanUniformWrapper(UniformWrapper uniform)
            {
                this._uniform = uniform;
                this._state = false;
            }

            public void Set(bool b)
            {
                if (b != this._state)
                {
                    this._state = b;
                    this._uniform.Set(b);
                }
            }
        }

        public class FloatUniformWrapper
        {
            private UniformWrapper _uniform;
            private float _state;

            public FloatUniformWrapper(UniformWrapper uniform)
            {
                this._uniform = uniform;
                this._state = 0.0f;
            }

            public void Set(float f)
            {
                if (f != this._state)
                {
                    this._state = f;
                    this._uniform.Set(f);
                }
            }

            public void SetWithLogging(float f)
            {
                if (f != this._state)
                {
                    Console.WriteLine($"State changed from {this._state} to {f}.");
                    this._state = f;
                    this._uniform.Set(f);
                }
            }
        }

        public class Vector4UniformWrapper
        {
            private UniformWrapper _uniform;
            private Vector4 _state;

            public Vector4UniformWrapper(UniformWrapper uniform)
            {
                this._uniform = uniform;
                this._state = Vector4.Zero;
            }

            public void Set(Vector4 v)
            {
                if (v != this._state)
                {
                    this._state = v;
                    this._uniform.Set(v);
                }
            }
        }

        public class Vector2UniformWrapper
        {
            private UniformWrapper _uniform;
            private Vector2 _state;

            public Vector2UniformWrapper(UniformWrapper uniform)
            {
                this._uniform = uniform;
                this._state = Vector2.Zero;
            }

            public void Set(Vector2 v)
            {
                if (v != this._state)
                {
                    this._state = v;
                    this._uniform.Set(v);
                }
            }
        }

        public class UnsignedIntegerUniformWrapper
        {
            private UniformWrapper _uniform;
            private uint _state;

            public UnsignedIntegerUniformWrapper(UniformWrapper uniform)
            {
                this._uniform = uniform;
                this._state = 0;
            }

            public void Set(uint ui)
            {
                if (ui != this._state)
                {
                    this._state = ui;
                    this._uniform.Set(ui);
                }
            }
        }
    }
}
