namespace VTT.Util
{
    using OpenTK.Core;
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System.Collections.Generic;
    using System.Linq;
    using VTT.Asset.Obj;
    using VTT.GL;
    using VTT.Network;

    public static class OpenGLUtil
    {
        private static Dictionary<ShaderType, string> expectedShaderExtensions = new Dictionary<ShaderType, string>() {
            [ShaderType.VertexShader] = "vert",
            [ShaderType.FragmentShader] = "frag",
            [ShaderType.GeometryShader] = "geom"
        };

        public static WavefrontObject LoadModel(string name, VertexFormat desiredFormat)
        {
            string[] lines = IOVTT.ResourceToLines("VTT.Embed." + name + ".obj");
            return new WavefrontObject(lines, desiredFormat);
        }

        public static ShaderProgram LoadShader(string name, params ShaderType[] types)
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
            if (!ShaderProgram.TryCompile(out ShaderProgram sp, vSh, gSh, fSh))
            {
                Logger l = Client.Instance.Logger;
                l.Log(LogLevel.Fatal, "Could not compile shader!");
                throw new System.Exception("Could not compile shader " + name);
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
    }
}
