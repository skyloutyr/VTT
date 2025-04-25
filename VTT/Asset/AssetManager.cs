namespace VTT.Asset
{
    using Newtonsoft.Json;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Formats.Gif;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Numerics;
    using System.Threading;
    using System.Threading.Tasks;
    using VTT.Asset.Glb;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public class AssetManager
    {
        public Dictionary<Guid, AssetRef> Refs { get; } = new Dictionary<Guid, AssetRef>();

        public ClientAssetLibrary ClientAssetLibrary { get; } = new ClientAssetLibrary();
        public AssetBinaryCache ServerAssetCache { get; }
        public AssetSoundHeatmap ServerSoundHeatmap { get; }

        public bool IsServer { get; set; }

        public AssetDirectory Root { get; } = new AssetDirectory() { Name = "root" };

        public AssetManager()
        {
            this.ClientAssetLibrary.Container = this;
            this.ServerAssetCache = new AssetBinaryCache(this);
            this.ServerSoundHeatmap = new AssetSoundHeatmap(this);
        }

        public string GetFSPath(AssetDirectory dir)
        {
            string ret = "";
            if (!this.IsServer)
            {
                return ret;
            }

            while (dir != this.Root)
            {
                ret = Path.Combine(dir.Name, ret);
                dir = dir.Parent;
            }

            ret = Path.Combine(IOVTT.ServerDir, "Assets", ret);
            return ret;
        }

        public AssetDirectory GetDirAt(string path)
        {
            string[] elements = path.Split('/');
            AssetDirectory d = this.Root;
            if (elements.Length <= 1)
            {
                return d;
            }

            for (int i = 1; i < elements.Length; ++i)
            {
                string g = elements[i];
                if (string.IsNullOrEmpty(g))
                {
                    continue;
                }

                bool havead = false;
                foreach (AssetDirectory ad in d.Directories)
                {
                    if (ad.Name.Equals(g))
                    {
                        d = ad;
                        havead = true;
                        break;
                    }
                }

                if (!havead) // No such directory exists
                {
                    return d;
                }
            }

            return d;
        }

        public AssetRef FindRefForAsset(Asset asset) => this.Refs[asset.ID];

        public AssetDirectory FindDirForRef(Guid refID) => this.InternalFindDirForRefRecursive(this.Root, refID);

        private AssetDirectory InternalFindDirForRefRecursive(AssetDirectory self, Guid refID)
        {
            foreach (AssetRef r in self.Refs)
            {
                if (r.AssetID.Equals(refID))
                {
                    return self;
                }
            }

            foreach (AssetDirectory d in self.Directories)
            {
                AssetDirectory r = this.InternalFindDirForRefRecursive(d, refID);
                if (r != null)
                {
                    return r;
                }
            }

            return null;
        }

        public void RecursivelyDeleteDirectory(AssetDirectory dir)
        {
            for (int i = dir.Refs.Count - 1; i >= 0; i--)
            {
                AssetRef aRef = dir.Refs[i];
                this.Refs.Remove(aRef.AssetID);
            }

            for (int i = dir.Directories.Count - 1; i >= 0; i--)
            {
                AssetDirectory d = dir.Directories[i];
                this.RecursivelyDeleteDirectory(d);
            }

            dir.Parent.Directories.Remove(dir);
            string fPath = this.GetFSPath(dir);
            if (this.IsServer && Directory.Exists(fPath))
            {
                Directory.Delete(this.GetFSPath(dir), true);
            }
        }

        public void RecursivelyPopulateRefs(AssetDirectory dir)
        {
            foreach (AssetRef aRef in dir.Refs)
            {
                this.Refs[aRef.AssetID] = aRef;
            }

            foreach (AssetDirectory d in dir.Directories)
            {
                this.RecursivelyPopulateRefs(d);
            }
        }

        public void Load() // Load on server side
        {
            string assetDir = Path.Combine(IOVTT.ServerDir, "Assets");
            List<(AssetDirectory assetDir, string file)> paths = new List<(AssetDirectory assetDir, string file)>(4096);
            this.ScanServerDir(assetDir, this.Root, paths);
            object localRefLock = new object();
            Parallel.ForEach(paths, x =>
            {
                Guid aId = Guid.Parse(Path.GetFileNameWithoutExtension(x.file));
                if (AssetBinaryPointer.ReadAssetMetadata(x.file, out AssetMetadata meta))
                {
                    AssetBinaryPointer abp = new AssetBinaryPointer() { FileLocation = x.file, PreviewPointer = aId };
                    AssetRef aRef = new AssetRef() { AssetID = aId, AssetPreviewID = aId, IsServer = true, ServerPointer = abp, Meta = meta };
                    if (aRef.Meta != null && aRef.Meta.Type == AssetType.Sound)
                    {
                        if (!string.Equals(aRef.Meta.SoundInfo.SoundAssetName, aRef.Name))
                        {
                            aRef.Meta.SoundInfo.SoundAssetName = aRef.Name;
                            File.WriteAllText(aRef.ServerPointer.FileLocation + ".json", JsonConvert.SerializeObject(aRef.Meta));
                        }
                    }

                    lock (x.assetDir.@lock)
                    {
                        x.assetDir.Refs.Add(aRef);
                    }

                    lock (localRefLock)
                    {
                        this.Refs.Add(aId, aRef);
                    }
                }
            });
        }

        public WaitHandle LoadAsync()
        {
            ManualResetEvent evt = new ManualResetEvent(false);
            ThreadPool.QueueUserWorkItem(x =>
            {
                this.Load();
                ((ManualResetEvent)x).Set();
            }, evt);

            return evt;
        }

        public void ScanServerDir(string dir, AssetDirectory assetDir, List<(AssetDirectory assetDir, string file)> paths) // Recursive directory iteration
        {
            Directory.CreateDirectory(dir);
            foreach (string file in Directory.EnumerateFiles(dir)) // List all files
            {
                if (file.EndsWith(".ab")) // Have asset def!
                {
                    Server.Instance.Logger.Log(LogLevel.Info, "Found asset candidate at " + file);
                    paths.Add((assetDir, file));
                }
            }

            foreach (string subdir in Directory.EnumerateDirectories(dir))
            {
                Server.Instance.Logger.Log(LogLevel.Info, "Found asset subdirectory at " + subdir);
                AssetDirectory subDir = new AssetDirectory() { Name = Path.GetFileName(subdir) };
                this.ScanServerDir(subdir, subDir, paths);
                subDir.Parent = assetDir;
                assetDir.Directories.Add(subDir);
            }
        }

        public byte[] GetServerPreview(Guid ptr)
        {
            string previewDir = Path.Combine(IOVTT.ServerDir, "Previews");
            Directory.CreateDirectory(previewDir);
            return File.ReadAllBytes(Path.Combine(previewDir, ptr.ToString() + ".png"));
        }
    }

    public class ClientAssetLibrary
    {
        public const long AssetAwaitTime = 30000;

        public AssetManager Container { get; set; }

        public AssetLibrary<Guid, Asset> Assets { get; }
        public AssetLibrary<Guid, AssetPreview> Previews { get; }
        public AssetLibrary<Guid, AssetPreview> Portraits { get; }
        public AssetLibrary<string, AssetPreview> WebPictures { get; }

        public int GlMaxTextureSize { get; set; }

        public ClientAssetLibrary()
        {
            this.Assets = new AssetLibrary<Guid, Asset>()
            {
                ValueValidator = this.ValidateAsset,
                RequestGenerator = this.RequestAsset,
                DataParser = this.ReceiveAsset,
                ValueCleaner = this.ClearAsset,
                ValueEraser = this.EraseAsset,
            };

            this.Previews = new AssetLibrary<Guid, AssetPreview>()
            { 
                RequestGenerator = this.RequestPreview,
                DataParser = this.ReceivePreview,
                ValueCleaner = this.ClearPreview,
            };

            this.Portraits = new AssetLibrary<Guid, AssetPreview>()
            {
                CustomValueGetter = this.GetPortrait,
                ValueCleaner = this.ClearPreview
            };

            this.WebPictures = new AssetLibrary<string, AssetPreview>()
            {
                RequestGenerator = this.RequestWebImage,
                DataParser = this.ReceiveWebImage,
                ValueCleaner = this.ClearWebImage
            };
        }

        #region Asset Handling
        private void ValidateAsset(Guid id, AssetType requestedType, Asset a)
        {
            if (a.Type == AssetType.Texture && requestedType == AssetType.Model && a.Model == null && a.Texture != null && a.Texture.glReady && Client.Instance.Frontend.CheckThread())
            {
                GlbScene mdl = a.Texture.ToGlbModel();
                a.Model = new ModelData() { GLMdl = mdl };
            }
        }

        private void RequestAsset(Guid id, AssetType at) => new PacketAssetRequest() { AssetID = id, AssetType = at }.Send();

        private AssetStatus ReceiveAsset(Guid id, AssetType type, AssetResponseType response, byte[] bin, AssetMetadata meta, out Asset val)
        {
            Asset a = val = new Asset() { ID = id, Type = type };
            ThreadPool.QueueUserWorkItem(x =>
            {
                byte[] rawBinary = AssetBinaryPointer.GetRawAssetBinary(bin);
                switch (type)
                {
                    case AssetType.Texture:
                    {
                        a.Texture = new TextureData();
                        a.Texture.Accept(rawBinary);
                        a.Texture.Meta = meta.TextureInfo;
                        break;
                    }

                    case AssetType.Model:
                    {
                        a.Model = new ModelData();
                        a.Model.Meta = meta.ModelInfo;
                        a.Model.Accept(rawBinary);
                        break;
                    }

                    case AssetType.Shader:
                    {
                        a.Shader = new ShaderData();
                        a.Shader.Accept(rawBinary);
                        break;
                    }

                    case AssetType.GlslFragmentShader:
                    {
                        a.GlslFragment = new GlslFragmentData();
                        a.GlslFragment.Accept(rawBinary);
                        break;
                    }

                    case AssetType.Sound:
                    {
                        a.Sound = new SoundData();
                        a.Sound.Meta = meta.SoundInfo;
                        a.Sound.Accept(rawBinary);
                        break;
                    }

                    case AssetType.ParticleSystem:
                    {
                        if (a.ParticleSystem == null)
                        {
                            a.ParticleSystem = new ParticleSystem();
                        }

                        using MemoryStream ms = new MemoryStream(rawBinary);
                        using BinaryReader br = new BinaryReader(ms);
                        if (meta.Version == 2)
                        {
                            a.ParticleSystem.ReadV2(br);
                        }
                        else
                        {
                            a.ParticleSystem.ReadV1(br);
                        }

                        break;
                    }
                }
            });

            return AssetStatus.Return;
        }

        private void ClearAsset(Guid id, Asset a) => a?.Dispose();

        private void EraseAsset(Guid id)
        {
            this.Previews.EraseRecord(id);
            this.Portraits.EraseRecord(id);
        }

        #endregion

        #region Preview Handling
        private void RequestPreview(Guid id, AssetType at)
        {
            if (this.Assets.GetStatus(id, out AssetStatus status) && status is AssetStatus.Error or AssetStatus.NoAsset)
            {
                this.Previews.Receive(id, at, status == AssetStatus.Error ? AssetResponseType.InternalError : AssetResponseType.NoAsset, null, null);
            }
            else
            {
                new PacketAssetPreview() { ID = id }.Send();
            }
        }

        private AssetStatus ReceivePreview(Guid id, AssetType type, AssetResponseType response, byte[] bin, AssetMetadata meta, out AssetPreview preview)
        {
            if (response != AssetResponseType.Ok)
            {
                preview = null;
                return response == AssetResponseType.InternalError ? AssetStatus.Error : AssetStatus.NoAsset;
            }

            preview = new AssetPreview() { Data = bin };
            return AssetStatus.Return;
        }

        private void ClearPreview(Guid id, AssetPreview preview) => preview.Free();

        #endregion

        #region Portrait Handling
        private bool GetPortrait(Guid id, AssetType type, Dictionary<Guid, AssetPreview> values, Dictionary<Guid, AssetStatus> statuses, Action<Guid, AssetType> requesterDelegate, out AssetStatus status, out AssetPreview value)
        {
            if (statuses.TryGetValue(id, out AssetStatus cStat))
            {
                if (cStat is AssetStatus.Error or AssetStatus.NoAsset)
                {
                    status = cStat;
                    value = null;
                    return true;
                }
            }

            AssetStatus assetStatus = this.Assets.Get(id, AssetType.Model, out Asset a);
            switch (assetStatus)
            {
                case AssetStatus.Error:
                {
                    statuses[id] = status = AssetStatus.Error;
                    value = null;
                    return true;
                }

                case AssetStatus.NoAsset:
                {
                    statuses[id] = status = AssetStatus.NoAsset;
                    value = null;
                    return true;
                }

                case AssetStatus.Return:
                {
                    if (a != null)
                    {
                        if (a.Type == AssetType.Texture && (a?.Texture?.glReady ?? false))
                        {
                            // Safe to do as CopyGlTexture will call GetOrCreateGLTexture anyway. GetOrCreate will not queue multiple times if texture is not ready
                            Texture t1 = a.Texture.GetOrCreateGLTexture(false, out TextureAnimation anim);
                            if (t1.IsAsyncReady)
                            {
                                Texture tex = a.Texture.CopyGlTexture(SizedInternalFormat.Rgba8);
                                AssetPreview prev = new AssetPreview() { GLTex = tex };
                                prev.CopyFromAnimation(anim, tex.Size);
                                values[id] = value = prev;
                                statuses[id] = AssetStatus.Return;
                                status = AssetStatus.Return;
                                return true;
                            }
                            else
                            {
                                statuses[id] = status = AssetStatus.Await;
                                value = null;
                                return true;
                            }
                        }

                        if (a.Type == AssetType.Model && a.ModelGlReady)
                        {
                            using Image<Rgba32> img = a.Model.GLMdl.CreatePreview(256, 256, new Vector4(0, 0, 0, 0), true);
                            Texture tex = new Texture(TextureTarget.Texture2D);
                            tex.Bind();
                            tex.SetWrapParameters(WrapParam.Repeat, WrapParam.Repeat, WrapParam.Repeat);
                            tex.SetFilterParameters(FilterParam.LinearMipmapLinear, FilterParam.Linear);
                            tex.SetImage(img, SizedInternalFormat.Rgba8);
                            tex.GenerateMipMaps();
                            AssetPreview prev = new AssetPreview() { GLTex = tex };
                            values[id] = value = prev;
                            statuses[id] = AssetStatus.Return;
                            status = AssetStatus.Return;
                            return true;
                        }

                        goto case AssetStatus.Await;
                    }
                    else
                    {
                        goto case AssetStatus.Error;
                    }
                }

                case AssetStatus.Await:
                default:
                {
                    status = statuses[id] = AssetStatus.Await;
                    value = null;
                    return true;
                }
            }
        }

        #endregion

        #region Web Picture Handling
        private void RequestWebImage(string id, AssetType type)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", $"VTT-C#-Client-ImageRequestBot/1.0.0 (https://github.com/skyloutyr; skyloutyr@gmail.com) Requesting-User-Constant-GUID/{Client.Instance.ID} ClientCachePolicy/Aggressive");
            try
            {
                client.GetByteArrayAsync(id).ContinueWith(t =>
                {
                    try
                    {
                        if (t.Status == TaskStatus.RanToCompletion)
                        {
                            byte[] array = t.Result;
                            this.WebPictures.ReceiveAsync(id, type, AssetResponseType.Ok, array, null);
                        }
                        else
                        {
                            this.WebPictures.ReceiveAsync(id, type, AssetResponseType.InternalError, null, null);
                        }
                    }
                    catch
                    {
                        this.WebPictures.ReceiveAsync(id, type, AssetResponseType.InternalError, null, null);
                    }
                });
            }
            catch (Exception e)
            {
                Client.Instance.Logger.Log(LogLevel.Error, "Image download request failed!");
                Client.Instance.Logger.Exception(LogLevel.Error, e);
                this.WebPictures.ReceiveAsync(id, type, AssetResponseType.InternalError, null, null);
            }
        }

        private AssetStatus ReceiveWebImage(string id, AssetType type, AssetResponseType response, byte[] bin, AssetMetadata meta, out AssetPreview preview)
        {
            if (response == AssetResponseType.Ok)
            {
                ThreadPool.QueueUserWorkItem(x =>
                {
                    Image<Rgba32> img = null;
                    bool erroredOut = false;
                    List<AssetPreview.FrameData> frames = new List<AssetPreview.FrameData>();
                    try
                    {
                        img = Image.Load<Rgba32>(bin);
                        if (img.Frames.Count > 1)
                        {
                            GraphicsOptions opts = new GraphicsOptions() { AlphaCompositionMode = PixelAlphaCompositionMode.Src, Antialias = false, ColorBlendingMode = PixelColorBlendingMode.Normal };
                            ImageFrameCollection<Rgba32> ifc = img.Frames;
                            int wS = 0;
                            int wM = 0;
                            int hM = 0;
                            foreach (ImageFrame<Rgba32> frame in ifc.Cast<ImageFrame<Rgba32>>())
                            {
                                if (wS + frame.Width > this.GlMaxTextureSize)
                                {
                                    wM = Math.Max(wM, wS);
                                    wS = 0;
                                    hM += frame.Height;
                                    if (hM > this.GlMaxTextureSize)
                                    {
                                        img.Dispose();
                                        Client.Instance.DoTask(() => this.WebPictures.DangerousSetStatusAndData(id, AssetStatus.Error, null));
                                        return;
                                    }
                                }

                                wS += frame.Width;
                            }

                            wM = Math.Max(wM, wS);
                            hM += img.Height;

                            Image<Rgba32> final = new Image<Rgba32>(wM, hM);
                            GifMetadata imgGifMeta = img.Metadata?.GetGifMetadata();
                            int posX = 0;
                            int posY = 0;
                            int cumulativeDelay = 0;
                            for (int i = 0; i < ifc.Count; ++i)
                            {
                                Image<Rgba32> frame = ifc.CloneFrame(i);
                                ImageFrame<Rgba32> aFrame = ifc[i];
                                int delay = 10;
                                if (aFrame.Metadata != null)
                                {
                                    try
                                    {
                                        GifFrameMetadata gfm = aFrame.Metadata.GetGifMetadata();
                                        delay = gfm?.FrameDelay ?? 10;
                                        if (delay == 0)
                                        {
                                            delay = 10;
                                        }
                                    }
                                    catch
                                    {
                                        // NOOP
                                    }
                                }

                                if (posX + frame.Width > wM)
                                {
                                    posX = 0;
                                    posY += img.Height;
                                }

                                cumulativeDelay += delay;
                                final.Mutate(x => x.DrawImage(frame, new Point(posX, posY), opts));
                                AssetPreview.FrameData currentData = new AssetPreview.FrameData(posX, posY, frame.Height, frame.Width, delay, cumulativeDelay);
                                frames.Add(currentData);
                                posX += frame.Width;
                                frame.Dispose();
                            }

                            img.Dispose();
                            img = final;
                        }
                    }
                    catch
                    {
                        erroredOut = true;
                    }

                    Client.Instance.DoTask(() =>
                    {
                        if (erroredOut)
                        {
                            this.WebPictures.EraseRecord(id);
                            this.WebPictures.DangerousSetStatusAndData(id, AssetStatus.Error, null);
                        }
                        else
                        {
                            Texture tex = new Texture(TextureTarget.Texture2D);
                            tex.Bind();
                            if (frames.Count == 0)
                            {
                                tex.SetWrapParameters(WrapParam.Repeat, WrapParam.Repeat, WrapParam.Repeat);
                                tex.SetFilterParameters(FilterParam.LinearMipmapLinear, FilterParam.Linear);
                                tex.SetImage(img, SizedInternalFormat.Rgba8);
                                tex.GenerateMipMaps();
                                img.Dispose();
                                Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
                            }
                            else
                            {
                                tex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
                                tex.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
                                tex.SetImage(img, SizedInternalFormat.Rgba8);
                                img.Dispose();
                                Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
                            }

                            AssetPreview prev = new AssetPreview() { GLTex = tex, IsAnimated = frames.Count > 0 };
                            if (frames.Count > 0)
                            {
                                prev.Frames = frames.ToArray();
                                prev.FramesTotalDelay = frames.Sum(f => f.Duration);
                            }

                            this.WebPictures.EraseRecord(id);
                            this.WebPictures.DangerousSetStatusAndData(id, AssetStatus.Return, prev);
                        }
                    });
                });

                preview = null;
                return AssetStatus.Await;
            }
            else
            {
                preview = null;
                return AssetStatus.Error;
            }
        }

        private void ClearWebImage(string id, AssetPreview preview) => preview.Free();
        #endregion

        public void Clear()
        {
            this.ClearAssets();
            this.Previews.Clear();
            this.WebPictures.Clear();
            Client.Instance.Frontend.Sound?.ClearAssets();
            ParticleSystem.ImageEmissionLocations.Clear();
        }

        public void ClearAssets()
        {
            Client.Instance.Logger.Log(LogLevel.Debug, $"Cleared {this.Assets.NumRecords} asset records.");
            this.Assets.Clear();
            this.Portraits.Clear();
        }

        public void Pulse()
        {
            this.Assets.Pulse();
            this.Previews.Pulse();
            this.WebPictures.Pulse();
        }
    }

    public class AssetLibrary<K, T>
    {
        public delegate AssetStatus AssetReceiverFilterProcedure(K id, AssetType type, AssetResponseType response, byte[] bin, AssetMetadata meta);
        public delegate AssetStatus AssetReceiverParserProcedure(K id, AssetType type, AssetResponseType response, byte[] bin, AssetMetadata meta, out T val);
        public delegate bool AssetCustomGetterProcedure(K id, AssetType type, Dictionary<K, T> values, Dictionary<K, AssetStatus> statuses, Action<K, AssetType> requesterDelegate, out AssetStatus status, out T value);

        private readonly Dictionary<K, T> _values = new Dictionary<K, T>();
        private readonly Queue<(K, AssetType)> _requests = new Queue<(K, AssetType)>();
        private readonly Dictionary<K, AssetStatus> _statuses = new Dictionary<K, AssetStatus>();
        private readonly Dictionary<K, List<Action<AssetStatus, T>>> _callbacks = new Dictionary<K, List<Action<AssetStatus, T>>>();

        private long _lastRequestTicks;

        public Action<K, AssetType, T> ValueValidator { get; set; }
        public Action<K, AssetType> RequestGenerator { get; set; }
        public AssetReceiverFilterProcedure ReceivePreProcessor { get; set; }
        public AssetReceiverParserProcedure DataParser { get; set; }
        public Action<K, T> ValueCleaner { get; set; }
        public Action<K> ValueEraser { get; set; }
        public AssetCustomGetterProcedure CustomValueGetter { get; set; }

        public bool GetStatus(K id, out AssetStatus status)
        {
            if (this._values.TryGetValue(id, out _))
            {
                status = AssetStatus.Return;
                return true;
            }

            if (this._statuses.TryGetValue(id, out status))
            {
                return true;
            }

            status = AssetStatus.NoAsset;
            return false;
        }

        public bool TryUpdate(K id, Action<T> act)
        {
            if (this._values.TryGetValue(id, out T val))
            {
                act(val);
                return true;
            }

            return false;
        }

        public bool TryGetWithoutRequest(K id, out T val) => this._values.TryGetValue(id, out val);

        public AssetStatus ActUpon(K id, AssetType t, Action<AssetStatus, T> callback)
        {
            if (this._values.TryGetValue(id, out T value))
            {
                callback(AssetStatus.Return, value);
                return AssetStatus.Return;
            }

            if (this._statuses.TryGetValue(id, out AssetStatus ret))
            {
                if (ret == AssetStatus.Return)
                {
                    throw new Exception($"Have asset status return for an asset {id}, but no such asset exists!");
                }

                if (ret != AssetStatus.Await)
                {
                    callback(ret, default);
                    return ret;
                }    
            }

            if (!this._callbacks.TryGetValue(id, out List<Action<AssetStatus, T>> callbacks))
            {
                callbacks = new List<Action<AssetStatus, T>>();
                this._callbacks[id] = callbacks;
            }

            callbacks.Add(callback);
            return AssetStatus.Await;
        }

        public AssetStatus Get(K id, AssetType t, out T value)
        {
            if (this._values.TryGetValue(id, out value))
            {
                this.ValueValidator?.Invoke(id, t, value);
                return AssetStatus.Return;
            }

            if (this.CustomValueGetter != null)
            {
                if (this.CustomValueGetter(id, t, this._values, this._statuses, this.RequestAssetInternal, out AssetStatus stat, out T v))
                {
                    value = v;
                    return stat;
                }
            }

            value = default;
            if (this._statuses.TryGetValue(id, out AssetStatus ret))
            {
                return ret;
            }

            this.RequestAssetInternal(id, t);
            return AssetStatus.Await;
        }

        private void RequestAssetInternal(K id, AssetType t)
        {
            this._statuses[id] = AssetStatus.Await;
            this._requests.Enqueue((id, t));
            this.PulseRequest();
        }

        private void PulseRequest()
        {
            this._lastRequestTicks = DateTimeOffset.Now.Ticks;
            if (this._requests.TryDequeue(out (K, AssetType) dat))
            {
                this.RequestGenerator(dat.Item1, dat.Item2);
            }
        }

        public void ReceiveAsync(K id, AssetType type, AssetResponseType response, byte[] binary, AssetMetadata meta) => Client.Instance.DoTask(() => this.Receive(id, type, response, binary, meta));

        public void Receive(K id, AssetType type, AssetResponseType response, byte[] binary, AssetMetadata meta)
        {
            switch (response)
            {
                case AssetResponseType.InternalError:
                {
                    this._statuses[id] = AssetStatus.Error;
                    this.FireCallbacks(id, AssetStatus.Error, default);
                    this.PulseRequest();
                    return;
                }

                case AssetResponseType.NoAsset:
                {
                    this._statuses[id] = AssetStatus.NoAsset;
                    this.FireCallbacks(id, AssetStatus.NoAsset, default);
                    this.PulseRequest();
                    return;
                }

                default:
                {
                    AssetStatus pps = this.ReceivePreProcessor?.Invoke(id, type, response, binary, meta) ?? AssetStatus.Return;
                    if (pps != AssetStatus.Return)
                    {
                        this._statuses[id] = pps;
                        this.FireCallbacks(id, pps, default);
                        this.PulseRequest();
                        return;
                    }

                    T val = default;
                    pps = this.DataParser?.Invoke(id, type, response, binary, meta, out val) ?? AssetStatus.Error;
                    if (pps == AssetStatus.Return)
                    {
                        this._values[id] = val;
                    }

                    this.FireCallbacks(id, pps, val);
                    this._statuses[id] = pps;
                    this.PulseRequest();
                    return;
                }
            }
        }

        private void FireCallbacks(K id, AssetStatus status, T value)
        {
            if (this._callbacks.TryGetValue(id, out List<Action<AssetStatus, T>> callbacks))
            {
                foreach (Action<AssetStatus, T> action in callbacks)
                {
                    action(status, value);
                }

                this._callbacks.Remove(id);
            }
        }

        public void EraseRecord(K id)
        {
            if (this._values.Remove(id, out T val))
            {
                this.ValueCleaner?.Invoke(id, val);
            }

            this._statuses.Remove(id);
            this.ValueEraser?.Invoke(id);
        }
    
        public void DangerousSetStatusAndData(K id, AssetStatus status, T val)
        {
            if (val != null)
            {
                this._values[id] = val;
            }

            this._statuses[id] = status;
        }

        public void Pulse()
        {
            long now = DateTimeOffset.Now.Ticks;
            if (now - this._lastRequestTicks >= TimeSpan.TicksPerSecond * 30)
            {
                this.PulseRequest();
            }
        }

        public void Clear()
        {
            foreach (KeyValuePair<K, T> kv in this._values)
            {
                if (kv.Value != null)
                {
                    this.ValueCleaner(kv.Key, kv.Value);
                }
            }

            this._values.Clear();
            this._statuses.Clear();
            this._requests.Clear();
            this._callbacks.Clear();
        }

        public int NumRecords => this._values.Count;
        public int NumStatuses => this._statuses.Count;
        public int NumPendingRequests => this._requests.Count;
    }

    public enum AssetStatus
    {
        Return,
        Await,
        NoAsset,
        Error
    }

}
