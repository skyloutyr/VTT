namespace VTT.Render
{
    using NLayer;
    using NVorbis;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Numerics;
    using System.Text;
    using System.Threading;
    using VTT.Asset;
    using VTT.Asset.Glb;
    using VTT.GL;
    using VTT.Network;
    using VTT.Sound;

    public class AsyncAssetLoader
    {
        private volatile int _requestsTotal;
        private volatile int _requestsCompleted;
        private volatile int _requestsQueued;
        private readonly object _lock = new object();

        public int RequestsTotal => this._requestsTotal;
        public int RequestsCompleted => this._requestsCompleted;
        public int RequestsQueued => this._requestsQueued;

        public delegate void AssetLoadCallback(AssetLoadResult result);

        public void EnqueueLoad(AssetLoadType type, string fsPath, AssetLoadCallback callback)
        {
            lock (this._lock)
            {
                if (this._requestsQueued == 0)
                {
                    this._requestsQueued = this._requestsTotal = 1;
                    this._requestsCompleted = 0;
                }
                else
                {
                    ++this._requestsQueued;
                    ++this._requestsTotal;
                }
            }

            AssetWorkload workload = new AssetWorkload(type, fsPath, callback);
            ThreadPool.QueueUserWorkItem(x => this.WorkAssetLoadGeneric(workload));
        }

        private void ReceiveModelGeneratedSignal(AssetWorkload workload, Image<Rgba32> preview, bool status)
        {
            if (status)
            {
                ThreadPool.QueueUserWorkItem(x => this.WorkModelLoadSecondary(workload, preview));
            }
            else
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorOpenGL, workload);
            }
        }

        private void WorkAssetLoadGeneric(AssetWorkload workload)
        {
            switch (workload.type)
            {
                case AssetLoadType.Model:
                {
                    this.WorkModelLoadInitial(workload); 
                    break;
                }

                case AssetLoadType.Texture:
                {
                    this.WorkImageLoad(workload); 
                    break;
                }

                case AssetLoadType.FragmentShader:
                {
                    this.WorkFragmentShaderLoad(workload);
                    break;
                }

                case AssetLoadType.Sound:
                {
                    this.WorkSoundLoad(workload); 
                    break;
                }

                case AssetLoadType.AnimatedTexture:
                {
                    this.WorkAnimationLoad(workload);
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        private void WorkSoundLoad(AssetWorkload workload)
        {
            if (!File.Exists(workload.fsPath))
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorNoFile, workload);
                return;
            }

            try
            {
                string ext = Path.GetExtension(workload.fsPath).ToLower();
                WaveAudio wa = null;
                Image<Rgba32> img = null;
                SoundData.Metadata meta = new SoundData.Metadata();
                if (ext.EndsWith("wav")) // Wave Sound
                {
                    wa = new WaveAudio();
                    wa.Load(File.OpenRead(workload.fsPath));
                }

                if (ext.EndsWith("mp3"))
                {
                    FileStream fs = File.OpenRead(workload.fsPath);
                    if (!WaveAudio.ValidateMPEGFrame(fs))
                    {
                        fs.Dispose();
                        this.NotifyClientOfFailure(AssetLoadStatus.ErrorInvalidMp3, workload);
                        return;
                    }
                    else
                    {
                        MpegFile mpeg = new MpegFile(fs);
                        wa = new WaveAudio(mpeg);
                        mpeg.Dispose();
                    }
                }

                if (ext.EndsWith("ogg"))
                {
                    VorbisReader vorbis = new VorbisReader(File.OpenRead(workload.fsPath));
                    wa = new WaveAudio(vorbis);
                    vorbis.Dispose();
                }

                img = wa.GenWaveForm(1024, 1024);
                bool doCompress = (Client.Instance.Frontend.FFmpegWrapper.IsInitialized &&
                    Client.Instance.Settings.SoundCompressionPolicy == ClientSettings.AudioCompressionPolicy.Always) ||
                    (Client.Instance.Settings.SoundCompressionPolicy == ClientSettings.AudioCompressionPolicy.LargeFilesOnly && wa.DataLength > 4194304); // 4Mb

                byte[] dataArray = null;
                long[] packetOffsets = null;
                bool wasCompressed = false;
                if (doCompress)
                {
                    lock (_ffmpegLock)
                    {
                        wasCompressed = wa.TryGetMpegEncodedData(out dataArray, out packetOffsets) && dataArray != null;
                    }
                }

                if (wasCompressed)
                {
                    meta.SoundType = SoundData.Metadata.StorageType.Mpeg;
                    meta.IsFullData = false;
                    meta.TotalChunks = packetOffsets.Length;
                    meta.CompressedChunkOffsets = packetOffsets;
                }
                else
                {
                    meta.SoundType = SoundData.Metadata.StorageType.Raw;
                    meta.IsFullData = wa.DataLength <= 4194304; // 4mb are allowed as raw
                    meta.TotalChunks = (int)Math.Ceiling((double)wa.DataLength / (wa.SampleRate * wa.NumChannels * 5)); // 5s audio buffers
                    meta.CompressedChunkOffsets = Array.Empty<long>();
                }

                meta.SampleRate = wa.SampleRate;
                meta.NumChannels = wa.NumChannels;
                meta.TotalDuration = wa.Duration;
                meta.SoundAssetName = Path.GetFileNameWithoutExtension(workload.fsPath);
                SoundData sound = new SoundData();
                sound.Meta = meta;

                Asset a = new Asset()
                {
                    ID = Guid.NewGuid(),
                    Sound = sound,
                    Type = AssetType.Sound
                };

                using MemoryStream ms = new MemoryStream();
                img.SaveAsPng(ms);
                img.Dispose();
                this.NotifyClientOfLoad(workload, new AssetLoadResult(AssetLoadStatus.Ok, ms.ToArray(), workload.fsPath, a, meta.SoundType == SoundData.Metadata.StorageType.Raw ? wa.GetManagedDataCopy() : dataArray, meta));
                wa.Free();
            }
            catch (IOException)
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorIO, workload);
            }
            catch (Exception)
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorGeneric, workload);
            }
        }

        private static readonly object _ffmpegLock = new object();
        private void WorkAnimationLoad(AssetWorkload workload)
        {
            if (!File.Exists(workload.fsPath))
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorNoFile, workload);
                return;
            }

            try
            {
                int i = 0;
                byte[] previewBinary = Array.Empty<byte>();
                List<TextureData.Frame> frames = new List<TextureData.Frame>();
                lock (_ffmpegLock)
                {
                    foreach (Image<Rgba32> img in Client.Instance.Frontend.FFmpegWrapper.DecodeAllFrames(workload.fsPath))
                    {
                        if (i == 0)
                        {
                            Image<Rgba32> preview = img.Clone();
                            preview.Mutate(x => x.Resize(256, 256));
                            using MemoryStream ms1 = new MemoryStream();
                            preview.SaveAsPng(ms1);
                            previewBinary = ms1.ToArray();
                            preview.Dispose();
                        }

                        MemoryStream ms = new MemoryStream();
                        img.SaveAsPng(ms);
                        byte[] imgBin = ms.ToArray();
                        ms.Dispose();
                        TextureData.Frame f = new TextureData.Frame(i, 1, false, imgBin);
                        frames.Add(f);
                        img.Dispose();
                        ++i;
                    }
                }

                TextureData ret = new TextureData()
                {
                    Meta = new TextureData.Metadata()
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

                Asset a = new Asset()
                {
                    ID = Guid.NewGuid(),
                    Texture = ret,
                    Type = AssetType.Texture
                };

                this.NotifyClientOfLoad(workload, new AssetLoadResult(AssetLoadStatus.Ok, previewBinary, workload.fsPath, a, a.ToBinary(ret.Write()), ret.Meta));
            }
            catch (ApplicationException)
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorFFMpeg, workload);
            }
            catch (IOException)
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorIO, workload);
            }
            catch (Exception)
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorGeneric, workload);
            }
        }

        private void WorkImageLoad(AssetWorkload workload)
        {
            if (!File.Exists(workload.fsPath))
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorNoFile, workload);
                return;
            }

            try
            {
                using Image<Rgba32> img = Image.Load<Rgba32>(workload.fsPath);
                using Image<Rgba32> preview = img.Clone();
                preview.Mutate(x => x.Resize(256, 256));
                using MemoryStream ms = new MemoryStream();
                preview.SaveAsPng(ms);
                preview.Dispose();
                Asset a = new Asset()
                {
                    ID = Guid.NewGuid(),
                    Texture = TextureData.CreateDefaultFromImage(img, out byte[] tdataBinary, out TextureData.Metadata meta),
                    Type = AssetType.Texture
                };

                this.NotifyClientOfLoad(workload, new AssetLoadResult(AssetLoadStatus.Ok, ms.ToArray(), workload.fsPath, a, a.ToBinary(tdataBinary), meta));
            }
            catch (IOException)
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorIO, workload);
            }
            catch (Exception)
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorGeneric, workload);
            }
        }

        private void WorkFragmentShaderLoad(AssetWorkload workload)
        {
            static bool IsSmallChar(char c) => c is ',' or ' ' or '.' or '\'' or '"' or ':' or ';' or '^' or '*' or '-' or '_' or '+' or '=';
            if (!File.Exists(workload.fsPath))
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorNoFile, workload);
                return;
            }
            try
            {
                string data = File.ReadAllText(workload.fsPath);
                string[] contents = data.Split('\n');
                Image<Rgba32> previewImg = new Image<Rgba32>(256, 256);
                for (int i = 0; i < 256 && i < contents.Length; ++i)
                {
                    string line = contents[i];
                    for (int x = 0; x < 256 && x < line.Length; ++x)
                    {
                        char c = line[x];
                        Rgba32 col = IsSmallChar(c) ? new Rgba32(0xff1f1f1f) : new Rgba32(0xfffafafa);
                        previewImg[x, i] = col;
                    }
                }

                using MemoryStream ms = new MemoryStream();
                previewImg.SaveAsPng(ms);
                previewImg.Dispose();
                Asset a = new Asset()
                {
                    ID = Guid.NewGuid(),
                    GlslFragment = new GlslFragmentData() { Data = data },
                    Type = AssetType.GlslFragmentShader
                };

                this.NotifyClientOfLoad(workload, new AssetLoadResult(AssetLoadStatus.Ok, ms.ToArray(), workload.fsPath, a, a.ToBinary(Encoding.UTF8.GetBytes(data)), null));
            }
            catch (IOException)
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorIO, workload);
            }
            catch (Exception)
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorGeneric, workload);
            }
        }

        private void WorkModelLoadInitial(AssetWorkload workload)
        {
            try
            {
                if (!File.Exists(workload.fsPath))
                {
                    this.NotifyClientOfFailure(AssetLoadStatus.ErrorNoFile, workload);
                    return;
                }

                byte[] binary = workload.binary = File.ReadAllBytes(workload.fsPath);
                using MemoryStream str = new MemoryStream(binary);
                GlbScene glbm = new GlbScene(new ModelData.Metadata() { CompressNormal = false, CompressAlbedo = false, CompressAOMR = false, CompressEmissive = false }, str);
                workload.passthroughData = glbm;
                Client.Instance.Frontend.EnqueueSpecializedTask(new Util.RepeatableActionContainer(() => CheckModelStatusAndGeneratePreview(workload, glbm)));
            }
            catch (IOException)
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorIO, workload);
                return;
            }
            catch (Exception)
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorGeneric, workload);
                return;
            }
        }

        private bool CheckModelStatusAndGeneratePreview(AssetWorkload workload, GlbScene scene)
        {
            if (scene.GlReady)
            {
                try
                {
                    Image<Rgba32> img = scene.CreatePreview(256, 256, new Vector4(0.39f, 0.39f, 0.39f, 1.0f));
                    this.ReceiveModelGeneratedSignal(workload, img, true);
                    return true;
                }
                catch
                {
                    this.ReceiveModelGeneratedSignal(workload, null, false);
                    try
                    {
                        scene?.Dispose();
                    }
                    catch
                    {
                        // NOOP
                    }

                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        private void WorkModelLoadSecondary(AssetWorkload workload, Image<Rgba32> preview)
        {
            try
            {
                using MemoryStream imgMs = new MemoryStream();
                preview.SaveAsPng(imgMs);
                Asset a = new Asset()
                {
                    ID = Guid.NewGuid(),
                    Model = new ModelData { GLMdl = (GlbScene)workload.passthroughData },
                    Type = AssetType.Model
                };

                this.NotifyClientOfLoad(workload, new AssetLoadResult(AssetLoadStatus.Ok, imgMs.ToArray(), workload.fsPath, a, a.ToBinary(workload.binary), null));
                ((GlbScene)workload.passthroughData).Dispose();
                preview.Dispose();
            }
            catch
            {
                this.NotifyClientOfFailure(AssetLoadStatus.ErrorGeneric, workload);
            }
        }

        private void NotifyClientOfFailure(AssetLoadStatus status, AssetWorkload workload) => this.NotifyClientOfLoad(workload, new AssetLoadResult(status, null, workload.fsPath, null, null, null));

        private void NotifyClientOfLoad(AssetWorkload workload, AssetLoadResult result)
        {
            lock (this._lock)
            {
                --this._requestsQueued;
                ++this._requestsCompleted;
            }

            Client.Instance.DoTask(() => workload.callback.Invoke(result));
        }

        private class AssetWorkload
        {
            public AssetLoadType type;
            public string fsPath;
            public AssetLoadCallback callback;
            public object passthroughData;
            public byte[] binary;

            public AssetWorkload(AssetLoadType type, string fsPath, AssetLoadCallback callback)
            {
                this.type = type;
                this.fsPath = fsPath;
                this.callback = callback;
            }
        }

        public class AssetLoadResult
        {
            public AssetLoadStatus Status { get; }
            public string FileSystemPath { get; }
            public Asset Asset { get; }
            public byte[] Preview { get; }
            public byte[] AssetBinary { get; }
            public object ExtraData { get; }

            public AssetLoadResult(AssetLoadStatus status, byte[] preview, string fileSystemPath, Asset asset, byte[] assetBinary, object extra)
            {
                this.Status = status;
                this.Preview = preview;
                this.FileSystemPath = fileSystemPath;
                this.Asset = asset;
                this.AssetBinary = assetBinary;
                this.ExtraData = extra;
            }

            public static implicit operator bool(AssetLoadResult self) => self.Status == AssetLoadStatus.Ok;
        }

        public enum AssetLoadStatus
        {
            Ok,
            ModelFirstStepOk,
            Aborted,
            ErrorGeneric,
            ErrorIO,
            ErrorNoFile,
            ErrorOpenGL,
            ErrorFFMpeg,
            ErrorNoFFMpeg,
            ErrorInvalidMp3
        }

        public enum AssetLoadType
        {
            Model,
            FragmentShader,
            Texture,
            Sound,
            AnimatedTexture
        }
    }
}
