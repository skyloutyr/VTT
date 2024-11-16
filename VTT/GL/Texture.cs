namespace VTT.GL
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Buffers;
    using System.Collections.Concurrent;
    using VTT.GL.Bindings;
    using VTT.Util;

    public class Texture
    {
        private uint _glId;
        private readonly TextureTarget _type;

        public Size Size { get; set; }
        public const int ImageMaximumContiguousMemoryAllowance = 1024 * 1024 * 128; // 128 mb
        public AsyncLoadState AsyncState { get; set; } = AsyncLoadState.NonAsync;
        public bool IsAsyncReady
        {
            get
            {
                if (this.AsyncState != AsyncLoadState.NonAsync)
                {
                    if (this.AsyncState == AsyncLoadState.Ready)
                    {
                        if (this.AsyncFenceID.Equals(IntPtr.Zero))
                        {
                            return true;
                        }
                        else
                        {
                            unsafe
                            {
                                int vals = GL.GetSync((void*)this.AsyncFenceID, SyncProperty.SyncStatus);
                                if (vals == (int)SyncStatus.Signaled)
                                {
                                    GL.DeleteSync((void*)this.AsyncFenceID);
                                    this.AsyncFenceID = IntPtr.Zero;
                                    return true;
                                }
                            }

                            return false;
                        }
                    }

                    return false;
                }

                return true;
            }
        }

        public TextureTarget Target => this._type;
        public IntPtr AsyncFenceID { get; set; }

        private static readonly ConcurrentDictionary<uint, Guid> _textureProtection = new ConcurrentDictionary<uint, Guid>(); // Kinda really bad performance wise, but we don't expect to create too many textures anyway so maybe fine?

        public Texture(TextureTarget tt, bool gl = true)
        {
            this._type = tt;
            if (gl)
            {
                this._glId = GL.GenTexture();
                _textureProtection[this._glId] = Guid.NewGuid();
            }
        }

        public void Allocate()
        {
            this._glId = GL.GenTexture();
            _textureProtection[this._glId] = Guid.NewGuid();
        }

        public Guid GetUniqueID() => _textureProtection[this._glId];
        public bool CheckUniqueID(Guid id)
        {
            bool b = _textureProtection.TryGetValue(this._glId, out Guid gid);
            return b && id.Equals(gid);
        }

        public void Bind() => GL.BindTexture(this._type, this._glId);

        public void SetFilterParameters(FilterParam min, FilterParam mag)
        {
            GL.TexParameter(this._type, TextureProperty.MinFilter, GLParamFromFilter(min));
            GL.TexParameter(this._type, TextureProperty.MagFilter, GLParamFromFilter(mag));
        }

        public void SetWrapParameters(WrapParam x, WrapParam y, WrapParam z)
        {
            GL.TexParameter(this._type, TextureProperty.WrapS, GLParamFromWrap(x));
            if (this._type is TextureTarget.Texture2D or TextureTarget.Texture2DArray)
            {
                GL.TexParameter(this._type, TextureProperty.WrapT, GLParamFromWrap(y));
            }

            if (this._type is TextureTarget.Texture3D)
            {
                GL.TexParameter(this._type, TextureProperty.WrapR, GLParamFromWrap(z));
            }
        }

        public unsafe Image<T> GetImage<T>(int level = 0) where T : unmanaged, IPixel<T>
        {
            int w = GL.GetTexLevelParameter(this._type, level, TextureLevelPropertyGetter.Width);
            int h = GL.GetTexLevelParameter(this._type, level, TextureLevelPropertyGetter.Height);
            Image<T> ret = new Image<T>(w, h);
            if (IntPtr.Size == 4 && ((long)sizeof(T) * w * h > int.MaxValue))
            {
                throw new Exception("Image too large for a 32bit process!");
            }

            T* data = MemoryHelper.Allocate<T>((nuint)(w * h));
            GL.GetTexImage(this._type, level, GetFormatFromPixelType(typeof(T)), PixelDataType.Byte, data);
            ret.ProcessPixelRows(x =>
            {
                for (int y = 0; y < x.Height; ++y)
                {
                    Span<T> rowSpan = x.GetRowSpan(y);
                    Span<T> memSpan = new Span<T>(data + (y * x.Width), x.Width);
                    memSpan.CopyTo(rowSpan);
                }
            });

            MemoryHelper.Free(data);
            return ret;
        }

        public unsafe void SetImage<T>(Image<T> img, SizedInternalFormat format, int level = 0, PixelDataType type = PixelDataType.Byte, TextureTarget tType = 0) where T : unmanaged, IPixel<T>
        {
            if (tType == 0)
            {
                tType = this._type;
            }

            this.Size = new Size(img.Width, img.Height);
            if (img.Configuration.PreferContiguousImageBuffers && img.DangerousTryGetSinglePixelMemory(out Memory<T> mem))
            {
                MemoryHandle mh = mem.Pin();
                GL.TexImage2D(tType, level, format, img.Width, img.Height, GetFormatFromPixelType(typeof(T)), type, new IntPtr(mh.Pointer));
                mh.Dispose();
                return;
            }

            if (img.Width * img.Height * sizeof(T) <= ImageMaximumContiguousMemoryAllowance)
            {
                T* pixels = MemoryHelper.Allocate<T>((nuint)(img.Width * img.Height));
                img.ProcessPixelRows(x =>
                {
                    for (int y = 0; y < x.Height; ++y)
                    {
                        Span<T> rowSpan = x.GetRowSpan(y);
                        fixed (void* span = rowSpan)
                        {
                            int spanLength = rowSpan.Length * sizeof(T);
                            Buffer.MemoryCopy(span, pixels + (y * x.Width), spanLength, spanLength);
                        }
                    }
                });

                GL.TexImage2D(tType, level, format, img.Width, img.Height, GetFormatFromPixelType(typeof(T)), type, (IntPtr)pixels);
                MemoryHelper.Free(pixels);
            }
            else
            {
                GL.TexImage2D(tType, level, format, img.Width, img.Height, GetFormatFromPixelType(typeof(T)), type, IntPtr.Zero);
                TextureTarget selfTT = tType;
                if (img.Height % 4 == 0) // Height is a multiple of 4, attempt to copy in blocks of 4 to support compression
                {
                    T* pixelBuffer = MemoryHelper.Allocate<T>((nuint)(img.Width * 4));
                    img.ProcessPixelRows(x =>
                    {
                        for (int y = 0; y < x.Height; ++y)
                        {
                            Span<T> rowSpan = x.GetRowSpan(y);
                            if (y != 0 && y % 4 == 0)
                            {
                                GL.TexSubImage2D(selfTT, level, 0, y - 4, x.Width, 4, GetFormatFromPixelType(typeof(T)), type, (IntPtr)pixelBuffer);
                            }

                            T* tOffsetB = pixelBuffer + (y % 4 * img.Width);
                            Span<T> s = new Span<T>(tOffsetB, img.Width);
                            rowSpan.CopyTo(s);
                        }

                        GL.TexSubImage2D(selfTT, level, 0, img.Height - 4, x.Width, 4, GetFormatFromPixelType(typeof(T)), type, (IntPtr)pixelBuffer);
                    });

                    MemoryHelper.Free(pixelBuffer);
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
        }

        public void GenerateMipMaps() => GL.GenerateMipmap(this._type);

        private static int GLParamFromFilter(FilterParam param)
        {
            return param switch
            {
                FilterParam.LinearMipmapLinear => (int)TextureMinFilter.LinearMipmapLinear,
                FilterParam.LinearMipmapNearest => (int)TextureMinFilter.LinearMipmapNearest,
                FilterParam.Linear => (int)TextureMinFilter.Linear,
                _ => (int)TextureMinFilter.Nearest
            };
        }

        private static int GLParamFromWrap(WrapParam param)
        {
            return param switch
            {
                WrapParam.Mirror => (int)TextureWrapMode.MirroredRepeat,
                WrapParam.ClampToBorder => (int)TextureWrapMode.BorderClamp,
                WrapParam.ClampToEdge => (int)TextureWrapMode.EdgeClamp,
                _ => (int)TextureWrapMode.Repeat,
            };
        }

        private static PixelDataFormat GetFormatFromPixelType(Type t)
        {
            return
                t == typeof(Rgba32)
                ? PixelDataFormat.Rgba
                : t == typeof(Rgba64)
                    ? PixelDataFormat.RgbaInteger
                    : t == typeof(Rgb24)
                        ? PixelDataFormat.Rgb
                        : t == typeof(RgbaVector) ? PixelDataFormat.Rgba : PixelDataFormat.DepthComponent;
        }

        public void Dispose()
        {
            if (this._glId > 0)
            {
                GL.DeleteTexture(this._glId);
                _textureProtection.TryRemove(this._glId, out _);
            }

            if (!IntPtr.Zero.Equals(this.AsyncFenceID))
            {
                unsafe
                {
                    GL.DeleteSync((void*)this.AsyncFenceID);
                }
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

    public enum AsyncLoadState
    {
        NonAsync = 0,
        Queued = 2,
        Processing = 3,
        Ready = 1
    }
}
