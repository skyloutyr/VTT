namespace VTT.Asset
{
    using Newtonsoft.Json;
    using OpenTK.Graphics.OpenGL;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Formats.Gif;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public class AssetManager
    {
        public Dictionary<Guid, Asset> Assets { get; } = new Dictionary<Guid, Asset>();
        public Dictionary<Guid, AssetRef> Refs { get; } = new Dictionary<Guid, AssetRef>();
        public Dictionary<Guid, AssetPreview> Previews { get; } = new Dictionary<Guid, AssetPreview>();
        public Dictionary<Guid, AssetPreview> Portraits { get; } = new Dictionary<Guid, AssetPreview>();
        public Dictionary<string, AssetPreview> WebPictures { get; } = new Dictionary<string, AssetPreview>();

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

                    x.assetDir.Refs.Add(aRef);
                    lock (localRefLock)
                    {
                        Refs.Add(aId, aRef);
                    }
                }
            });
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

        public Queue<Guid> RequestQueue { get; } = new Queue<Guid>();
        public Queue<Guid> PreviewRequestQueue { get; } = new Queue<Guid>();
        public Queue<AssetType> RequestTypeQueue { get; } = new Queue<AssetType>();

        public Dictionary<Guid, AssetStatus> ErroredAssets { get; } = new Dictionary<Guid, AssetStatus>();
        public Dictionary<Guid, AssetStatus> ErroredPreviews { get; } = new Dictionary<Guid, AssetStatus>();
        public Dictionary<string, AssetStatus> ErroredWebImages { get; } = new Dictionary<string, AssetStatus>();

        public long LastRequestTime { get; set; }
        public long LastPreviewRequestTime { get; set; }
        public int GlMaxTextureSize { get; set; }

        private readonly object _lock = new object();
        private readonly object _lock2 = new object();

        public AssetStatus GetWebImage(string url, out AssetPreview ap)
        {
            if (this.Container.WebPictures.TryGetValue(url, out ap))
            {
                return AssetStatus.Return;
            }

            ap = null;
            if (this.ErroredWebImages.TryGetValue(url, out AssetStatus ret))
            {
                return ret;
            }

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", $"VTT-C#-Client-ImageRequestBot/1.0.0 (https://github.com/skyloutyr; skyloutyr@gmail.com) Requesting-User-Constant-GUID/{Client.Instance.ID} ClientCachePolicy/Aggressive");

            try
            {
                client.GetByteArrayAsync(url).ContinueWith((t) =>
                {
                    try
                    {
                        if (t.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                        {
                            byte[] array = t.Result;
                            Image<Rgba32> img = Image.Load<Rgba32>(array);
                            List<AssetPreview.FrameData> frames = new List<AssetPreview.FrameData>();
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
                                            this.ErroredWebImages[url] = AssetStatus.Error;
                                            img.Dispose();
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

                            Client.Instance.DoTask(() =>
                            {
                                Texture tex = new Texture(TextureTarget.Texture2D);
                                tex.Bind();
                                if (frames.Count == 0)
                                {
                                    tex.SetWrapParameters(WrapParam.Repeat, WrapParam.Repeat, WrapParam.Repeat);
                                    tex.SetFilterParameters(FilterParam.LinearMipmapLinear, FilterParam.Linear);
                                    tex.SetImage(img, PixelInternalFormat.Rgba);
                                    tex.GenerateMipMaps();
                                    img.Dispose();
                                    Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
                                }
                                else
                                {
                                    tex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
                                    tex.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
                                    System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                                    tex.SetImage(img, PixelInternalFormat.Rgba);
                                    img.Dispose();
                                    Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
                                }

                                AssetPreview prev = new AssetPreview() { GLTex = tex, IsAnimated = frames.Count > 0 };
                                if (frames.Count > 0)
                                {
                                    prev.Frames = frames.ToArray();
                                    prev.FramesTotalDelay = frames.Sum(f => f.Duration);
                                }

                                this.Container.WebPictures[url] = prev;
                                this.Container.ClientAssetLibrary.ErroredWebImages.Remove(url);
                            });
                        }
                        else
                        {
                            this.ErroredWebImages[url] = AssetStatus.Error;
                        }
                    }
                    catch
                    {
                        this.ErroredWebImages[url] = AssetStatus.Error;
                    }
                });
            }
            catch (Exception e)
            {
                Client.Instance.Logger.Log(LogLevel.Error, "Image download request failed!");
                Client.Instance.Logger.Exception(LogLevel.Error, e);
                this.ErroredWebImages[url] = AssetStatus.Error;
                return AssetStatus.Error;
            }

            this.ErroredWebImages[url] = AssetStatus.Await;
            return AssetStatus.Await;
        }

        public AssetStatus GetOrCreatePortrait(Guid aID, out AssetPreview ap)
        {
            if (this.Container.Portraits.TryGetValue(aID, out ap))
            {
                return AssetStatus.Return;
            }

            ap = null;
            if (this.ErroredAssets.TryGetValue(aID, out AssetStatus ret))
            {
                return ret;
            }

            if (this.RequestQueue.Contains(aID))
            {
                return AssetStatus.Await; // Asset already requested
            }

            if (!this.Container.Assets.ContainsKey(aID))
            {
                // Request asset
                this.RequestQueue.Enqueue(aID);
                this.RequestTypeQueue.Enqueue(AssetType.Model);
                this.PulseRequest();
                return AssetStatus.Await;
            }

            Asset a = this.Container.Assets[aID];
            if (a != null)
            {
                if (a.Type == AssetType.Texture && (a?.Texture?.glReady ?? false))
                {
                    Texture tex = a?.Texture.CopyGlTexture(PixelInternalFormat.Rgba);
                    if (tex.IsAsyncReady)
                    {
                        AssetPreview prev = new AssetPreview() { GLTex = tex };
                        this.Container.Portraits[aID] = prev;
                        ap = prev;
                        return AssetStatus.Return;
                    }
                    else
                    {
                        return AssetStatus.Await;
                    }
                }

                if (a.Type == AssetType.Model && (a?.Model?.GLMdl?.glReady ?? false))
                {
                    using Image<Rgba32> img = a.Model.GLMdl.CreatePreview(256, 256, new OpenTK.Mathematics.Vector4(0, 0, 0, 0), true);
                    Texture tex = new Texture(TextureTarget.Texture2D);
                    tex.Bind();
                    tex.SetWrapParameters(WrapParam.Repeat, WrapParam.Repeat, WrapParam.Repeat);
                    tex.SetFilterParameters(FilterParam.LinearMipmapLinear, FilterParam.Linear);
                    tex.SetImage(img, PixelInternalFormat.Rgba);
                    tex.GenerateMipMaps();
                    AssetPreview prev = new AssetPreview() { GLTex = tex };
                    this.Container.Portraits[aID] = prev;
                    ap = prev;

                    return AssetStatus.Return;
                }

                return AssetStatus.Error;
            }

            return AssetStatus.NoAsset; //Race condition?
        }

        public AssetStatus GetOrRequestPreview(Guid aID, out AssetPreview ap)
        {
            if (this.Container.Previews.TryGetValue(aID, out ap))
            {
                return AssetStatus.Return;
            }

            ap = null;
            if (this.ErroredAssets.TryGetValue(aID, out AssetStatus retEA))
            {
                return retEA;
            }

            if (this.ErroredPreviews.TryGetValue(aID, out AssetStatus retEP))
            {
                return retEP;
            }

            if (this.PreviewRequestQueue.Contains(aID))
            {
                return AssetStatus.Await;
            }

            this.PreviewRequestQueue.Enqueue(aID);
            this.PulsePreview();
            return AssetStatus.Await;
        }

        private readonly ConcurrentDictionary<Guid, ConcurrentQueue<Action<AssetStatus, Asset>>> _assetCallbacks = new ConcurrentDictionary<Guid, ConcurrentQueue<Action<AssetStatus, Asset>>>();
        private readonly object _assetCallbackLock = new object();

        public void PerformClientAssetAction(Guid aID, AssetType aType, Action<AssetStatus, Asset> callback)
        {
            if (this.Container.Assets.TryGetValue(aID, out Asset a))
            {
                if (a.Type == AssetType.Texture && aType == AssetType.Model && a.Model == null && a.Texture != null && a.Texture.glReady && Client.Instance.Frontend.CheckThread())
                {
                    Glb.GlbScene mdl = a.Texture.ToGlbModel();
                    a.Model = new ModelData() { GLMdl = mdl };
                }

                callback(AssetStatus.Return, a);
                return;
            }

            if (this.ErroredAssets.TryGetValue(aID, out AssetStatus ret))
            {
                callback(ret, null);
                return;
            }

            if (this.RequestQueue.Contains(aID))
            {
                // Asset already requested, enqueue?
                lock (this._assetCallbackLock)
                {
                    // Try for object status here again, may be present
                    if (this.Container.Assets.TryGetValue(aID, out a))
                    {
                        if (a.Type == AssetType.Texture && aType == AssetType.Model && a.Model == null && a.Texture != null && a.Texture.glReady && Client.Instance.Frontend.CheckThread())
                        {
                            Glb.GlbScene mdl = a.Texture.ToGlbModel();
                            a.Model = new ModelData() { GLMdl = mdl };
                        }

                        callback(AssetStatus.Return, a);
                        return;
                    }

                    if (this.ErroredAssets.TryGetValue(aID, out ret))
                    {
                        callback(ret, null);
                        return;
                    }

                    // Enqueue
                    if (!this._assetCallbacks.TryGetValue(aID, out ConcurrentQueue<Action<AssetStatus, Asset>> callbackQueue))
                    {
                        ConcurrentQueue<Action<AssetStatus, Asset>> cq = callbackQueue = new ConcurrentQueue<Action<AssetStatus, Asset>>();
                        this._assetCallbacks.TryAdd(aID, cq);
                    }

                    callbackQueue.Enqueue(callback);
                }

                return;
            }

            this.RequestQueue.Enqueue(aID);
            this.RequestTypeQueue.Enqueue(aType);
            this.PulseRequest();
        }

        public AssetStatus GetOrRequestAsset(Guid aID, AssetType aType, out Asset a)
        {
            if (this.Container.Assets.TryGetValue(aID, out a))
            {
                if (a.Type == AssetType.Texture && aType == AssetType.Model && a.Model == null && a.Texture != null && a.Texture.glReady && Client.Instance.Frontend.CheckThread())
                {
                    Glb.GlbScene mdl = a.Texture.ToGlbModel();
                    a.Model = new ModelData() { GLMdl = mdl };
                }

                return AssetStatus.Return;
            }

            a = null;
            if (this.ErroredAssets.TryGetValue(aID, out AssetStatus ret))
            {
                return ret;
            }

            if (this.RequestQueue.Contains(aID))
            {
                return AssetStatus.Await; // Asset already requested
            }

            this.RequestQueue.Enqueue(aID);
            this.RequestTypeQueue.Enqueue(aType);
            this.PulseRequest();
            return AssetStatus.Await; // Requested asset
        }

        public void EraseAssetRecord(Guid aID) => this.ErroredAssets.Remove(aID);

        public void PulsePreview()
        {
            lock (this._lock2) // Multithreaded access prevention
            {
                bool timestampMatch = this.LastPreviewRequestTime == 0 || ((DateTimeOffset.Now.ToUnixTimeMilliseconds() - this.LastPreviewRequestTime) >= AssetAwaitTime);
                if (timestampMatch)
                {
                    if (this.PreviewRequestQueue.Count > 0)
                    {
                        this.LastPreviewRequestTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        Guid id = this.PreviewRequestQueue.Dequeue();
                        this.ErroredPreviews[id] = AssetStatus.Await;
                        this.InternalRequestAssetPreview(id);
                    }
                }
            }
        }

        private void InternalRequestAssetPreview(Guid guid)
        {
            PacketAssetPreview pap = new PacketAssetPreview() { ID = guid, Session = Client.Instance.SessionID, IsServer = false };
            pap.Send(Client.Instance.NetClient);
        }

        public void PulseRequest()
        {
            lock (this._lock) // Multithreaded access prevention
            {
                bool timestampMatch = this.LastRequestTime == 0 || ((DateTimeOffset.Now.ToUnixTimeMilliseconds() - this.LastRequestTime) >= AssetAwaitTime);
                if (timestampMatch)
                {
                    if (this.RequestQueue.Count > 0)
                    {
                        Guid id = this.RequestQueue.Dequeue();
                        this.LastRequestTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        this.ErroredAssets[id] = AssetStatus.Await;
                        this.InternalRequestAsset(id, this.RequestTypeQueue.Dequeue());
                    }
                }
            }
        }

        private void InternalRequestAsset(Guid assetID, AssetType aType)
        {
            PacketAssetRequest par = new PacketAssetRequest() { AssetID = assetID, AssetType = aType, IsServer = false };
            par.Send(Client.Instance.NetClient);
        }

        public void ErrorAsset(Guid assetID, AssetResponseType responseType)
        {
            AssetStatus astat = responseType == AssetResponseType.InternalError ? AssetStatus.Error : AssetStatus.NoAsset;
            this.ErroredAssets[assetID] = astat;

            // Notify callbacks of error
            lock (this._assetCallbackLock)
            {
                if (this._assetCallbacks.TryGetValue(assetID, out ConcurrentQueue<Action<AssetStatus, Asset>> callbacks))
                {
                    while (!callbacks.IsEmpty)
                    {
                        if (!callbacks.TryDequeue(out Action<AssetStatus, Asset> callback))
                        {
                            break;
                        }

                        callback(astat, null);
                    }
                }
            }

            this.LastRequestTime = 0;
            this.PulseRequest(); // Check for new requests and process them
        }

        public void ReceivePreview(Guid assetID, AssetResponseType response, byte[] binary)
        {
            if (response != AssetResponseType.Ok)
            {
                this.ErroredPreviews[assetID] = response == AssetResponseType.InternalError ? AssetStatus.Error : AssetStatus.NoAsset;
            }
            else
            {
                AssetPreview ap = new AssetPreview() { Data = binary };
                this.Container.Previews[assetID] = ap;
                this.ErroredPreviews.Remove(assetID);
            }

            this.LastPreviewRequestTime = 0;
            this.PulsePreview();
        }

        public void ReceiveAsset(Guid assetID, AssetType assetType, byte[] binary, AssetMetadata meta)
        {
            Asset a = new Asset() { ID = assetID, Type = assetType };
            byte[] rawBinary = AssetBinaryPointer.GetRawAssetBinary(binary);
            ThreadPool.QueueUserWorkItem((token) =>
            {
                switch (assetType)
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

            this.Container.Assets[assetID] = a;
            this.EraseAssetRecord(assetID);

            // Process callbacks.
            lock (this._assetCallbackLock)
            {
                if (this._assetCallbacks.TryGetValue(assetID, out ConcurrentQueue<Action<AssetStatus, Asset>> callbacks))
                {
                    while (!callbacks.IsEmpty)
                    {
                        if (!callbacks.TryDequeue(out Action<AssetStatus, Asset> callback))
                        {
                            break;
                        }

                        callback(AssetStatus.Return, a);
                    }

                    this._assetCallbacks.TryRemove(assetID, out _); // Do not care for out param
                }
            }


            this.LastRequestTime = 0;
            this.PulseRequest(); // Check for new requests and process them
        }

        public void Clear()
        {
            this.ClearAssets();
            this.ErroredPreviews.Clear();
            this.ErroredWebImages.Clear();
            this.RequestQueue.Clear();
            this.RequestTypeQueue.Clear();
            foreach (AssetPreview ap in this.Container.Previews.Values)
            {
                ap?.GLTex?.Dispose();
            }

            this.Container.Previews.Clear();

            foreach (AssetPreview ap in this.Container.Portraits.Values)
            {
                ap?.GLTex?.Dispose();
            }

            this.Container.Portraits.Clear();

            foreach (AssetPreview ap in this.Container.WebPictures.Values)
            {
                ap?.GLTex?.Dispose();
            }

            this.Container.WebPictures.Clear();
            Client.Instance.Frontend.Sound?.ClearAssets();
        }

        public void ClearAssets()
        {
            this.LastRequestTime = 0;
            this.ErroredAssets.Clear();
            foreach (Asset a in this.Container.Assets.Values)
            {
                a.Dispose();
            }

            this.Container.Assets.Clear();
        }
    }

    public enum AssetStatus
    {
        Return,
        Await,
        NoAsset,
        Error
    }

}
