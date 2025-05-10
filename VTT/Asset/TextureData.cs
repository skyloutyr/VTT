namespace VTT.Asset
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using VTT.Asset.Glb;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Util;

    public class TextureData : IAssetData
    {
        public Frame[] Frames { get; set; }
        public Metadata Meta { get; set; }

        private Texture _glTex;
        private TextureAnimation _cachedAnim = new TextureAnimation(null);

        public bool glReady;

        public GlbScene ToGlbModel() => new GlbScene(this);

        public static TextureData CreateDefaultFromImage(Image<Rgba32> clientImage, out byte[] selfBinary, out Metadata meta)
        {
            MemoryStream ms = new MemoryStream();
            clientImage.SaveAsPng(ms);
            byte[] imgBin = ms.ToArray();
            ms.Dispose();
            TextureData ret = new TextureData()
            {
                Meta = new Metadata()
                {
                    WrapS = WrapParam.Repeat,
                    WrapT = WrapParam.Repeat,
                    FilterMag = FilterParam.Linear,
                    FilterMin = FilterParam.LinearMipmapLinear,
                    EnableBlending = true,
                    Compress = true,
                    GammaCorrect = true,
                },

                Frames = new Frame[1] { new Frame(0, 1, false, imgBin) }
            };

            meta = ret.Meta;
            selfBinary = ret.Write();
            return ret;
        }

        public static TextureData CreateFromImageArray(IEnumerable<Image<Rgba32>> images, out byte[] selfBinary, out Metadata meta)
        {
            List<Frame> frames = new List<Frame>();
            int j = 0;
            foreach (Image<Rgba32> i in images)
            {
                MemoryStream ms = new MemoryStream();
                i.SaveAsPng(ms);
                byte[] imgBin = ms.ToArray();
                ms.Dispose();
                Frame f = new Frame(j++, 1, false, imgBin);
                frames.Add(f);
            }

            TextureData ret = new TextureData()
            {
                Meta = new Metadata()
                {
                    WrapS = WrapParam.Repeat,
                    WrapT = WrapParam.Repeat,
                    FilterMag = FilterParam.Linear,
                    FilterMin = FilterParam.LinearMipmapLinear,
                    EnableBlending = true,
                    Compress = true,
                    GammaCorrect = true,
                },

                Frames = frames.ToArray()
            };

            meta = ret.Meta;
            selfBinary = ret.Write();
            return ret;
        }

        public void Accept(byte[] binary)
        {
            using MemoryStream ms = new MemoryStream(binary);
            using BinaryReader br = new BinaryReader(ms);
            int version = br.ReadByte();
            switch (version)
            {
                case 0:
                {
                    this.Meta = new Metadata();
                    this.Meta.WrapS = (WrapParam)br.ReadInt32();
                    this.Meta.WrapT = (WrapParam)br.ReadInt32();
                    this.Meta.FilterMin = (FilterParam)br.ReadInt32();
                    this.Meta.FilterMag = (FilterParam)br.ReadInt32();
                    this.Meta.EnableBlending = br.ReadBoolean();
                    this.Meta.Compress = br.ReadBoolean();
                    this.Meta.GammaCorrect = br.ReadBoolean();
                    int nFrames = br.ReadInt32();
                    this.Frames = new Frame[nFrames];
                    for (int i = 0; i < nFrames; ++i)
                    {
                        int d = br.ReadInt32();
                        bool b = br.ReadBoolean();
                        int bLen = br.ReadInt32();
                        byte[] bin = br.ReadBytes(bLen);
                        this.Frames[i] = new Frame(i, d, b, bin);
                    }

                    break;
                }

                case 1:
                {
                    // On version 1 metadata migrated to AssetRef, no need to read it.
                    int nFrames = br.ReadInt32();
                    this.Frames = new Frame[nFrames];
                    for (int i = 0; i < nFrames; ++i)
                    {
                        int d = br.ReadInt32();
                        bool b = br.ReadBoolean();
                        int bLen = br.ReadInt32();
                        byte[] bin = br.ReadBytes(bLen);
                        this.Frames[i] = new Frame(i, d, b, bin);
                    }

                    break;
                }

                default:
                    break;
            }

            this.glReady = true;
        }

        public byte[] Write()
        {
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((byte)1);
            bw.Write(this.Frames.Length);
            foreach (Frame f in this.Frames)
            {
                bw.Write(f.Duration);
                bw.Write(f.Blend);
                bw.Write(f.ImageBinary.Length);
                bw.Write(f.ImageBinary);
            }

            return ms.ToArray();
        }

        public bool TryCopyGlTexture(SizedInternalFormat internalFormat, out Texture texOut)
        {
            Texture tex = this.GetOrCreateGLTexture(false, true, out _);
            if (tex.IsAsyncReady)
            {
                Texture ret = new Texture(TextureTarget.Texture2D);
                ret.Bind();
                if (internalFormat == SizedInternalFormat.Red8)
                {
                    internalFormat = this.Meta.Compress ? this.Meta.GammaCorrect ? SizedInternalFormat.CompressedSrgbAlphaBPTC : SizedInternalFormat.CompressedRgbaBPTC :
                        this.Meta.GammaCorrect ? SizedInternalFormat.Srgb8Alpha8 : SizedInternalFormat.Rgba8;
                }

                ret.SetFilterParameters(this.Meta.FilterMin, this.Meta.FilterMag);
                ret.SetWrapParameters(this.Meta.WrapS, this.Meta.WrapT, WrapParam.Repeat);
                tex.Bind();
                using Image<Rgba32> img = tex.GetImage<Rgba32>();
                ret.Bind();
                ret.SetImage(img, internalFormat);
                if (this.Meta.FilterMin is FilterParam.LinearMipmapLinear or FilterParam.LinearMipmapNearest)
                {
                    ret.GenerateMipMaps();
                }

                texOut = ret;
                return true;
            }

            texOut = null;
            return false;
        }

        public Texture GetOrCreateGLTexture(bool compoundOffscreen, bool forceSync, out TextureAnimation animationData)
        {
            if (this._glTex == null)
            {
                this._glTex = new Texture(TextureTarget.Texture2D);

                // We will at least notify others of (potential) async compression
                // If we determine the texture to be non-async it will be reset to non-async anyway
                this._glTex.AsyncState = AsyncLoadState.Queued;
                this._glTex.Bind();
                SizedInternalFormat pif =
                    this.Meta.Compress ?
                        this.Meta.GammaCorrect ? SizedInternalFormat.CompressedSrgbAlphaBPTC :
                    SizedInternalFormat.CompressedRgbaBPTC :
                this.Meta.GammaCorrect ? SizedInternalFormat.Srgb8Alpha8 :
                    SizedInternalFormat.Rgba8;

                this._glTex.SetFilterParameters(this.Meta.FilterMin, this.Meta.FilterMag);
                this._glTex.SetWrapParameters(this.Meta.WrapS, this.Meta.WrapT, WrapParam.Repeat);

                bool useDXTCompression = OpenGLUtil.UsingDXTCompression;
                bool useHWCompression = Client.Instance.Settings.AsyncDXTCompression;
                bool haveMips = this.Meta.FilterMin is FilterParam.LinearMipmapLinear or FilterParam.LinearMipmapNearest;
                Guid protectedID = this._glTex.GetUniqueID();

                void SetImage(Image<Rgba32> img)
                {
                    if (useDXTCompression && this.Meta.Compress && useHWCompression)
                    {
                        // Load DXT async.
                        ThreadPool.QueueUserWorkItem(x =>
                        {
                            StbDxt.CompressedMipmapData mipArray = StbDxt.CompressImageWithMipmaps(img, Client.Instance.Settings.MultithreadedTextureCompression, haveMips);
                            Size imgS = new Size(img.Width, img.Height);
                            img.Dispose();
                            Client.Instance.DoTask(() =>
                            {
                                Texture gTex = this._glTex;
                                if (gTex != null)
                                {
                                    // Do not need to query GL.IsTexture here as the ID protection system already takes care of that - disposed textures will drop their ID
                                    bool sameID = gTex.CheckUniqueID(protectedID); // ID protection system to prevent accidental texture overrides.
                                    if (sameID)
                                    {
                                        AsyncTextureUploader atu = Client.Instance.Frontend.TextureUploader;
                                        SizedInternalFormat glif = this.Meta.GammaCorrect ? SizedInternalFormat.CompressedSrgbAlphaS3TCDxt5Ext : SizedInternalFormat.CompressedRgbaS3TCDxt5Ext;
                                        if (!Client.Instance.Settings.AsyncTextureUploading || forceSync || !atu.FireAsyncTextureUpload(this._glTex, this._glTex.GetUniqueID(), glif, mipArray, (d, r) => mipArray.Free()))
                                        {
                                            gTex.AsyncState = AsyncLoadState.NonAsync;
                                            GL.BindTexture(TextureTarget.Texture2D, gTex);
                                            gTex.Size = imgS;
                                            int nMips = mipArray.numMips;
                                            if (nMips > 1)
                                            {
                                                GL.TexParameter(TextureTarget.Texture2D, TextureProperty.BaseLevel, 0);
                                                GL.TexParameter(TextureTarget.Texture2D, TextureProperty.MaxLevel, nMips - 1);
                                                for (int i = 0; i < nMips; ++i)
                                                {
                                                    Size mipSz = mipArray.sizes[i];
                                                    unsafe
                                                    {
                                                        GL.CompressedTexImage2D(TextureTarget.Texture2D, i, glif, mipSz.Width, mipSz.Height, mipArray.dataLength[i], (void*)mipArray.data[i]);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                unsafe
                                                {
                                                    GL.CompressedTexImage2D(TextureTarget.Texture2D, 0, glif, imgS.Width, imgS.Height, mipArray.dataLength[0], (void*)mipArray.data[0]);
                                                }
                                            }

                                            mipArray.Free();
                                        }
                                    }
                                    else
                                    {
                                        mipArray.Free();
                                    }
                                }
                                else
                                {
                                    mipArray.Free();
                                }
                            });
                        });

                        this._glTex.AsyncState = AsyncLoadState.Queued; // Assume texture is async for now, will be set to non-async later if it isn't
                    }
                    else
                    {
                        pif = OpenGLUtil.MapCompressedFormat(pif);
                        AsyncTextureUploader atu = Client.Instance.Frontend.TextureUploader;
                        if (!Client.Instance.Settings.AsyncTextureUploading || forceSync || !atu.FireAsyncTextureUpload(this._glTex, this._glTex.GetUniqueID(), pif, img, this.Meta.FilterMin is FilterParam.LinearMipmapLinear or FilterParam.LinearMipmapNearest ? int.MaxValue : 0, (d, r) => d.Image.Dispose()))
                        {
                            this._glTex.AsyncState = AsyncLoadState.NonAsync;
                            this._glTex.Bind();
                            this._glTex.SetImage(img, pif);
                            if ((this.Meta.FilterMin is FilterParam.LinearMipmapLinear or FilterParam.LinearMipmapNearest))
                            {
                                this._glTex.GenerateMipMaps();
                            }

                            img.Dispose();
                        }
                    }
                }

                if (compoundOffscreen && Client.Instance.Settings.AsyncTextureUploading)
                {
                    ThreadPool.QueueUserWorkItem(x =>
                    {
                        Image<Rgba32> img = this.CompoundImage();
                        Client.Instance.DoTask(() => SetImage(img));
                    });
                }
                else
                {
                    Image<Rgba32> img = this.CompoundImage();
                    SetImage(img);
                }
            }

            animationData = this._cachedAnim;
            return this._glTex;
        }

        public TextureAnimation CachedAnimation => this._cachedAnim;
        private Image<Rgba32> _cachedImage;

        public Image<Rgba32> CompoundAndCacheImage()
        {
            if (this._cachedImage == null)
            {
                this._cachedImage = this.CompoundImage();
            }

            return this._cachedImage;
        }

        public Image<Rgba32> CompoundImage()
        {
            TextureAnimation.Frame[] allFrames = new TextureAnimation.Frame[this.Frames.Length];
            Rectangle[] positions = new Rectangle[this.Frames.Length];
            int imgW = 0, imgH = 0;
            int cX = 0, cY = 0;
            int lH = 0;
            int maxS = Client.Instance.AssetManager.ClientAssetLibrary.GlMaxTextureSize;
            for (int i = 0; i < this.Frames.Length; ++i)
            {
                Frame f = this.Frames[i];
                ImageInfo ii = Image.Identify(f.ImageBinary);
                lH = Math.Max(ii.Height, lH);
                if (cX + ii.Width > maxS)
                {
                    imgW = Math.Max(cX, imgW);
                    imgH += lH;
                    cX = 0;
                    cY += lH;
                    lH = ii.Height;
                }

                positions[i] = new Rectangle(cX, cY, ii.Width, ii.Height);
                cX += ii.Width;
            }

            imgW = Math.Max(cX, imgW);
            imgH += lH;
            bool mayBeContinuous = ((long)imgW * (long)imgH * 4L) < int.MaxValue; // 32bpp
            Configuration cfg = Configuration.Default.Clone();
            cfg.PreferContiguousImageBuffers = mayBeContinuous;
            Image<Rgba32> img = new Image<Rgba32>(cfg, imgW, imgH);
            GraphicsOptions go = new GraphicsOptions() { Antialias = false, BlendPercentage = 1, ColorBlendingMode = PixelColorBlendingMode.Normal };
            
            static void CopyImageData(Image<Rgba32> src, Image<Rgba32> dst, int dstFromX, int dstFromY)
            {
                dst.ProcessPixelRows(src, (dstAccessor, srcAccessor) =>
                {
                    for (int y = 0; y < src.Height; ++y)
                    {
                        Span<Rgba32> dstSpan = dstAccessor.GetRowSpan(dstFromY + y);
                        Span<Rgba32> srcSpan = srcAccessor.GetRowSpan(y);
                        dstSpan = dstSpan.Slice(dstFromX, srcSpan.Length);
                        srcSpan.CopyTo(dstSpan);
                    }
                });
            }
            
            if (this.Frames.Length > 1)
            {
                Parallel.For(0, this.Frames.Length, i =>
                {
                    Image<Rgba32> f = Image.Load<Rgba32>(this.Frames[i].ImageBinary);
                    Rectangle r = positions[i];
                    allFrames[i] = new TextureAnimation.Frame() { Duration = (uint)this.Frames[i].Duration, Location = new RectangleF((float)r.X / img.Width, (float)r.Y / img.Height, (float)r.Width / img.Width, (float)r.Height / img.Height) };
                    CopyImageData(f, img, r.X, r.Y);
                    f.Dispose();
                    //this.Frames[i] = this.Frames[i].ClearBinary();
                });
            }
            else
            {
                if (this.Frames.Length == 1)
                {
                    Image<Rgba32> f = Image.Load<Rgba32>(this.Frames[0].ImageBinary);
                    Rectangle r = positions[0];
                    allFrames[0] = new TextureAnimation.Frame() { Duration = (uint)this.Frames[0].Duration, Location = new RectangleF(r.X / img.Width, r.Y / img.Height, r.Width / img.Width, r.Height / img.Height) };
                    CopyImageData(f, img, r.X, r.Y);
                    f.Dispose();
                }
            }

            this._cachedAnim.SetFrameData(allFrames);
            return img;
        }

        public void Dispose()
        {
            this._glTex?.Dispose();
            this._cachedAnim.SetFrameData(null);
            this._cachedImage?.Dispose();
        }

        public struct Frame
        {
            public int Idx { get; set; }
            public int Duration { get; set; }
            public bool Blend { get; set; }
            public byte[] ImageBinary { get; set; }

            public Frame(int idx, int duration, bool blend, byte[] imageBinary)
            {
                this.Idx = idx;
                this.Duration = duration;
                this.Blend = blend;
                this.ImageBinary = imageBinary;
            }

            public readonly Frame ClearBinary() => new Frame(this.Idx, this.Duration, this.Blend, null);
        }

        public class Metadata : ISerializable
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public WrapParam WrapS { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public WrapParam WrapT { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public FilterParam FilterMin { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public FilterParam FilterMag { get; set; }

            public bool EnableBlending { get; set; }
            public bool Compress { get; set; }
            public bool GammaCorrect { get; set; }
            public bool AlbedoIsEmissive { get; set; }

            public Metadata Copy() =>
                new Metadata()
                {
                    WrapS = this.WrapS,
                    WrapT = this.WrapT,
                    FilterMin = this.FilterMin,
                    FilterMag = this.FilterMag,
                    EnableBlending = this.EnableBlending,
                    Compress = this.Compress,
                    GammaCorrect = this.GammaCorrect,
                    AlbedoIsEmissive = this.AlbedoIsEmissive,
                };

            public void Deserialize(DataElement e)
            {
                this.WrapS = e.GetEnum<WrapParam>("WrapS");
                this.WrapT = e.GetEnum<WrapParam>("WrapT");
                this.FilterMin = e.GetEnum<FilterParam>("FilterMin");
                this.FilterMag = e.GetEnum<FilterParam>("FilterMag");
                this.EnableBlending = e.GetBool("Blend");
                this.Compress = e.GetBool("Compress");
                this.GammaCorrect = e.GetBool("Gamma");
                this.AlbedoIsEmissive = e.GetBool("A2E", false);
            }

            public DataElement Serialize()
            {
                DataElement ret = new DataElement();
                ret.SetEnum("WrapS", this.WrapS);
                ret.SetEnum("WrapT", this.WrapT);
                ret.SetEnum("FilterMin", this.FilterMin);
                ret.SetEnum("FilterMag", this.FilterMag);
                ret.SetBool("Blend", this.EnableBlending);
                ret.SetBool("Compress", this.Compress);
                ret.SetBool("Gamma", this.GammaCorrect);
                ret.SetBool("A2E", this.AlbedoIsEmissive);
                return ret;
            }
        }
    }
}
