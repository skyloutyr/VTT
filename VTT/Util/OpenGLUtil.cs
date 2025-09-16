namespace VTT.Util
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using VTT.Asset.Obj;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using static VTT.Network.ClientSettings;

    public static class OpenGLUtil
    {
        private static readonly Dictionary<string, bool> glExtensionAvailability = new Dictionary<string, bool>();
        private static bool glExtensionsDetermined;
        private static void GatherGLExtensions()
        {
            if (ArgsManager.TryGetValue(LaunchArgumentKey.DisableOpenGLEXT, out bool noExt))
            {
                glExtensionsDetermined = true;
                return;
            }

            int exts = GL.GetInteger(GLPropertyName.NumExtensions)[0];
            for (uint i = 0; i < exts; ++i)
            {
                glExtensionAvailability[GL.GetExtensionAt(i).ToLower()] = true;
            }

            glExtensionsDetermined = true;
        }

        public static bool IsExtensionAvailable(string ext)
        {
            if (!glExtensionsDetermined)
            {
                GatherGLExtensions();
            }

            return glExtensionAvailability.ContainsKey(ext.ToLower());
        }

        public static SizedInternalFormat SrgbCompressedFormat { get; set; }
        public static SizedInternalFormat SrgbAlphaCompressedFormat { get; set; }
        public static SizedInternalFormat RgbCompressedFormat { get; set; }
        public static SizedInternalFormat RgbaCompressedFormat { get; set; }

        public static bool UsingDXTCompression { get; set; }

        public static SizedInternalFormat MapCompressedFormat(SizedInternalFormat fmtIn)
        {
            return fmtIn switch
            {
                SizedInternalFormat.CompressedSrgbAlphaBPTC => SrgbAlphaCompressedFormat,
                SizedInternalFormat.CompressedRgbBPTCFloat => RgbCompressedFormat,
                SizedInternalFormat.CompressedRgbaBPTC => RgbaCompressedFormat,
                _ => fmtIn
            };
        }

        public static void DetermineCompressedFormats()
        {
            SrgbCompressedFormat = SizedInternalFormat.CompressedSrgbAlphaBPTC;
            SrgbAlphaCompressedFormat = SizedInternalFormat.CompressedSrgbAlphaBPTC;
            RgbCompressedFormat = SizedInternalFormat.CompressedRgbaBPTC;
            RgbaCompressedFormat = SizedInternalFormat.CompressedRgbaBPTC;

            TextureCompressionPreference tcp = Client.Instance.Settings.CompressionPreference;
            if (tcp == TextureCompressionPreference.Disabled)
            {
                UsingDXTCompression = false;
                return;
            }

            bool bptcAvailable = IsExtensionAvailable("GL_ARB_texture_compression_bptc");
            bool dxtAvailable = IsExtensionAvailable("GL_EXT_texture_compression_s3tc") && IsExtensionAvailable("GL_EXT_texture_sRGB");
            if (tcp == TextureCompressionPreference.BPTC)
            {
                if (bptcAvailable)
                {
                    SrgbAlphaCompressedFormat = SizedInternalFormat.CompressedSrgbAlphaBPTCUnorm;
                    SrgbCompressedFormat = SizedInternalFormat.CompressedSrgbAlphaBPTCUnorm;
                    RgbCompressedFormat = SizedInternalFormat.CompressedRgbaBPTCUnorm;
                    RgbaCompressedFormat = SizedInternalFormat.CompressedRgbaBPTCUnorm;
                    UsingDXTCompression = false;
                }
                else
                {
                    if (dxtAvailable)
                    {
                        SrgbAlphaCompressedFormat = SizedInternalFormat.CompressedSrgbAlphaS3TCDxt5Ext;
                        SrgbCompressedFormat = SizedInternalFormat.CompressedSrgbS3TCDxt1Ext;
                        RgbCompressedFormat = SizedInternalFormat.CompressedRgbS3TCDxt1Ext;
                        RgbaCompressedFormat = SizedInternalFormat.CompressedRgbaS3TCDxt5Ext;
                        UsingDXTCompression = true;
                    }
                }
            }
            else
            {
                if (dxtAvailable)
                {
                    SrgbAlphaCompressedFormat = SizedInternalFormat.CompressedSrgbAlphaS3TCDxt5Ext;
                    SrgbCompressedFormat = SizedInternalFormat.CompressedSrgbS3TCDxt1Ext;
                    RgbCompressedFormat = SizedInternalFormat.CompressedRgbS3TCDxt1Ext;
                    RgbaCompressedFormat = SizedInternalFormat.CompressedRgbaS3TCDxt5Ext;
                    UsingDXTCompression = true;
                }
                else
                {
                    if (bptcAvailable)
                    {
                        SrgbAlphaCompressedFormat = SizedInternalFormat.CompressedSrgbAlphaBPTCUnorm;
                        SrgbCompressedFormat = SizedInternalFormat.CompressedSrgbAlphaBPTCUnorm;
                        RgbCompressedFormat = SizedInternalFormat.CompressedRgbaBPTCUnorm;
                        RgbaCompressedFormat = SizedInternalFormat.CompressedRgbaBPTCUnorm;
                        UsingDXTCompression = false;
                    }
                }
            }
        }

        public static WavefrontObject LoadModel(string name, VertexFormat desiredFormat)
        {
            string[] lines = IOVTT.ResourceToLines("VTT.Embed." + name + ".obj");
            return new WavefrontObject(lines, desiredFormat);
        }

        public static ShaderProgram LoadShader(string name, Span<ShaderType> types, Span<DefineRule> defines = default) => LoadShaderCode(name, types, defines);

        private static ShaderProgram LoadShaderCode(string name, Span<ShaderType> types, Span<DefineRule> defines = default)
        {
            string vSh = null;
            string gSh = null;
            string fSh = null;

            bool reqV = false;
            bool reqG = false;
            bool reqF = false;

            foreach (ShaderType st in types)
            {
                switch (st)
                {
                    case ShaderType.Vertex:
                    {
                        reqV = true;
                        break;
                    }

                    case ShaderType.Geometry:
                    {
                        reqG = true;
                        break;
                    }

                    case ShaderType.Fragment:
                    {
                        reqF = true;
                        break;
                    }
                }
            }

            static string ProcessRules(string input, Span<DefineRule> rules)
            {
                if (rules.Length == 0)
                {
                    return input;
                }

                foreach (DefineRule dr in rules)
                {
                    input = dr.ApplyRule(input);
                }

                return input;
            }

            if (reqV)
            {
                vSh = ProcessRules(IOVTT.ResourceToString("VTT.Embed." + name + ".vert"), defines);
            }

            if (reqG)
            {
                gSh = ProcessRules(IOVTT.ResourceToString("VTT.Embed." + name + ".geom"), defines);
            }

            if (reqF)
            {
                fSh = ProcessRules(IOVTT.ResourceToString("VTT.Embed." + name + ".frag"), defines);
            }

            Client.Instance.Logger.Log(LogLevel.Debug, "Loading shader VTT.Embed." + name);
            if (!ShaderProgram.TryCompile(out ShaderProgram sp, vSh, gSh, fSh, out string err))
            {
                Logger l = Client.Instance.Logger;
                l.Log(LogLevel.Fatal, "Could not compile shader!");
                l.Log(LogLevel.Fatal, err);
                throw new Exception("Could not compile shader " + name + "! Shader error was " + err);
            }

            NameObject(GLObjectType.Program, sp, name.Capitalize() + " program");
            return sp;
        }

        public static Texture LoadUIImage(string name, SizedInternalFormat format = SizedInternalFormat.Rgba8, WrapParam wrap = WrapParam.ClampToBorder)
        {
            Texture tex = new Texture(TextureTarget.Texture2D);
            tex.Bind();
            tex.SetWrapParameters(wrap, wrap, wrap);
            if (wrap == WrapParam.ClampToBorder)
            {
                GL.TexParameter(TextureTarget.Texture2D, TextureProperty.BorderColor, new float[] { 0, 0, 0, 0 });
            }

            tex.SetFilterParameters(FilterParam.LinearMipmapLinear, FilterParam.Linear);
            using Image<Rgba32> img = IOVTT.ResourceToImage<Rgba32>("VTT.Embed." + name + ".png");
            tex.SetImage(img, format);
            tex.GenerateMipMaps();
            return tex;
        }

        public static Texture LoadFromOnePixel(Rgba32 pixel, SizedInternalFormat format = SizedInternalFormat.Rgba8)
        {
            Texture tex = new Texture(TextureTarget.Texture2D);
            tex.Bind();
            tex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            tex.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            using Image<Rgba32> img = new Image<Rgba32>(1, 1, pixel);
            tex.SetImage(img, format);
            return tex;
        }

        public static Texture LoadBasicTexture(Image<Rgba32> img, SizedInternalFormat format = SizedInternalFormat.Rgba8)
        {
            Texture tex = new Texture(TextureTarget.Texture2D);
            tex.Bind();
            tex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            tex.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            tex.SetImage(img, format);
            return tex;
        }

        public static Size GetMipmapSize(Size imgSize, int mipLevel)
        {
            float div = 1 << mipLevel;
            int nW = (int)Math.Floor(Math.Max(1, imgSize.Width / div));
            int nH = (int)Math.Floor(Math.Max(1, imgSize.Height / div));
            return new Size(nW, nH);
        }

        public static int GetMaxMipmapAmount(Size imgSize, int minSize = 1) => (int)Math.Max(minSize, 1 + Math.Floor(Math.Log2(Math.Max(imgSize.Width, imgSize.Height))));

        public static void StartSection(string label)
        {
            if (Client.Instance.Frontend.GLDebugEnabled)
            {
                GL.PushDebugGroup(label);
            }
        }

        public static void EndSection()
        {
            if (Client.Instance.Frontend.GLDebugEnabled)
            {
                GL.PopDebugGroup();
            }
        }

        public static void NameObject(GLObjectType type, uint obj, string name)
        {
            if (Client.Instance.Frontend.GLDebugEnabled)
            {
                Client.Instance.Logger.Log(LogLevel.Debug, $"Assigned name {name} to GL {type} of ID {obj}.");
                GL.ObjectLabel(type, obj, name);
            }
        }
    }

    public readonly struct DefineRule
    { 
        public enum Mode
        {
            Define,
            Undef,
            Replace,
            ReplaceOrDefine
        }

        public readonly Mode mode;
        public readonly string key;
        public readonly string value;

        public DefineRule(Mode mode, string key, string value)
        {
            this.mode = mode;
            this.key = $"#define {key}";
            this.value = $"#define {value}";
        }

        public DefineRule(Mode mode, string value)
        {
            this.mode = mode;
            this.key = string.Empty;
            this.value = $"#define {value}";
        }

        public readonly string ApplyRule(string strIn)
        {
            switch (this.mode)
            {
                case Mode.Define:
                {
                    strIn = this.AddDefine(strIn);
                    break;
                }

                case Mode.Undef:
                {
                    strIn = this.RemoveDefine(strIn);
                    break;
                }

                case Mode.Replace:
                {
                    this.ReplaceDefine(ref strIn);
                    break;
                }

                case Mode.ReplaceOrDefine:
                {
                    if (!this.ReplaceDefine(ref strIn))
                    {
                        strIn = this.AddDefine(strIn);
                    }

                    break;
                }
            }

            return strIn;
        }

        public readonly string AddDefine(string str)
        {
            int idxTopLineEnd = str.IndexOf('\n');
            if (idxTopLineEnd != -1 && idxTopLineEnd != str.Length - 1)
            {
                // Account for lf
                bool useLf = false;
                if (str[idxTopLineEnd + 1] == '\r')
                {
                    idxTopLineEnd += 1;
                    useLf = true;
                }

                str = str.Insert(idxTopLineEnd + 1, $"{this.value}\n{(useLf ? "\n" : "")}");
            }

            return str;
        }

        public readonly string RemoveDefine(string str)
        {
            string key = string.IsNullOrEmpty(this.value) ? this.key : this.value;
            int idx = str.IndexOf(key);
            if (idx != -1)
            {
                int len = key.Length;
                if (idx + len < str.Length - 1)
                {
                    // check for cr
                    if (str[idx + len] == '\n')
                    {
                        len += 1;
                    }
                }

                if (idx + len < str.Length - 1)
                {
                    // check for lf
                    if (str[idx + len] == '\r')
                    {
                        len += 1;
                    }
                }

                str = str.Remove(idx, len);
            }

            return str;
        }

        public readonly bool ReplaceDefine(ref string str)
        {
            if (!string.IsNullOrEmpty(this.key) && !string.IsNullOrEmpty(this.value))
            {
                str = str.Replace(this.key, this.value);
                return true;
            }

            return false;
        }
    }
}
