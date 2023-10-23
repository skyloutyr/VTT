﻿namespace VTT.Util
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Windowing.GraphicsLibraryFramework;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Linq;
    using VTT.Asset.Obj;
    using VTT.GL;
    using VTT.Network;
    using static VTT.Network.ClientSettings;

    public static class OpenGLUtil
    {
        public static PixelInternalFormat SrgbCompressedFormat { get; set; }
        public static PixelInternalFormat SrgbAlphaCompressedFormat { get; set; }
        public static PixelInternalFormat RgbCompressedFormat { get; set; }
        public static PixelInternalFormat RgbaCompressedFormat { get; set; }

        public static bool UsingDXTCompression { get; set; }

        public static PixelInternalFormat MapCompressedFormat(PixelInternalFormat fmtIn)
        {
            return fmtIn switch
            {
                PixelInternalFormat.CompressedSrgb => SrgbCompressedFormat,
                PixelInternalFormat.CompressedSrgbAlpha => SrgbAlphaCompressedFormat,
                PixelInternalFormat.CompressedRgb => RgbCompressedFormat,
                PixelInternalFormat.CompressedRgba => RgbaCompressedFormat,
                _ => fmtIn
            };
        }

        public static void DetermineCompressedFormats()
        {
            SrgbCompressedFormat = PixelInternalFormat.CompressedSrgb;
            SrgbAlphaCompressedFormat = PixelInternalFormat.CompressedSrgbAlpha;
            RgbCompressedFormat = PixelInternalFormat.CompressedRgb;
            RgbaCompressedFormat = PixelInternalFormat.CompressedRgba;

            TextureCompressionPreference tcp = Client.Instance.Settings.CompressionPreference;
            if (tcp == TextureCompressionPreference.Disabled)
            {
                UsingDXTCompression = false;
                return;
            }

            int exts = GL.GetInteger(GetPName.NumExtensions);
            string[] allExtensions = new string[exts];
            for (int i = 0; i < exts; ++i)
            {
                string extension = GL.GetString(StringNameIndexed.Extensions, i);
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
                    SrgbAlphaCompressedFormat = PixelInternalFormat.CompressedSrgbAlphaBptcUnorm;
                    SrgbCompressedFormat = PixelInternalFormat.CompressedSrgbAlphaBptcUnorm;
                    RgbCompressedFormat = PixelInternalFormat.CompressedRgbaBptcUnorm;
                    RgbaCompressedFormat = PixelInternalFormat.CompressedRgbaBptcUnorm;
                    UsingDXTCompression = false;
                }
                else
                {
                    if (dxtAvailable)
                    {
                        SrgbAlphaCompressedFormat = PixelInternalFormat.CompressedSrgbAlphaS3tcDxt5Ext;
                        SrgbCompressedFormat = PixelInternalFormat.CompressedSrgbS3tcDxt1Ext;
                        RgbCompressedFormat = PixelInternalFormat.CompressedRgbS3tcDxt1Ext;
                        RgbaCompressedFormat = PixelInternalFormat.CompressedRgbaS3tcDxt5Ext;
                        UsingDXTCompression = true;
                    }
                }
            }
            else
            {
                if (dxtAvailable)
                {
                    SrgbAlphaCompressedFormat = PixelInternalFormat.CompressedSrgbAlphaS3tcDxt5Ext;
                    SrgbCompressedFormat = PixelInternalFormat.CompressedSrgbS3tcDxt1Ext;
                    RgbCompressedFormat = PixelInternalFormat.CompressedRgbS3tcDxt1Ext;
                    RgbaCompressedFormat = PixelInternalFormat.CompressedRgbaS3tcDxt5Ext;
                    UsingDXTCompression = true;
                }
                else
                {
                    if (bptcAvailable)
                    {
                        SrgbAlphaCompressedFormat = PixelInternalFormat.CompressedSrgbAlphaBptcUnorm;
                        SrgbCompressedFormat = PixelInternalFormat.CompressedSrgbAlphaBptcUnorm;
                        RgbCompressedFormat = PixelInternalFormat.CompressedRgbaBptcUnorm;
                        RgbaCompressedFormat = PixelInternalFormat.CompressedRgbaBptcUnorm;
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

        private static readonly Lazy<bool> haveSpirVArb = new Lazy<bool>(() => IsExtensionSupported("ARB_gl_spirv"));
        public static bool IsExtensionSupported(string extName) => GLFW.ExtensionSupported(extName);

        public static bool PreferSpirV { get; set; }

        public static bool ShouldUseSPIRV => haveSpirVArb.Value && PreferSpirV && Client.Instance.Settings.UseSpirVShaders;

        public static ShaderProgram LoadShader(string name, params ShaderType[] types)
        {
            bool trySpirV = false;
            if (haveSpirVArb.Value && PreferSpirV)
            {
                trySpirV = IOVTT.DoesResourceExist("VTT.Embed." + name + ".vert.spv") || IOVTT.DoesResourceExist("VTT.Embed." + name + ".frag.spv");
            }

            return trySpirV ? LoadShaderBinary(name, types) : LoadShaderCode(name, types);
        }

        private static ShaderProgram LoadShaderBinary(string name, params ShaderType[] types)
        {
            byte[] vSh = null;
            byte[] gSh = null;
            byte[] fSh = null;

            if (types.Contains(ShaderType.VertexShader))
            {
                vSh = IOVTT.ResourceToBytes("VTT.Embed." + name + ".vert.spv");
            }

            if (types.Contains(ShaderType.GeometryShader))
            {
                gSh = IOVTT.ResourceToBytes("VTT.Embed." + name + ".geom.spv");
            }

            if (types.Contains(ShaderType.FragmentShader))
            {
                fSh = IOVTT.ResourceToBytes("VTT.Embed." + name + ".frag.spv");
            }

            Client.Instance.Logger.Log(LogLevel.Debug, "Loading SPIR-V shader VTT.Embed." + name);
            if (!ShaderProgram.TryLoadBinary(out ShaderProgram sp, vSh, gSh, fSh, x => default, out string err))
            {
                Logger l = Client.Instance.Logger;
                l.Log(LogLevel.Fatal, "Could not compile SPIR-V shader!");
                l.Log(LogLevel.Fatal, err);
                throw new System.Exception("Could not compile SPIR-V shader " + name + "! Shader error was " + err);
            }

            return sp;
        }

        private static ShaderProgram LoadShaderCode(string name, params ShaderType[] types)
        {
            string vSh = null;
            string gSh = null;
            string fSh = null;

            if (types.Contains(ShaderType.VertexShader))
            {
                vSh = IOVTT.ResourceToString("VTT.Embed." + name + ".vert");
            }

            if (types.Contains(ShaderType.GeometryShader))
            {
                gSh = IOVTT.ResourceToString("VTT.Embed." + name + ".geom");
            }

            if (types.Contains(ShaderType.FragmentShader))
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

        public static Texture LoadUIImage(string name, PixelInternalFormat format = PixelInternalFormat.Rgba, WrapParam wrap = WrapParam.ClampToBorder)
        {
            Texture tex = new Texture(TextureTarget.Texture2D);
            tex.Bind();
            tex.SetWrapParameters(wrap, wrap, wrap);
            if (wrap == WrapParam.ClampToBorder)
            {
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 0, 0, 0, 0 });
            }

            tex.SetFilterParameters(FilterParam.LinearMipmapLinear, FilterParam.Linear);
            using Image<Rgba32> img = IOVTT.ResourceToImage<Rgba32>("VTT.Embed." + name + ".png");
            tex.SetImage(img, format);
            tex.GenerateMipMaps();
            return tex;
        }

        public static Texture LoadFromOnePixel(Rgba32 pixel, PixelInternalFormat format = PixelInternalFormat.Rgba)
        {
            Texture tex = new Texture(TextureTarget.Texture2D);
            tex.Bind();
            tex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            tex.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            using Image<Rgba32> img = new Image<Rgba32>(1, 1, pixel);
            tex.SetImage(img, format);
            return tex;
        }

        public static Texture LoadBasicTexture(Image<Rgba32> img, PixelInternalFormat format = PixelInternalFormat.Rgba)
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
