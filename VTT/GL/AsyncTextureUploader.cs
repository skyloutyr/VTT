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
    using VTT.Network;
    using VTT.Util;
    using OGL = OpenTK.Graphics.OpenGL.GL;

    public unsafe class AsyncTextureUploader
    {
        public const int MaxPBOSize = 1024 * 1024 * 1024;

        private readonly int _pbo;
        private int _pboSize;

        public AsyncTextureUploader(int nPixels = 2560000)
        {
            this._pbo = OGL.GenBuffer();
            this.CheckBufferSize(sizeof(Bgra32) * nPixels);
            new Thread(this.WorkSecondary) { IsBackground = true }.Start();
        }

        public void Kill() => this._shutdown = true;

        private void CheckBufferSize(int bytesNeeded)
        {
            if (bytesNeeded > this._pboSize)
            {
                this._pboSize = bytesNeeded;
                OGL.BindBuffer(OpenTK.Graphics.OpenGL.BufferTarget.PixelUnpackBuffer, this._pbo);
                OGL.BufferData(OpenTK.Graphics.OpenGL.BufferTarget.PixelUnpackBuffer, this._pboSize, IntPtr.Zero, OpenTK.Graphics.OpenGL.BufferUsageHint.DynamicDraw);
                OGL.BindBuffer(OpenTK.Graphics.OpenGL.BufferTarget.PixelUnpackBuffer, 0);
            }
        }

        private readonly EventWaitHandle _waitHandleSecondary = new EventWaitHandle(false, EventResetMode.ManualReset);
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

        public void ProcessPrimary()
        {
            if (!this._tasks.IsEmpty)
            {
                if (!this._hasWorkForSecondary && !this._hasWorkForPrimary && this._tasks.TryDequeue(out AsyncTextureUploadRequest request))
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
                    this.CheckBufferSize(neededBytes);
                    OGL.BindBuffer(OpenTK.Graphics.OpenGL.BufferTarget.PixelUnpackBuffer, this._pbo);
                    OGL.BufferData(OpenTK.Graphics.OpenGL.BufferTarget.PixelUnpackBuffer, this._pboSize, IntPtr.Zero, OpenTK.Graphics.OpenGL.BufferUsageHint.DynamicDraw);
                    this._mappedPtr = OGL.MapBuffer(OpenTK.Graphics.OpenGL.BufferTarget.PixelUnpackBuffer, OpenTK.Graphics.OpenGL.BufferAccess.WriteOnly);
                    if (this._mappedPtr.Equals(IntPtr.Zero))
                    {
                        Client.Instance.Settings.AsyncTextureUploading = false;
                        Client.Instance.Settings.Save();
                        throw new OutOfMemoryException("OpenGL (likely) ran out of memory for PBO allocation! Please disable async texture loading in the client config!");
                    }

                    OGL.BindBuffer(OpenTK.Graphics.OpenGL.BufferTarget.PixelUnpackBuffer, 0);
                    this._requestWorkedWith = request;
                    request.Texture.AsyncState = AsyncLoadState.Processing;
                    this._hasWorkForSecondary = true;
                }
            }

            if (this._hasWorkForPrimary)
            {
                OGL.BindBuffer(OpenTK.Graphics.OpenGL.BufferTarget.PixelUnpackBuffer, this._pbo);
                OGL.UnmapBuffer(OpenTK.Graphics.OpenGL.BufferTarget.PixelUnpackBuffer);
                this._texturesEnqueued.Remove(this._requestWorkedWith);
                if (this._requestWorkedWith.CheckTextureStatus())
                {
                    OGL.BindTexture(this._requestWorkedWith.Texture.Target, this._requestWorkedWith.Texture);
                    OGL.TexParameter(this._requestWorkedWith.Texture.Target, OpenTK.Graphics.OpenGL.TextureParameterName.TextureMaxLevel, this._requestWorkedWith.MipmapAmount - 1);
                    if (this._requestWorkedWith.DataType == AsyncTextureUploadRequest.ImageDataType.Image)
                    {
                        int bOffset = 0;
                        for (int j = 0; j < this._requestWorkedWith.MipmapAmount; ++j)
                        {
                            Size s = this._requestWorkedWith.MipmapSizes[j];
                            OGL.TexImage2D(this._requestWorkedWith.Texture.Target, j, this._requestWorkedWith.DesiredPixelFormat, s.Width, s.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.RgbaInteger, OpenTK.Graphics.OpenGL.PixelType.UnsignedByte, (IntPtr)bOffset);
                            bOffset += sizeof(Rgba32) * s.Width * s.Height;
                        }
                    }
                    else
                    {
                        int bOffset = 0;
                        for (int i = 0; i < this._requestWorkedWith.MipmapAmount; ++i)
                        {
                            Size s = this._requestWorkedWith.MipmapSizes[i];
                            OGL.CompressedTexImage2D(this._requestWorkedWith.Texture.Target, i, (OpenTK.Graphics.OpenGL.InternalFormat)this._requestWorkedWith.DesiredPixelFormat, s.Width, s.Height, 0, this._requestWorkedWith.CompressedData.dataLength[i], (IntPtr)bOffset);
                            bOffset += this._requestWorkedWith.CompressedData.dataLength[i];
                        }
                    }

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

                OGL.BindBuffer(OpenTK.Graphics.OpenGL.BufferTarget.PixelUnpackBuffer, 0);
            }

            this._waitHandleSecondary.Set();
        }

        public bool FireAsyncTextureUpload(Texture tex, Guid tId, OpenTK.Graphics.OpenGL.PixelInternalFormat pif, Image<Rgba32> img, int nMips, Action<AsyncTextureUploadRequest, bool> callback)
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

        public bool FireAsyncTextureUpload(Texture tex, Guid tId, OpenTK.Graphics.OpenGL.PixelInternalFormat pif, StbDxt.CompressedMipmapData compressedData, Action<AsyncTextureUploadRequest, bool> callback)
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
            if (desired <= 0)
            {
                desired = 1;
            }

            int i = 1;
            int m = Math.Min(imgSize.Width, imgSize.Height);
            int wS = imgSize.Width;
            int hS = imgSize.Height;
            neededBytes = 0;
            while (true)
            {
                if (m <= 4)
                {
                    break;
                }

                m >>= 1;
                i += 1;
            }

            imgSizes = new Size[i];
            for (int j = 0; j < i; ++j)
            {
                imgSizes[j] = new Size(wS, hS);
                neededBytes += sizeof(Rgba32) * wS * hS;
                wS /= 2;
                hS /= 2;
            }

            return Math.Min(desired, i);
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
        public OpenTK.Graphics.OpenGL.PixelInternalFormat DesiredPixelFormat { get; set; }
        public int MipmapAmount { get; set; }

        internal Size[] MipmapSizes { get; set; }
        internal int MipmapDataByteSize { get; set; }

        public bool CheckTextureStatus() => OGL.IsTexture(this.Texture) && this.Texture.CheckUniqueID(this.TextureID);

        public enum ImageDataType
        {
            Image,
            CompressedMips
        }
    }
}
