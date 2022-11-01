namespace VTT.GL
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using System.Collections.Generic;

    public readonly struct UniformManager
    {
        private static readonly UniformWrapper invalid = new UniformWrapper(-1);
        private readonly Dictionary<string, UniformWrapper> _name2idMappings = new Dictionary<string, UniformWrapper>();

        public UniformManager()
        {
        }

        public void InitUniforms(int pId)
        {
            GL.UseProgram(pId);
            GL.GetProgram(pId, GetProgramParameterName.ActiveUniforms, out int count);
            for (int i = 0; i < count; ++i)
            {
                GL.GetActiveUniform(pId, i, 128, out _, out _, out _, out string nameStr);
                if (nameStr.IndexOf('[') != -1)
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

    public struct UniformWrapper
    {
        private readonly int _id;

        public bool Valid => this._id != -1;

        public UniformWrapper(int id) => this._id = id;

        public void Set(float f)
        {
            if (this.Valid)
            {
                GL.Uniform1(this._id, f);
            }
        }

        public void Set(uint i)
        {
            if (this.Valid)
            {
                GL.Uniform1(this._id, i);
            }
        }

        public void Set(int i)
        {
            if (this.Valid)
            {
                GL.Uniform1(this._id, i);
            }
        }

        public void Set(bool b)
        {
            if (this.Valid)
            {
                GL.Uniform1(this._id, b ? 1 : 0);
            }
        }

        public void Set(Vector2 vec)
        {
            if (this.Valid)
            {
                GL.Uniform2(this._id, vec);
            }
        }

        public void Set(System.Numerics.Vector2 vec)
        {
            if (this.Valid)
            {
                GL.Uniform2(this._id, vec.X, vec.Y);
            }
        }

        public void Set(Vector3 vec)
        {
            if (this.Valid)
            {
                GL.Uniform3(this._id, vec);
            }
        }

        public void Set(System.Numerics.Vector3 vec)
        {
            if (this.Valid)
            {
                GL.Uniform3(this._id, vec.X, vec.Y, vec.Z);
            }
        }

        public void Set(Vector4 vec)
        {
            if (this.Valid)
            {
                GL.Uniform4(this._id, vec);
            }
        }

        public void Set(System.Numerics.Vector4 vec)
        {
            if (this.Valid)
            {
                GL.Uniform4(this._id, vec.X, vec.Y, vec.Z, vec.W);
            }
        }

        public void Set(Matrix2 mat)
        {
            if (this.Valid)
            {
                GL.UniformMatrix2(this._id, 1, false, ref mat.Row0.X);
            }
        }

        public void Set(Matrix3 mat)
        {
            if (this.Valid)
            {
                GL.UniformMatrix3(this._id, 1, false, ref mat.Row0.X);
            }
        }

        public void Set(Matrix4 mat)
        {
            if (this.Valid)
            {
                GL.UniformMatrix4(this._id, 1, false, ref mat.Row0.X);
            }
        }

        public static implicit operator bool(UniformWrapper self) => self.Valid;
        public static implicit operator UniformWrapper(int index) => new UniformWrapper(index);
    }
}
