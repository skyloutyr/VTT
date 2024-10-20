namespace VTT.GL
{
    using System.Collections.Generic;
    using System.Numerics;
    using VTT.GL.Bindings;
    using GL = VTT.GL.Bindings.GL;

    public readonly struct UniformManager
    {
        private static readonly UniformWrapper invalid = new UniformWrapper(-1);
        private readonly Dictionary<string, UniformWrapper> _name2idMappings = new Dictionary<string, UniformWrapper>();

        public UniformManager()
        {
        }

        public void InitUniforms(uint pId)
        {
            GL.UseProgram(pId);
            int count = GL.GetProgramProperty(pId, ProgramProperty.ActiveUniforms)[0];
            for (int i = 0; i < count; ++i)
            {
                GL.GetActiveUniform(pId, (uint)i, 128, out _, out _, out _, out string nameStr);
                if (nameStr.Contains('['))
                {
                    int c = 0;
                    string sB = nameStr[..(nameStr.IndexOf('[') + 1)];
                    string sE = nameStr[nameStr.IndexOf(']')..];
                    while (true)
                    {
                        string sL = sB + c + sE;
                        int j = GL.GetUniformLocation(pId, sL);
                        if (j == -1)
                        {
                            break;
                        }

                        this._name2idMappings[sL] = j;
                        c++;
                    }
                }
                else
                {
                    this._name2idMappings[nameStr] = GL.GetUniformLocation(pId, nameStr);
                }
            }
        }

        public UniformWrapper GetUniform(string name) => this._name2idMappings.GetValueOrDefault(name, invalid);

        public Dictionary<string, UniformWrapper> Name2IDMappings => this._name2idMappings;
    }

    public readonly struct UniformWrapper
    {
        private readonly int _id;

        public bool Valid => this._id != -1;

        public UniformWrapper(int id) => this._id = id;

        public void Set(float f)
        {
            if (this.Valid)
            {
                GL.Uniform(this._id, f);
            }
        }

        public void Set(uint i)
        {
            if (this.Valid)
            {
                GL.Uniform(this._id, i);
            }
        }

        public void Set(int i)
        {
            if (this.Valid)
            {
                GL.Uniform(this._id, i);
            }
        }

        public void Set(bool b)
        {
            if (this.Valid)
            {
                GL.Uniform(this._id, b ? 1 : 0);
            }
        }

        public void Set(Vector2 vec)
        {
            if (this.Valid)
            {
                GL.Uniform(this._id, vec);
            }
        }

        public void Set(Vector3 vec)
        {
            if (this.Valid)
            {
                GL.Uniform(this._id, vec);
            }
        }

        public void Set(Vector4 vec)
        {
            if (this.Valid)
            {
                GL.Uniform(this._id, vec);
            }
        }


        public void Set(Matrix4x4 mat)
        {
            if (this.Valid)
            {
                GL.Uniform(this._id, mat);
            }
        }

        public static implicit operator bool(UniformWrapper self) => self.Valid;
        public static implicit operator UniformWrapper(int index) => new UniformWrapper(index);
    }
}
