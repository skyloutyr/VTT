namespace VTT.GL
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Util;
    using OGL = Bindings.GL;

    public unsafe class AsyncTextureUploader
    {
        public const int MaxPBOSize = 1024 * 1024 * 1024;

        private readonly uint[] _pbo;
        private readonly int[] _pboSize = new int[3];
        private int _pboIndex;

        public AsyncTextureUploader(int nPixels = 256)
        {
            this._pbo = OGL.GenBuffers(3).ToArray();
            this.CheckBufferSize(sizeof(Bgra32) * nPixels, 0);
            this.CheckBufferSize(sizeof(Bgra32) * nPixels, 1);
            this.CheckBufferSize(sizeof(Bgra32) * nPixels, 2);
            OpenGLUtil.NameObject(GLObjectType.Buffer, this._pbo[0], "Async texture uploader unpack pbo 0");
            OpenGLUtil.NameObject(GLObjectType.Buffer, this._pbo[1], "Async texture uploader unpack pbo 1");
            OpenGLUtil.NameObject(GLObjectType.Buffer, this._pbo[2], "Async texture uploader unpack pbo 2");
            new Thread(this.WorkSecondary) { IsBackground = true }.Start();
        }

        public void Kill() => this._shutdown = true;

        private void CheckBufferSize(int bytesNeeded, int index = -1)
        {
            bool doNotWarn = true;
            if (index == -1)
            {
                index = this._pboIndex;
                doNotWarn = false;
            }

            if (bytesNeeded > this._pboSize[index])
            {
                if (!doNotWarn)
                {
                    Client.Instance.Logger.Log(LogLevel.Warn, $"Async Texture Uploader had to resize the PBO {index} from {ConvertBytesToText(this._pboSize[index])} to {ConvertBytesToText(bytesNeeded)}!");
                }

                this._pboSize[index] = bytesNeeded;
                OGL.BindBuffer(BufferTarget.PixelUnpack, this._pbo[index]);
                OGL.BufferData(BufferTarget.PixelUnpack, this._pboSize[index], IntPtr.Zero, BufferUsage.DynamicDraw);
                OGL.BindBuffer(BufferTarget.PixelUnpack, 0);
            }
        }

        private static readonly string[] suffixes = { 
            "B",
            "KB",
            "MB",
            "GB",
            "impossible value"
        };

        private static string ConvertBytesToText(int nBytes)
        {
            string suffix = "B";
            if (nBytes < 1024)
            {
                return nBytes.ToString() + suffix;
            }

            float v = nBytes;
            int i = 0;
            while (v > 1024)
            {
                v = v / 1024;
                suffix = suffixes[++i];
            }

            return v.ToString("0.00") + suffix;
        }

        private readonly EventWaitHandle _waitHandleSecondary = new EventWaitHandle(false, EventResetMode.AutoReset);
        private volatile bool _shutdown;

        private void WorkSecondary()
        {
            while (!this._shutdown)
            {
                this._waitHandleSecondary.WaitOne();
                this.ProcessSecondary();
            }
        }

        private readonly ConcurrentQueue<AsyncTextureUploadRequest> _tasks = new ConcurrentQueue<AsyncTextureUploadRequest>();
        private volatile bool _hasWorkForPrimary;
        private volatile bool _hasWorkForSecondary;
        private volatile IntPtr _mappedPtr;
        private AsyncTextureUploadRequest _requestWorkedWith;
        private readonly List<AsyncTextureUploadRequest> _texturesEnqueued = new List<AsyncTextureUploadRequest>();

        public void ProcessSecondary()
        {
            if (this._hasWorkForSecondary) // Assume buffer is mapped here
            {
                int numMips = this._requestWorkedWith.MipmapAmount;
                Size[] szs = this._requestWorkedWith.MipmapSizes;
                if (this._requestWorkedWith.DataType == AsyncTextureUploadRequest.ImageDataType.Image)
                {
                    Rgba32* pixelPointer = (Rgba32*)this._mappedPtr;
                    int rgba32Offset = 0;
                    for (int i = 0; i < numMips; ++i)
                    {
                        Image<Rgba32> limg = i == 0
                            ? this._requestWorkedWith.Image
                            : this._requestWorkedWith.Image.Clone(x => x.Resize(szs[i], KnownResamplers.Triangle, true));

                        int ppr = limg.Width;
                        limg.ProcessPixelRows(x =>
                        {
                            for (int j = 0; j < limg.Height; ++j)
                            {
                                Span<Rgba32> span = x.GetRowSpan(j);
                                fixed (Rgba32* ptr = span)
                                {
                                    int sz = sizeof(Rgba32) * span.Length;
                                    Buffer.MemoryCopy(ptr, pixelPointer + rgba32Offset + (ppr * j), sz, sz);
                                }
                            }
                        });

                        rgba32Offset += szs[i].Width * szs[i].Height;
                        if (i != 0)
                        {
                            limg.Dispose();
                        }
                    }
                }
                else
                {
                    int ptrOffset = 0;
                    for (int i = 0; i < numMips; ++i)
                    {
                        Buffer.MemoryCopy((void*)this._requestWorkedWith.CompressedData.data[i], (void*)(this._mappedPtr + ptrOffset), this._requestWorkedWith.CompressedData.dataLength[i], this._requestWorkedWith.CompressedData.dataLength[i]);
                        ptrOffset += this._requestWorkedWith.CompressedData.dataLength[i];
                    }
                }

                this._hasWorkForPrimary = true;
                this._hasWorkForSecondary = false;
            }
        }

        private readonly (Texture, Guid)[] _pboTextures = new (Texture, Guid)[3];
        private bool IsNextPBOAvailable()
        {
            (Texture, Guid) checkedTex = this._pboTextures[this._pboIndex];
            return checkedTex.Item1 == null || !checkedTex.Item1.CheckUniqueID(checkedTex.Item2) || checkedTex.Item1.IsAsyncReady;
        }

        public void ProcessPrimary()
        {
            if (!this._tasks.IsEmpty)
            {
                if (!this._hasWorkForSecondary && !this._hasWorkForPrimary && this.IsNextPBOAvailable() && this._tasks.TryDequeue(out AsyncTextureUploadRequest request))
                {
                    int actualMips;
                    Size[] imgSizes;
                    int neededBytes;
                    if (request.DataType == AsyncTextureUploadRequest.ImageDataType.Image)
                    {
                        actualMips = this.DeterminePossibleMipMaps(request.Image.Size, request.MipmapAmount, out imgSizes, out neededBytes);
                    }
                    else
                    {
                        actualMips = request.CompressedData.numMips;
                        imgSizes = request.CompressedData.sizes;
                        neededBytes = request.CompressedData.dataLength.Sum();
                    }

                    request.MipmapAmount = actualMips;
                    request.MipmapSizes = imgSizes;
                    request.MipmapDataByteSize = neededBytes;
                    request.PBOIndex = this._pboIndex;
                    this.CheckBufferSize(neededBytes);
                    OGL.BindBuffer(BufferTarget.PixelUnpack, this._pbo[this._pboIndex]);
                    this._mappedPtr = (IntPtr)OGL.MapBufferRange(BufferTarget.PixelUnpack, 0, this._pboSize[this._pboIndex], BufferRangeAccessMask.Write | BufferRangeAccessMask.Unsynchronized | BufferRangeAccessMask.InvalidateBuffer); 
                    if (this._mappedPtr.Equals(IntPtr.Zero))
                    {
                        Client.Instance.Settings.AsyncTextureUploading = false;
                        Client.Instance.Settings.Save();
                        throw new OutOfMemoryException("OpenGL (likely) ran out of memory for PBO allocation! Please disable async texture loading in the client config!");
                    }

                    OGL.BindBuffer(BufferTarget.PixelUnpack, 0);
                    this._requestWorkedWith = request;
                    request.Texture.AsyncState = AsyncLoadState.Processing;
                    this._pboTextures[this._pboIndex] = (request.Texture, request.Texture.GetUniqueID());
                    this._hasWorkForSecondary = true;
                    this._waitHandleSecondary.Set();
                    this._pboIndex = Client.Instance.Settings.NumAsyncTextureBuffers == 1 ? 0 : (this._pboIndex + 1) % Client.Instance.Settings.NumAsyncTextureBuffers;
                }
            }

            if (this._hasWorkForPrimary)
            {
                OGL.BindBuffer(BufferTarget.PixelUnpack, this._pbo[this._requestWorkedWith.PBOIndex]);
                OGL.UnmapBuffer(BufferTarget.PixelUnpack);
                this._texturesEnqueued.Remove(this._requestWorkedWith);
                if (this._requestWorkedWith.CheckTextureStatus())
                {
                    OGL.BindTexture(this._requestWorkedWith.Texture.Target, this._requestWorkedWith.Texture);
                    OGL.TexParameter(this._requestWorkedWith.Texture.Target, TextureProperty.MaxLevel, this._requestWorkedWith.MipmapAmount - 1);
                    this._requestWorkedWith.Texture.Size = this._requestWorkedWith.MipmapSizes[0];
                    if (this._requestWorkedWith.DataType == AsyncTextureUploadRequest.ImageDataType.Image)
                    {
                        int bOffset = 0;
                        for (int j = 0; j < this._requestWorkedWith.MipmapAmount; ++j)
                        {
                            Size s = this._requestWorkedWith.MipmapSizes[j];
                            OGL.TexImage2D(this._requestWorkedWith.Texture.Target, j, this._requestWorkedWith.DesiredPixelFormat, s.Width, s.Height, PixelDataFormat.Rgba, PixelDataType.Byte, (IntPtr)bOffset);
                            bOffset += sizeof(Rgba32) * s.Width * s.Height;
                        }
                    }
                    else
                    {
                        int bOffset = 0;
                        for (int i = 0; i < this._requestWorkedWith.MipmapAmount; ++i)
                        {
                            Size s = this._requestWorkedWith.MipmapSizes[i];
                            OGL.CompressedTexImage2D(this._requestWorkedWith.Texture.Target, i, this._requestWorkedWith.DesiredPixelFormat, s.Width, s.Height, this._requestWorkedWith.CompressedData.dataLength[i], (void*)bOffset);
                            bOffset += this._requestWorkedWith.CompressedData.dataLength[i];
                        }
                    }

                    this._requestWorkedWith.Texture.AsyncFenceID = (IntPtr)OGL.GenFenceSync();
                    this._requestWorkedWith.Texture.AsyncState = AsyncLoadState.Ready;
                    this._requestWorkedWith.UploadCallback?.Invoke(this._requestWorkedWith, true);
                    this._requestWorkedWith = null;
                    this._hasWorkForPrimary = false;
                }
                else // Not texture, bad data
                {
                    // Invoke the callback for cleanup either way
                    this._requestWorkedWith.UploadCallback?.Invoke(this._requestWorkedWith, false);
                    this._requestWorkedWith = null;
                    this._hasWorkForPrimary = false;
                }

                OGL.BindBuffer(BufferTarget.PixelUnpack, 0);
            }
        }

        public bool FireAsyncTextureUpload(Texture tex, Guid tId, SizedInternalFormat pif, Image<Rgba32> img, int nMips, Action<AsyncTextureUploadRequest, bool> callback)
        {
            this.DeterminePossibleMipMaps(img.Size, nMips, out _, out int nBts);
            if (nBts > MaxPBOSize)
            {
                return false;
            }

            if (!this._texturesEnqueued.Any(t => t.TextureID.Equals(tId)))
            {
                AsyncTextureUploadRequest atur = new AsyncTextureUploadRequest()
                {
                    CompressedData = null,
                    DataType = AsyncTextureUploadRequest.ImageDataType.Image,
                    DesiredPixelFormat = pif,
                    Image = img,
                    MipmapAmount = nMips,
                    Texture = tex,
                    TextureID = tId,
                    UploadCallback = callback
                };

                tex.AsyncState = AsyncLoadState.Queued;
                this._texturesEnqueued.Add(atur);
                this._tasks.Enqueue(atur);
            }


            return true;
        }

        public bool FireAsyncTextureUpload(Texture tex, Guid tId, SizedInternalFormat pif, StbDxt.CompressedMipmapData compressedData, Action<AsyncTextureUploadRequest, bool> callback)
        {
            if (compressedData.dataLength.Sum() > MaxPBOSize)
            {
                return false;
            }

            if (!this._texturesEnqueued.Any(t => t.TextureID.Equals(tId)))
            {
                AsyncTextureUploadRequest atur = new AsyncTextureUploadRequest()
                {
                    CompressedData = compressedData,
                    Image = null,
                    DataType = AsyncTextureUploadRequest.ImageDataType.CompressedMips,
                    DesiredPixelFormat = pif,
                    MipmapAmount = compressedData.numMips,
                    Texture = tex,
                    TextureID = tId,
                    UploadCallback = callback
                };

                tex.AsyncState = AsyncLoadState.Queued;
                this._texturesEnqueued.Add(atur);
                this._tasks.Enqueue(atur);
            }


            return true;
        }

        private int DeterminePossibleMipMaps(Size imgSize, int desired, out Size[] imgSizes, out int neededBytes)
        {
            desired = Math.Max(1, desired);
            neededBytes = 0;
            int nMips = Math.Min(desired, OpenGLUtil.GetMaxMipmapAmount(imgSize));
            imgSizes = new Size[nMips];
            for (int j = 0; j < nMips; ++j)
            {
                Size sz = imgSizes[j] = OpenGLUtil.GetMipmapSize(imgSize, j);
                neededBytes += sizeof(Rgba32) * sz.Width * sz.Height;
            }

            return nMips;
        }
    }

    public class AsyncTextureUploadRequest
    {
        public Guid TextureID { get; set; }
        public Texture Texture { get; set; }
        public Action<AsyncTextureUploadRequest, bool> UploadCallback { get; set; }
        public Image<Rgba32> Image { get; set; }
        public StbDxt.CompressedMipmapData CompressedData { get; set; }
        public ImageDataType DataType { get; set; }
        public SizedInternalFormat DesiredPixelFormat { get; set; }
        public int MipmapAmount { get; set; }

        internal Size[] MipmapSizes { get; set; }
        internal int MipmapDataByteSize { get; set; }
        internal int PBOIndex { get; set; }

        public bool CheckTextureStatus() => OGL.IsTexture(this.Texture) && this.Texture.CheckUniqueID(this.TextureID);

        public enum ImageDataType
        {
            Image,
            CompressedMips
        }
    }
}
