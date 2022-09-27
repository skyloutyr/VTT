namespace VTT.GL
{
    using OpenTK.Graphics.OpenGL;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Advanced;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Buffers;
    using System.Runtime.InteropServices;

    public class Texture
    {
        private uint _glId;
        private readonly TextureTarget _type;

        public Size Size { get; set; }

        public Texture(TextureTarget tt, bool gl = true)
        {
            this._type = tt;
            if (gl)
            {
                GL.GenTextures(1, out this._glId);
            }
        }

        public void Allocate() => GL.GenTextures(1, out this._glId);

        public void Bind() => GL.BindTexture(this._type, this._glId);

        public void SetFilterParameters(FilterParam min, FilterParam mag)
        {
            GL.TexParameter(this._type, TextureParameterName.TextureMinFilter, GLParamFromFilter(min));
            GL.TexParameter(this._type, TextureParameterName.TextureMagFilter, GLParamFromFilter(mag));
        }

        public void SetWrapParameters(WrapParam x, WrapParam y, WrapParam z)
        {
            GL.TexParameter(this._type, TextureParameterName.TextureWrapS, GLParamFromWrap(x));
            if (this._type is TextureTarget.Texture2D or TextureTarget.Texture2DArray)
            {
                GL.TexParameter(this._type, TextureParameterName.TextureWrapT, GLParamFromWrap(y));
            }

            if (this._type is TextureTarget.Texture3D)
            {
                GL.TexParameter(this._type, TextureParameterName.TextureWrapR, GLParamFromWrap(z));
            }
        }

        public unsafe Image<T> GetImage<T>(int level = 0) where T : unmanaged, IPixel<T>
        {
            GL.GetTexLevelParameter(this._type, level, GetTextureParameter.TextureWidth, out int w);
            GL.GetTexLevelParameter(this._type, level, GetTextureParameter.TextureHeight, out int h);
            Image<T> ret = new Image<T>(w, h);
            if (IntPtr.Size == 4 && ((long)sizeof(T) * w * h > int.MaxValue))
            {
                throw new Exception("Image too large for a 32bit process!");
            }

            T* data = IntPtr.Size == 8 ? (T*)Marshal.AllocHGlobal(new IntPtr((long)sizeof(T) * w * h)) : (T*)Marshal.AllocHGlobal(sizeof(T) * w * h);
            GL.GetTexImage(this._type, level, GetFormatFromPixelType(typeof(T)), PixelType.UnsignedByte, (IntPtr)data);
            ret.ProcessPixelRows(x =>
            {
                for (int y = 0; y < x.Height; ++y)
                {
                    Span<T> rowSpan = x.GetRowSpan(y);
                    Span<T> memSpan = new Span<T>(data + (y * x.Width), x.Width);
                    memSpan.CopyTo(rowSpan);
                }
            });

            Marshal.FreeHGlobal((IntPtr)data);
            return ret;
        }

        public unsafe void SetImage<T>(Image<T> img, PixelInternalFormat format, int level = 0, PixelType type = PixelType.UnsignedByte) where T : unmanaged, IPixel<T>
        {
            this.Size = new Size(img.Width, img.Height);
            if (img.GetConfiguration().PreferContiguousImageBuffers && img.DangerousTryGetSinglePixelMemory(out Memory<T> mem))
            {
                MemoryHandle mh = mem.Pin();
                GL.TexImage2D(this._type, level, format, img.Width, img.Height, 0, GetFormatFromPixelType(typeof(T)), type, new IntPtr(mh.Pointer));
                mh.Dispose();
                return;
            }

            GL.TexImage2D(this._type, level, format, img.Width, img.Height, 0, GetFormatFromPixelType(typeof(T)), type, IntPtr.Zero);
            TextureTarget selfTT = this._type;
            if (img.Height % 4 == 0) // Height is a multiple of 4, attempt to copy in blocks of 4 to support compression
            {
                T* pixelBuffer = (T*)Marshal.AllocHGlobal(sizeof(T) * img.Width * 4);
                img.ProcessPixelRows(x =>
                {
                    for (int y = 0; y < x.Height; ++y)
                    {
                        Span<T> rowSpan = x.GetRowSpan(y);
                        if (y != 0 && y % 4 == 0)
                        {
                            GL.TexSubImage2D(selfTT, level, 0, y - 4, x.Width, 4, GetFormatFromPixelType(typeof(T)), type, (IntPtr)pixelBuffer);
                        }

                        T* tOffsetB = pixelBuffer + (y * img.Width);
                        Span<T> s = new Span<T>(tOffsetB, img.Width);
                        rowSpan.CopyTo(s);
                    }

                    GL.TexSubImage2D(selfTT, level, 0, img.Height - 4, x.Width, 4, GetFormatFromPixelType(typeof(T)), type, (IntPtr)pixelBuffer);
                });

                Marshal.FreeHGlobal((IntPtr)pixelBuffer);
            }
            else
            {
                img.ProcessPixelRows(x =>
                {
                    for (int y = 0; y < x.Height; ++y)
                    {
                        Span<T> rowSpan = x.GetRowSpan(y);
                        fixed (void* span = rowSpan)
                        {
                            GL.TexSubImage2D(selfTT, level, 0, y, x.Width, 1, GetFormatFromPixelType(typeof(T)), type, new IntPtr(span));
                        }
                    }
                });
            }
        }

        public void GenerateMipMaps() => GL.GenerateMipmap((GenerateMipmapTarget)this._type);

        private static int GLParamFromFilter(FilterParam param)
        {
            return param switch
            {
                FilterParam.LinearMipmapLinear => (int)All.LinearMipmapLinear,
                FilterParam.LinearMipmapNearest => (int)All.LinearMipmapNearest,
                FilterParam.Linear => (int)All.Linear,
                _ => (int)All.Nearest
            };
        }

        private static int GLParamFromWrap(WrapParam param)
        {
            return param switch
            {
                WrapParam.Mirror => (int)All.MirroredRepeat,
                WrapParam.ClampToBorder => (int)All.ClampToBorder,
                WrapParam.ClampToEdge => (int)All.ClampToEdge,
                _ => (int)All.Repeat,
            };
        }

        private static PixelFormat GetFormatFromPixelType(Type t)
        {
            if (t == typeof(Rgba32))
            {
                return PixelFormat.Rgba;
            }

            if (t == typeof(Rgba64))
            {
                return PixelFormat.RgbaInteger;
            }

            if (t == typeof(Rgb24))
            {
                return PixelFormat.Rgb;
            }

            return PixelFormat.DepthComponent;
        }

        public void Dispose()
        {
            if (this._glId > 0)
            {
                GL.DeleteTexture(this._glId);
            }
        }

        public static implicit operator uint(Texture self) => self._glId;
        public static implicit operator int(Texture self) => (int)self._glId;
        public static implicit operator IntPtr(Texture self) => new IntPtr(self._glId);
    }

    public enum FilterParam
    {
        Linear,
        Nearest,
        LinearMipmapLinear,
        LinearMipmapNearest
    }

    public enum WrapParam
    {
        Repeat,
        Mirror,
        ClampToBorder,
        ClampToEdge
    }
}
