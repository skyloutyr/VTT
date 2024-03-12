namespace VTT.Util
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Linq;
    using VTT.Asset.Obj;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using static VTT.Network.ClientSettings;

    public static class OpenGLUtil
    {
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

            int exts = GL.GetInteger(GLPropertyName.NumExtensions)[0];
            string[] allExtensions = new string[exts];
            for (uint i = 0; i < exts; ++i)
            {
                string extension = GL.GetExtensionAt(i);
                if (!extension.StartsWith("GL_"))
                {
                    extension = "GL_" + extension;
                }

                allExtensions[i] = extension.ToLower();
            }

            bool bptcAvailable = allExtensions.Contains("gl_arb_texture_compression_bptc");
            bool dxtAvailable = allExtensions.Contains("gl_ext_texture_compression_s3tc") && allExtensions.Contains("gl_ext_texture_srgb");
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

        public static ShaderProgram LoadShader(string name, params ShaderType[] types) => LoadShaderCode(name, types);

        private static ShaderProgram LoadShaderCode(string name, params ShaderType[] types)
        {
            string vSh = null;
            string gSh = null;
            string fSh = null;

            if (types.Contains(ShaderType.Vertex))
            {
                vSh = IOVTT.ResourceToString("VTT.Embed." + name + ".vert");
            }

            if (types.Contains(ShaderType.Geometry))
            {
                gSh = IOVTT.ResourceToString("VTT.Embed." + name + ".geom");
            }

            if (types.Contains(ShaderType.Fragment))
            {
                fSh = IOVTT.ResourceToString("VTT.Embed." + name + ".frag");
            }

            Client.Instance.Logger.Log(LogLevel.Debug, "Loading shader VTT.Embed." + name + ".vert");
            if (!ShaderProgram.TryCompile(out ShaderProgram sp, vSh, gSh, fSh, out string err))
            {
                Logger l = Client.Instance.Logger;
                l.Log(LogLevel.Fatal, "Could not compile shader!");
                l.Log(LogLevel.Fatal, err);
                throw new System.Exception("Could not compile shader " + name + "! Shader error was " + err);
            }

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
    }
}
