namespace VTT.GL
{
    using System;
    using VTT.GL.Bindings;
    using GL = VTT.GL.Bindings.GL;

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

        public static bool TryCompile(out ShaderProgram sp, string vertCode, string geomCode, string fragCode, out string error)
        {
            int vShader = -1;
            int gShader = -1;
            int fShader = -1;

            static bool CompileShader(ShaderType sType, ref int s, string code, out string lerror)
            {
                s = (int)GL.CreateShader(sType);
                GL.ShaderSource((uint)s, code);
                GL.CompileShader((uint)s);
                int result = GL.GetShaderProperty((uint)s, ShaderProperty.CompileStatus);
                bool ret = result == 1;
                if (!ret)
                {
                    lerror = GL.GetShaderInfoLog((uint)s);
                    GL.DeleteShader((uint)s);
                    s = -1;
                }
                else
                {
                    lerror = string.Empty;
                }

                return ret;
            }

            if (!string.IsNullOrEmpty(vertCode) && !CompileShader(ShaderType.Vertex, ref vShader, vertCode, out error))
            {
                sp = default;
                return false;
            }

            if (!string.IsNullOrEmpty(geomCode) && !CompileShader(ShaderType.Geometry, ref gShader, geomCode, out error))
            {
                sp = default;
                return false;
            }

            if (!string.IsNullOrEmpty(fragCode) && !CompileShader(ShaderType.Fragment, ref fShader, fragCode, out error))
            {
                sp = default;
                return false;
            }

            ShaderProgram ret = new ShaderProgram();
            int pId = (int)ret._glID;
            if (vShader != -1)
            {
                GL.AttachShader((uint)pId, (uint)vShader);
            }

            if (gShader != -1)
            {
                GL.AttachShader((uint)pId, (uint)gShader);
            }

            if (fShader != -1)
            {
                GL.AttachShader((uint)pId, (uint)fShader);
            }

            GL.LinkProgram((uint)pId);
            int pls = GL.GetProgramProperty((uint)pId, ProgramProperty.LinkStatus)[0];
            if (pls != 1)
            {
                GL.DeleteProgram((uint)pId);
                sp = default;
                error = GL.GetProgramInfoLog((uint)pId);
                return false;
            }

            if (vShader != -1)
            {
                GL.DetachShader((uint)pId, (uint)vShader);
                GL.DeleteShader((uint)vShader);
            }

            if (gShader != -1)
            {
                GL.DetachShader((uint)pId, (uint)gShader);
                GL.DeleteShader((uint)gShader);
            }

            if (fShader != -1)
            {
                GL.DetachShader((uint)pId, (uint)fShader);
                GL.DeleteShader((uint)fShader);
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
        public void BindUniformBlock(string blockName, uint slot)
        {
            uint index = GL.GetUniformBlockIndex(this._glID, blockName);
            GL.UniformBlockBinding(this._glID, index, slot);
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
