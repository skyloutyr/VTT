namespace VTT.GL
{
    using OpenTK.Graphics.OpenGL;
    using System;
    using System.Linq;

    public class ShaderProgram
    {
        private readonly uint _glID;
        public UniformManager UniformManager { get; }
        public UniformWrapper this[string name] => this.UniformManager.GetUniform(name);

        private static uint _lastProgramID;

        public ShaderProgram()
        {
            this._glID = (uint)GL.CreateProgram();
            this.UniformManager = new UniformManager();
        }

        public static bool IsLastShaderSame(ShaderProgram currentShader) => currentShader._glID == _lastProgramID;

        public static bool TryLoadBinary(out ShaderProgram sp, byte[] vert, byte[] geom, byte[] frag, Func<ShaderType, SpirVSpecializationData> specializer, out string error)
        {
            int vShader = -1;
            int gShader = -1;
            int fShader = -1;

            static void IncludeShader(ShaderType sType, ref int s, byte[] code, Func<ShaderType, SpirVSpecializationData> specializer)
            {
                s = GL.CreateShader(sType);
                GL.ShaderBinary(1, ref s, (BinaryFormat)All.ShaderBinaryFormatSpirVArb, code, code.Length);
                SpirVSpecializationData data = specializer(sType);
                GL.SpecializeShader(s, "main", data.SpecializationConstantIndices?.Length ?? 0, data.SpecializationConstantIndices, data.SpecializationConstantValues);
            }

            static bool IsNullOrEmpty(byte[] data) => data == null || data.Length == 0;


            if (!IsNullOrEmpty(vert))
            {
                IncludeShader(ShaderType.VertexShader, ref vShader, vert, specializer);
            }

            if (!IsNullOrEmpty(geom))
            {
                IncludeShader(ShaderType.GeometryShader, ref gShader, geom, specializer);
            }

            if (!IsNullOrEmpty(frag))
            {
                IncludeShader(ShaderType.FragmentShader, ref fShader, frag, specializer);
            }

            ShaderProgram ret = new ShaderProgram();
            int pId = (int)ret._glID;
            if (vShader != -1)
            {
                GL.AttachShader(pId, vShader);
            }

            if (gShader != -1)
            {
                GL.AttachShader(pId, gShader);
            }

            if (fShader != -1)
            {
                GL.AttachShader(pId, fShader);
            }

            GL.LinkProgram(pId);
            GL.GetProgram(pId, GetProgramParameterName.LinkStatus, out int pls);
            if (pls != 1)
            {
                GL.DeleteProgram(pId);
                sp = default;
                GL.GetProgramInfoLog(pId, out error);
                return false;
            }

            if (vShader != -1)
            {
                GL.DetachShader(pId, vShader);
                GL.DeleteShader(vShader);
            }

            if (gShader != -1)
            {
                GL.DetachShader(pId, gShader);
                GL.DeleteShader(gShader);
            }

            if (fShader != -1)
            {
                GL.DetachShader(pId, fShader);
                GL.DeleteShader(fShader);
            }

            sp = ret;
            ret.UniformManager.InitUniforms(ret);
            error = string.Empty;
            return true;
        }

        public static bool TryCompile(out ShaderProgram sp, string vertCode, string geomCode, string fragCode, out string error)
        {
            int vShader = -1;
            int gShader = -1;
            int fShader = -1;

            static bool CompileShader(ShaderType sType, ref int s, string code, out string lerror)
            {
                s = GL.CreateShader(sType);
                GL.ShaderSource(s, code);
                GL.CompileShader(s);
                GL.GetShader(s, ShaderParameter.CompileStatus, out int result);
                bool ret = result == 1;
                if (!ret)
                {
                    GL.GetShaderInfoLog(s, out lerror);
                    GL.DeleteShader(s);
                }
                else
                {
                    lerror = string.Empty;
                }

                return ret;
            }

            if (!string.IsNullOrEmpty(vertCode) && !CompileShader(ShaderType.VertexShader, ref vShader, vertCode, out error))
            {
                sp = default;
                return false;
            }

            if (!string.IsNullOrEmpty(geomCode) && !CompileShader(ShaderType.GeometryShader, ref gShader, geomCode, out error))
            {
                sp = default;
                return false;
            }

            if (!string.IsNullOrEmpty(fragCode) && !CompileShader(ShaderType.FragmentShader, ref fShader, fragCode, out error))
            {
                sp = default;
                return false;
            }

            ShaderProgram ret = new ShaderProgram();
            int pId = (int)ret._glID;
            if (vShader != -1)
            {
                GL.AttachShader(pId, vShader);
            }

            if (gShader != -1)
            {
                GL.AttachShader(pId, gShader);
            }

            if (fShader != -1)
            {
                GL.AttachShader(pId, fShader);
            }

            GL.LinkProgram(pId);
            GL.GetProgram(pId, GetProgramParameterName.LinkStatus, out int pls);
            if (pls != 1)
            {
                GL.DeleteProgram(pId);
                sp = default;
                GL.GetProgramInfoLog(pId, out error);
                return false;
            }

            if (vShader != -1)
            {
                GL.DetachShader(pId, vShader);
                GL.DeleteShader(vShader);
            }

            if (gShader != -1)
            {
                GL.DetachShader(pId, gShader);
                GL.DeleteShader(gShader);
            }

            if (fShader != -1)
            {
                GL.DetachShader(pId, fShader);
                GL.DeleteShader(fShader);
            }

            sp = ret;
            ret.UniformManager.InitUniforms(ret);
            error = string.Empty;
            return true;
        }

        public void Bind()
        {
            GL.UseProgram(this._glID);
            _lastProgramID = this._glID;
        }

        public void Dispose() => GL.DeleteProgram(this._glID);
        public void BindUniformBlock(string blockName, int slot)
        {
            int index = GL.GetUniformBlockIndex(this._glID, blockName);
            GL.UniformBlockBinding((int)this._glID, index, slot);
        }

        public static implicit operator uint(ShaderProgram self) => self._glID;
        public static implicit operator int(ShaderProgram self) => (int)self._glID;
    }

    public readonly struct SpirVSpecializationData
    {
        public int[] SpecializationConstantIndices { get; }
        public int[] SpecializationConstantValues { get; }

        public SpirVSpecializationData(int[] specializationConstantIndices, int[] specializationConstantValues)
        {
            this.SpecializationConstantIndices = specializationConstantIndices;
            this.SpecializationConstantValues = specializationConstantValues;
        }
    }
}
