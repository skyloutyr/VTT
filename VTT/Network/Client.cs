﻿namespace VTT.Network
{
    using NetCoreServer;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Net;
    using System.Numerics;
    using VTT.Asset;
    using VTT.Asset.Shader.NodeGraph;
    using VTT.Control;
    using VTT.Network.Packet;
    using VTT.Network.VSCC;
    using VTT.Render;
    using VTT.Util;
    using static VTT.Network.ChatDiceRollMemory;

    /*
     * Client-Server primer
     * 
     *  Clients are unique ID + Name pair
     *  Server is a ID
     */

    public class Client
    {
        public static Client Instance { get; set; }

        public Guid ID { get; set; }
        public Guid SessionID { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsObserver { get; set; }
        public Logger Logger { get; set; }

        public SimpleLanguage Lang { get; set; }
        public AppVersion ClientVersion { get; set; }
        public NetClient NetClient { get; set; }
        public bool Connected => this.NetClient?.IsConnected ?? false;

        public AssetManager AssetManager { get; } = new AssetManager() { IsServer = false };

        public Map CurrentMap { get; set; }
        public ClientSettings Settings { get; set; }
        public ClientWindow Frontend { get; set; }

        public ConcurrentDictionary<Guid, TextJournal> Journals { get; } = new ConcurrentDictionary<Guid, TextJournal>();
        public ConcurrentDictionary<Guid, ClientInfo> ClientInfos { get; } = new ConcurrentDictionary<Guid, ClientInfo>();

        public object ServerMapPointersLock { get; } = new object();
        //public SortedDictionary<string, List<(Guid, string)>> ServerMapPointers { get; } = new SortedDictionary<string, List<(Guid, string)>>();

        public object chatLock = new object();
        public List<ChatLine> Chat { get; } = new List<ChatLine>();

        public VSCCIntegration VSCCIntegration { get; } = new VSCCIntegration();
        public bool NetworkStateCorrupted { get; set; }

        public NetworkMonitor NetworkIn { get; } = new NetworkMonitor();
        public NetworkMonitor NetworkOut { get; } = new NetworkMonitor();

        public DisconnectReason LastDisconnectReason { get; set; } = DisconnectReason.InternalClientError;
        public long TimeoutInterval { get; set; } = (long)TimeSpan.FromMinutes(1).TotalMilliseconds;

        public Client()
        {
            Instance = this;
            if (!ArgsManager.TryGetValue(LaunchArgumentKey.LoggingLevel, out LogLevel ll))
            {
                ll = LogLevel.Off;
            }

            if (ArgsManager.TryGetValue(LaunchArgumentKey.NetworkTimeoutSpan, out long ti))
            {
                this.TimeoutInterval = ti;
            }

            AppDomain.CurrentDomain.ProcessExit += this.Cleanup;
            this.Logger = new Logger() { Prefix = "Client", TimeFormat = "HH:mm:ss.fff", ActiveLevel = ll };
            this.Logger.OnLog += Logger.Console;
            if (ArgsManager.TryGetValue(LaunchArgumentKey.EnableDebuggerLogging, out bool debuggerLogging) && debuggerLogging)
            {
                this.Logger.OnLog += Logger.Debug;
            }

            Logger.FileLogListener fll = this._fll = new Logger.FileLogListener(IOVTT.OpenLogFile(false));
            this.Logger.OnLog += fll.WriteLine;
            this.Logger.OnLog += VTTLogListener.Instance.WriteLine;
            this.Logger.Log(LogLevel.Info, DateTime.Now.ToString("ddd, dd MMM yyy HH:mm:ss GMT"));
            this.ID = IDUtil.GetDeviceID(this.Logger);
            this.Logger.Log(LogLevel.Info, "Self-assigned id is " + this.ID.ToString());
            this.Settings = ClientSettings.Load();
            this.LoadVersion();
            Localisation.GatherAll();
            this.Lang = Localisation.SwitchLanguage(this.Settings.Language);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(ShaderNodeTemplate).TypeHandle);
            this.Frontend = new ClientWindow();
        }

        private void LoadVersion()
        {
            string fPath = Path.Combine(IOVTT.AppDir, "Version.json");
            if (File.Exists(fPath))
            {
                this.ClientVersion = JsonConvert.DeserializeObject<AppVersion>(File.ReadAllText(fPath));
            }
            else // Have no version file
            {
                this.Logger.Log(LogLevel.Warn, "No version file present, downloading!");
                using System.Net.Http.HttpClient wc = new System.Net.Http.HttpClient();
                System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> task = wc.GetAsync("https://raw.githubusercontent.com/skyloutyr/VTT/master/VTT/Version.json");
                task.Start();
                try
                {
                    task.Wait(new TimeSpan(0, 0, 30));
                    if (task.IsCompletedSuccessfully)
                    {
                        using StreamReader sr = new StreamReader(task.Result.Content.ReadAsStream());
                        string contents = sr.ReadToEnd();
                        File.WriteAllText(fPath, contents);
                        this.ClientVersion = JsonConvert.DeserializeObject<AppVersion>(contents);
                    }
                    else
                    {
                        this.ClientVersion = new AppVersion() { Version = Program.Version };
                    }
                }
                catch (Exception e)
                {
                    this.Logger.Exception(LogLevel.Error, e);
                    this.ClientVersion = new AppVersion() { Version = Program.Version };
                }
            }
        }

        private readonly Logger.FileLogListener _fll;
        private void Cleanup(object sender, EventArgs e) =>
            // Try a logger cleanup
            this.CloseLogger();

        public void CloseLogger()
        {
            try
            {
                this._fll?.Close();
            }
            catch
            {
                // NOOP - no idea when this fires, may have FS/logger issues
            }
        }

        public void AddChatLine(ChatLine line)
        {
            lock (this.chatLock)
            {
                if (line.Index < 0)
                {
                    this.Logger.Log(LogLevel.Error, "Got negative chat line index!");
                    return;
                }

                if (this.Chat.Count == 0)
                {
                    this.Chat.Add(line);
                    return;
                }

                if (this.Chat[0].Index > line.Index)
                {
                    this.Chat.Insert(0, line);
                    return;
                }

                if (this.Chat[^1].Index < line.Index)
                {
                    this.Chat.Add(line);
                    if (line.SenderID != this.ID && (Guid.Empty.Equals(line.DestID) || line.DestID.Equals(this.ID)))
                    {
                        if (this.Settings.EnableChatNotification)
                        {
                            this.Frontend.PushNotification();
                            if (this.Settings.EnableSoundChatMessage)
                            {
                                this.Frontend.Sound.PlaySound(this.Frontend.Sound.ChatMessage, Sound.SoundCategory.UI);
                            }
                        }
                    }

                    return;
                }

                int idx = 0;
                while (true)
                {
                    ChatLine cl = this.Chat[idx++];
                    if (cl.Index > line.Index)
                    {
                        this.Chat.Insert(--idx, cl);
                        break;
                    }

                    if (cl.Index == line.Index)// Uh-oh
                    {
                        this.Logger.Log(LogLevel.Warn, "Got chat line for existing index!");
                        break;
                    }
                }
            }
        }

        private string[] _namesArray = Array.Empty<string>();
        private Guid[] _idsArray = Array.Empty<Guid>();
        public void TryGetClientNamesArray(Guid cId, out int namesArrayIndex, out string[] namesAray, out Guid[] idsArray)
        {
            if (this._namesArray.Length != this.ClientInfos.Count)
            {
                this._namesArray = new string[this.ClientInfos.Count];
                this._idsArray = new Guid[this.ClientInfos.Count];
            }

            namesArrayIndex = -1;
            int i = 0;
            foreach (KeyValuePair<Guid, ClientInfo> kv in this.ClientInfos)
            {
                this._namesArray[i] = kv.Value.Name;
                this._idsArray[i] = kv.Key;
                if (kv.Key.Equals(cId))
                {
                    namesArrayIndex = i;
                }

                i++;
            }

            namesAray = this._namesArray;
            idsArray = this._idsArray;
        }

        public bool TryFindName(Guid clientID, out string name)
        {
            if (this.ClientInfos.TryGetValue(clientID, out ClientInfo info))
            {
                name = info.Name;
                return true;
            }

            name = string.Empty;
            return false;
        }

        public ClientInfo CreateSelfInfo()
        {
            return new ClientInfo()
            {
                ID = this.ID,
                Color = Extensions.FromArgb(this.Settings.Color),
                IsAdmin = this.IsAdmin,
                IsObserver = this.IsObserver,
                MapID = this.CurrentMap?.ID ?? Guid.Empty,
                Name = this.Settings.Name
            };
        }

        public void Connect(IPEndPoint endpoint)
        {
            this.NetClient = new NetClient(endpoint) { Container = this, OptionKeepAlive = true };
            this.NetClient.ConnectAsync();
            this.Logger.Log(LogLevel.Info, "Trying to connect to server at " + endpoint.ToString());
        }

        public void Disconnect(DisconnectReason dcr)
        {
            this.DoTask(() =>
            {
                try
                {
                    this.NetClient?.DisconnectAsync();
                    this.NetClient?.Dispose();
                }
                finally
                {
                    this.NetClient = null;
                }

                lock (this.ServerMapPointersLock)
                {
                    this.RawClientMPMapsData.Clear();
                    this.ClientMPMapsRoot.Elements.Clear();
                }

                this.ClientInfos.Clear();
                lock (this.chatLock)
                {
                    this.Chat.Clear();
                }

                this.SetCurrentMap(null, () => { });
                this.AssetManager.ClientAssetLibrary.Clear();
                this.Frontend?.Sound?.ClearAssets();
                this.Logger.Log(LogLevel.Info, "Connection with server disposed");
                if (dcr != DisconnectReason.ManualDisconnect)
                {
                    this.SetDisconnectReason(dcr);
                }
            });
        }

        public Map CurrentMapIfMatches(Guid id)
        {
            Map ret = this.CurrentMap;
            return ret?.ID.Equals(id) ?? false ? ret : null;
        }

        public void SetCurrentMap(Map map, Action postSetAction)
        {
            if (map != null)
            {
                this.DoTask(() =>
                {
                    this.Frontend.Renderer.MapRenderer.ClientCamera.Position = map.DefaultCameraPosition;
                    this.Frontend.Renderer.MapRenderer.ClientCamera.Direction = map.DefaultCameraRotation;
                    this.Frontend.Renderer.MapRenderer.ClientCamera.RecalculateData(assumedUpAxis: Vector3.UnitZ);
                    this.Frontend.Renderer.MapRenderer.Switch2D(map.Is2D, map.DefaultCameraPosition.Z);
                    this.Frontend.Renderer.RulerRenderer.ClearAllRulers();
                    this.Frontend.Renderer.RulerRenderer.ActiveInfos.AddRange(map.PermanentMarks);
                    this.Frontend.Renderer.MapRenderer.DrawingRenderer.FreeAll();
                    this.Frontend.Renderer.MapRenderer.DrawingRenderer.AddContainers(map.Drawings);
                });
            }

            Map cMapRef = this.CurrentMap;
            this.DoTask(this.AssetManager.ClientAssetLibrary.ClearAssets);
            this.DoTask(this.Frontend.Renderer.PingRenderer.ClearPings);
            this.DoTask(() => this.Frontend.Renderer.ParticleRenderer.ClearParticles(cMapRef));
            this.DoTask(() => this.Frontend.Renderer.SelectionManager.BoxSelectCandidates.Clear());
            this.DoTask(() => this.Frontend.Renderer.SelectionManager.SelectedObjects.Clear());
            this.DoTask(() => cMapRef?.ShadowLayer2D.Free());

            this.CurrentMap = map;
            if (map != null)
            {
                this.Logger.Log(LogLevel.Info, "Current map set to " + map.ID + "(" + map.Name + ")");
            }

            this.DoTask(postSetAction);
        }

        public void DoTask(Action a) => this.Frontend.EnqueueTask(a);
        public void DoTaskNextFrame(Action a) => this.Frontend.EnqueueTaskNextUpdate(a);
        public void SetDisconnectReason(DisconnectReason dCR)
        {
            this.LastDisconnectReason = dCR;
            if (this.Frontend?.Renderer?.GuiRenderer != null)
            {
                this.Frontend.Renderer.GuiRenderer.showDisconnect = true;
            }
        }

        public List<(string, string, Guid)> RawClientMPMapsData { get; } = new List<(string, string, Guid)>();
        public MPMapPointer ClientMPMapsRoot { get; } = new MPMapPointer() { Name = "/" };
        public Guid DefaultMPMapID { get; set; } = Guid.Empty;

        public void SortClientMaps()
        {
            lock (this.ServerMapPointersLock)
            {
                this.ClientMPMapsRoot.Elements.Clear();
                foreach ((string, string, Guid) dat in this.RawClientMPMapsData)
                {
                    MPMapPointer dir = this.WalkDir(dat.Item1);
                    MPMapPointer map = new MPMapPointer() { IsMap = true, MapID = dat.Item3, Name = dat.Item2 };
                    dir.Elements.Add(map);
                }

                this.ClientMPMapsRoot.RecursivelySort();
            }
        }

        private MPMapPointer WalkDir(string path)
        {
            MPMapPointer GetOrCreate(MPMapPointer dir, string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return dir;
                }

                MPMapPointer ret = dir.Elements.Find(x => x.Name.Equals(name) && !x.IsMap);
                if (ret == null)
                {
                    ret = new MPMapPointer() { Name = name };
                    dir.Elements.Add(ret);
                }

                return ret;
            }

            MPMapPointer currentDir = this.ClientMPMapsRoot;
            if (path.Length < 1 || path[0] == '/')
            {
                return currentDir;
            }

            if (!path.Contains('/'))
            {
                return GetOrCreate(currentDir, path);
            }

            string[] split = path.Split('/');
            for (int i = 0; i < split.Length; ++i)
            {
                currentDir = GetOrCreate(currentDir, split[i]);
            }

            return currentDir;
        }
    }

    public class NetClient : TcpClient
    {
        public Client Container { get; set; }
        public PacketNetworkManager PacketNetworkManager { get; set; }
        public long LastPingResponseTime { get; set; }

        public NetClient(IPEndPoint endpoint) : base(endpoint) => this.LastPingResponseTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        protected override void OnConnected()
        {
            this.Container.NetworkStateCorrupted = false;
            this.PacketNetworkManager = new PacketNetworkManager() { IsServer = false };
            Client.Instance.Logger.Log(LogLevel.Info, "Server connection estabilished with connection id " + this.Id);
            new PacketHandshake() { ClientID = this.Container.ID, Session = this.Id, IsServer = false, ClientVersion = Program.GetVersionBytes(), ClientSecret = IDUtil.GetSecret() }.Send(this);
            Client.Instance.Logger.Log(LogLevel.Info, "Sending handshake");
            this.Container.SessionID = this.Id;
            this.LastPingResponseTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        protected override void OnError(System.Net.Sockets.SocketError error)
        {
            Client.Instance.Logger.Log(LogLevel.Error, "NetSocket error " + error);
            this.DisconnectAsync();
            this.Container.Disconnect(error switch
            {
                System.Net.Sockets.SocketError.TimedOut => DisconnectReason.Timeout,
                System.Net.Sockets.SocketError.Disconnecting => this.Container.LastDisconnectReason,
                _ => DisconnectReason.InternalServerError
            });
        }

        protected override void OnSent(long sent, long pending)
        {
            base.OnSent(sent, pending);
            Client.Instance.NetworkOut.Increment(sent);
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Client.Instance.NetworkIn.Increment(size);
            foreach (PacketBase pb in this.PacketNetworkManager.Receive(buffer, offset, size))
            {
                pb.IsServer = false;
                try
                {
                    pb.Act(this.Id, null, this.Container, false);
                }
                catch (Exception e)
                {
                    this.Container.Logger.Log(LogLevel.Error, "Error while handling packet - " + pb);
                    this.Container.Logger.Exception(LogLevel.Error, e);
                    throw;
                }
            }

            if (this.Container.NetworkStateCorrupted)
            {
                this.Container.Logger.Log(LogLevel.Error, "Network state got corrupted!");
                this.DisconnectAsync();
                this.Container.Disconnect(DisconnectReason.NetworkStateCorrupted);
            }
        }
    }

    public class ClientSettings
    {

        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool IsFullscreen { get; set; } = false;

        public ClientSize Resolution { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [DefaultValue(VSyncMode.On)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public VSyncMode VSync { get; set; } = VSyncMode.On;

        public string Name { get; set; }
        public uint Color { get; set; }


        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool EnableSunShadows { get; set; } = true;


        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool EnableDirectionalShadows { get; set; } = true;

        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool DisableShaderBranching { get; set; } = false;

        [JsonConverter(typeof(StringEnumConverter))]
        [DefaultValue(FullscreenMode.Normal)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public FullscreenMode ScreenMode { get; set; } = FullscreenMode.Normal;

        [JsonConverter(typeof(StringEnumConverter))]
        [DefaultValue(GraphicsSetting.Medium)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public GraphicsSetting PointShadowsQuality { get; set; } = GraphicsSetting.Medium;

        [JsonConverter(typeof(StringEnumConverter))]
        [DefaultValue(UISkin.Dark)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public UISkin InterfaceSkin { get; set; } = UISkin.Dark;

        [DefaultValue(0.75f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float FOWAdmin { get; set; } = 0.75f;

        [DefaultValue("en-EN")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string Language { get; set; } = "en-EN";

        [DefaultValue("")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string LastConnectIPAddress { get; set; } = string.Empty;

        [DefaultValue("")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string LastConnectPort { get; set; } = string.Empty;

        [JsonConverter(typeof(StringEnumConverter))]
        [DefaultValue(RaycastMultithreadingType.Eager)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public RaycastMultithreadingType RaycastMultithreading { get; set; } = RaycastMultithreadingType.Eager;

        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool DebugSettingsEnabled { get; set; } = false;

        [DefaultValue(1.0f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float Sensitivity { get; set; } = 1.0f;

        [DefaultValue(0.0f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float ChatBackgroundBrightness { get; set; } = 0.0f;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool ParticlesEnabled { get; set; } = true;

        [DefaultValue(2)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int ShadowsPCF { get; set; } = 2;

        [DefaultValue(2.2f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float Gamma { get; set; } = 2.2f;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool UseUBO { get; set; } = true;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool EnableCustomShaders { get; set; } = true;

        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool UseHalfPrecision { get; set; } = false;

        [DefaultValue(MSAAMode.Standard)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public MSAAMode MSAA { get; set; } = MSAAMode.Standard;

        [DefaultValue(UnfocusedFramerateCap.Native)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public UnfocusedFramerateCap UnfocusedFramerate { get; set; } = UnfocusedFramerateCap.Native;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool UseSpirVShaders { get; set; } = true;

        [DefaultValue(60.0f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float FOV { get; set; } = 60.0f;

        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool TurnTrackerParticlesEnabled { get; set; } = false;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool TextThickDropShadow { get; set; } = true;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool EnableChatNotification { get; set; } = true;

        [DefaultValue(1.0f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float SoundMasterVolume { get; set; } = 1.0f;

        [DefaultValue(1.0f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float SoundUIVolume { get; set; } = 1.0f;

        [DefaultValue(1.0f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float SoundMapFXVolume { get; set; } = 1.0f;

        [DefaultValue(1.0f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float SoundAssetVolume { get; set; } = 1.0f;

        [DefaultValue(1.0f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float SoundAmbianceVolume { get; set; } = 1.0f;

        [DefaultValue(1.0f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float SoundMusicVolume { get; set; } = 1.0f;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool EnableSoundChatMessage { get; set; } = true;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool EnableSoundTurnTracker { get; set; } = true;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool EnableSoundPing { get; set; } = true;

        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool DisableSounds { get; set; } = false;

        [JsonConverter(typeof(StringEnumConverter))]
        [DefaultValue(TextureCompressionPreference.DXT)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public TextureCompressionPreference CompressionPreference { get; set; } = TextureCompressionPreference.DXT;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool AsyncDXTCompression { get; set; } = true;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool ComprehensiveAuras { get; set; } = true;

        [DefaultValue(0.25f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float ComprehensiveAuraAlphaMultiplier { get; set; } = 0.25f;

        [DefaultValue(DrawingsResourceAllocationMode.Standard)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [JsonConverter(typeof(StringEnumConverter))]
        public DrawingsResourceAllocationMode DrawingsPerformance { get; set; } = DrawingsResourceAllocationMode.Standard;

        [DefaultValue(AudioCompressionPolicy.Always)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [JsonConverter(typeof(StringEnumConverter))]
        public AudioCompressionPolicy SoundCompressionPolicy { get; set; } = AudioCompressionPolicy.Always;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool HolidaySeasons { get; set; } = true;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool AsyncTextureUploading { get; set; } = true;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool MultithreadedTextureCompression { get; set; } = true;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool OffscreenParticleUpdates { get; set; } = true;

        [DefaultValue(GLContextHandlingMode.Checked)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public GLContextHandlingMode ContextHandlingMode { get; set; } = GLContextHandlingMode.Checked;

        [DefaultValue(0.75f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float Shadows2DAdmin { get; set; } = 0.75f;

        [DefaultValue(Shadow2DResolution.Medium)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [JsonConverter(typeof(StringEnumConverter))]
        public Shadow2DResolution Shadow2DPrecision { get; set; } = Shadow2DResolution.Medium;

        [DefaultValue(6)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int TurnTrackerSize { get; set; } = 6;

        [DefaultValue(TurnTrackerScaling.Medium)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [JsonConverter(typeof(StringEnumConverter))]
        public TurnTrackerScaling TurnTrackerScale { get; set; } = TurnTrackerScaling.Medium;

        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool AsyncAssetLoading { get; set; } = false;

        [DefaultValue(GLContextHandlingMode.Explicit)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public GLContextHandlingMode AudioContextHandlingMode { get; set; } = GLContextHandlingMode.Explicit;

        [DefaultValue(3)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int NumAsyncTextureBuffers { get; set; } = 3;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool ChatDiceEnabled { get; set; }

        [DefaultValue(0xff91faff)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public uint ColorD2 { get; set; } = 0xff91faff;

        [DefaultValue(ChatDiceColorMode.SetColor)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public ChatDiceColorMode ColorModeD2 { get; set; } = ChatDiceColorMode.SetColor;

        [DefaultValue(0xff7f7fff)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public uint ColorD4 { get; set; } = 0xff7f7fff;

        [DefaultValue(ChatDiceColorMode.SetColor)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public ChatDiceColorMode ColorModeD4 { get; set; } = ChatDiceColorMode.SetColor;

        [DefaultValue(0xff9bffee)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public uint ColorD6 { get; set; } = 0xff9bffee;

        [DefaultValue(ChatDiceColorMode.SetColor)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public ChatDiceColorMode ColorModeD6 { get; set; } = ChatDiceColorMode.SetColor;

        [DefaultValue(0xff97ff85)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public uint ColorD8 { get; set; } = 0xff97ff85;

        [DefaultValue(ChatDiceColorMode.SetColor)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public ChatDiceColorMode ColorModeD8 { get; set; } = ChatDiceColorMode.SetColor;

        [DefaultValue(0xfffaff85)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public uint ColorD10 { get; set; } = 0xfffaff85;

        [DefaultValue(ChatDiceColorMode.SetColor)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public ChatDiceColorMode ColorModeD10 { get; set; } = ChatDiceColorMode.SetColor;

        [DefaultValue(0xffd881ff)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public uint ColorD12 { get; set; } = 0xffd881ff;

        [DefaultValue(ChatDiceColorMode.SetColor)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public ChatDiceColorMode ColorModeD12 { get; set; } = ChatDiceColorMode.SetColor;

        [DefaultValue(0xfffe7c71)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public uint ColorD20 { get; set; } = 0xfffe7c71;

        [DefaultValue(ChatDiceColorMode.SetColor)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public ChatDiceColorMode ColorModeD20 { get; set; } = ChatDiceColorMode.SetColor;

        [DefaultValue(0xffffffff)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public uint ColorD100 { get; set; } = 0xffffffff;

        [DefaultValue(ChatDiceColorMode.SetColor)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public ChatDiceColorMode ColorModeD100 { get; set; } = ChatDiceColorMode.SetColor;

        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool UnifyChatDiceRendering { get; set; } = false;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public List<ChatDiceRollMemory> DiceRollMemory { get; set; } = new List<ChatDiceRollMemory>();

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool DrawSidebarLayerControls { get; set; } = true;

        [DefaultValue(16)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int UIDrawBuffersCapacity { get; set; } = 16;

        [DefaultValue(2f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float CameraInterpolationSpeed { get; set; } = 2f;


        public void IncrementKnownMemoryValue(ChatDiceRollMemory mem, int lastChatIndex)
        {
            mem.UseFrequency = lastChatIndex;
            this.DiceRollMemory.Sort();
            this.Save();
        }

        public void AddDiceRollMemory(string key, string value, DiceRollInformation data, int lastChatIndex)
        {
            if (this.DiceRollMemory == null)
            {
                this.DiceRollMemory = new List<ChatDiceRollMemory>();
            }

            foreach (ChatDiceRollMemory mem in this.DiceRollMemory)
            {
                if (string.Equals(mem.Key, key))
                {
                    mem.UseFrequency += 1;
                    this.DiceRollMemory.Sort();
                    this.Save();
                    return;
                }
            }

            // If we are here no such memory was found
            this.DiceRollMemory.Add(new ChatDiceRollMemory() { Key = key, Value = value, UseFrequency = lastChatIndex, RollInfo = data });
            this.DiceRollMemory.Sort();
            this.Save();
        }

        public void RemoveDiceRollMemory(string key)
        {
            if (this.DiceRollMemory == null)
            {
                this.DiceRollMemory = new List<ChatDiceRollMemory>();
                this.Save();
                return;
            }

            this.DiceRollMemory.RemoveAll(x => string.Equals(x.Key, key));
            this.DiceRollMemory.Sort();
            this.Save();
        }

        public static ClientSettings Load()
        {
            string expectedLocation = Path.Combine(IOVTT.ClientDir, "Settings.json");
            try
            {
                ClientSettings settings = JsonConvert.DeserializeObject<ClientSettings>(File.ReadAllText(expectedLocation));
                if (settings.Sensitivity is < 0.1f or > 10f)
                {
                    settings.Sensitivity = 1.0f;
                }

                return settings;
            }
            catch
            {
                Client.Instance.Logger.Log(LogLevel.Warn, "No client settings could be loaded, creating defaults");
            }

            Random random = new Random();
            ClientSettings ret = new ClientSettings()
            {
                IsFullscreen = false,
                Resolution = new Size(1366, 768),
                VSync = VSyncMode.Off,
                Name = Environment.UserName,
                Color = ((Color)new HSVColor((float)(random.NextDouble() * 360f), 1f, 1f)).Argb(),
                EnableSunShadows = true,
                EnableDirectionalShadows = true,
                DisableShaderBranching = false,
                FOWAdmin = 0.75f,
                Sensitivity = 1.0f,
                ChatBackgroundBrightness = 0.0f,
                Language = "en-EN",
                InterfaceSkin = UISkin.Dark,
                PointShadowsQuality = GraphicsSetting.Medium,
                ScreenMode = FullscreenMode.Normal,
                RaycastMultithreading = RaycastMultithreadingType.Eager,
                LastConnectIPAddress = string.Empty,
                LastConnectPort = string.Empty,
                DebugSettingsEnabled = false,
                ParticlesEnabled = true,
                ShadowsPCF = 2,
                Gamma = 2.2f,
                UseUBO = true,
                EnableCustomShaders = true,
                FOV = 60.0f,
                TurnTrackerParticlesEnabled = false,
                TextThickDropShadow = true,
                SoundMasterVolume = 1.0f,
                SoundUIVolume = 1.0f,
                SoundMapFXVolume = 1.0f,
                SoundAmbianceVolume = 1.0f,
                SoundMusicVolume = 1.0f,
                EnableSoundChatMessage = true,
                EnableChatNotification = true,
                EnableSoundTurnTracker = true,
                EnableSoundPing = true,
                DisableSounds = false,
                CompressionPreference = TextureCompressionPreference.DXT,
                AsyncDXTCompression = true,
                DrawingsPerformance = DrawingsResourceAllocationMode.Standard,
                SoundCompressionPolicy = AudioCompressionPolicy.Always,
                HolidaySeasons = true,
                AsyncTextureUploading = true,
                MultithreadedTextureCompression = true,
                OffscreenParticleUpdates = true,
                ContextHandlingMode = GLContextHandlingMode.Checked,
                Shadows2DAdmin = 0.75f,
                Shadow2DPrecision = Shadow2DResolution.Medium,
                TurnTrackerSize = 6,
                TurnTrackerScale = TurnTrackerScaling.Medium,
                AsyncAssetLoading = false,
                AudioContextHandlingMode = GLContextHandlingMode.Explicit,
                NumAsyncTextureBuffers = 3,
                ChatDiceEnabled = true,
                ColorD2 = 0xff91faff,
                ColorModeD2 = ChatDiceColorMode.SetColor,
                ColorD4 = 0xff7f7fff,
                ColorModeD4 = ChatDiceColorMode.SetColor,
                ColorD6 = 0xff9bffee,
                ColorModeD6 = ChatDiceColorMode.SetColor,
                ColorD8 = 0xff97ff85,
                ColorModeD8 = ChatDiceColorMode.SetColor,
                ColorD10 = 0xfffaff85,
                ColorModeD10 = ChatDiceColorMode.SetColor,
                ColorD12 = 0xffd881ff,
                ColorModeD12 = ChatDiceColorMode.SetColor,
                ColorD20 = 0xfffe7c71,
                ColorModeD20 = ChatDiceColorMode.SetColor,
                ColorD100 = 0xffffffff,
                ColorModeD100 = ChatDiceColorMode.SetColor,
                UnifyChatDiceRendering = false,
                DiceRollMemory = new List<ChatDiceRollMemory>(),
                DrawSidebarLayerControls = true,
                UIDrawBuffersCapacity = 16,
                CameraInterpolationSpeed = 2
            };

            ret.Save();
            return ret;
        }

        public void Save()
        {
            Client.Instance.Logger.Log(LogLevel.Info, "Saved client settings");
            string expectedLocation = Path.Combine(IOVTT.ClientDir, "Settings.json");
            File.WriteAllText(expectedLocation, JsonConvert.SerializeObject(this));
        }

        public enum GLContextHandlingMode
        {
            Implicit,
            Checked,
            Explicit
        }

        public enum VSyncMode
        {
            Off,
            On,
            Adaptive
        }

        public enum FullscreenMode
        {
            Normal,
            Fullscreen,
            Borderless
        }

        public enum GraphicsSetting
        {
            Low,        // 128x128
            Medium,     // 256x256
            High,       // 512x512
            Ultra       // 1024x1024
        }

        public enum RaycastMultithreadingType
        {
            Always,
            Eager,
            Cautious,
            Never
        }

        public enum UISkin
        {
            Light,
            Dark,
            Classic,
            SharpGray,
            DarkRounded,
            Source,
            HumanRevolution,
            DeepHell,
            VisualStudio,
            UnityDark,
            MSLight,
            Cherry,
            Photoshop
        }

        public enum UnfocusedFramerateCap
        {
            None,
            Native,
            High,
            Medium,
            Low,
            Lowest
        }

        public enum MSAAMode
        {
            Disabled,
            Low,
            Standard,
            High
        }

        public enum TextureCompressionPreference
        {
            Disabled,
            BPTC,
            DXT
        }

        public enum DrawingsResourceAllocationMode
        {
            None,
            Minimum,
            Limited,
            Standard,
            Extra,
            Unlimited
        }

        public enum AudioCompressionPolicy
        {
            Always,
            LargeFilesOnly,
            Never
        }

        public enum Shadow2DResolution
        {
            Low,
            Medium,
            High,
            Full
        }

        public enum TurnTrackerScaling
        {
            Smaller,
            Medium,
            Larger
        }

        public enum ChatDiceColorMode
        {
            SetColor,
            OwnColor,
            SenderColor,
        }
    }

    public class MPClientData
    {
        public Guid ID { get; set; }
        public string Name { get; set; }
        public Color Color { get; set; }
    }

    public readonly struct ClientSize
    {
        public int Width { get; }
        public int Height { get; }

        public ClientSize(int width, int height)
        {
            this.Width = width;
            this.Height = height;
        }

        public static implicit operator Size(ClientSize self) => new Size(self.Width, self.Height);
        public static implicit operator ClientSize(Size self) => new ClientSize(self.Width, self.Height);
    }

    public class MPMapPointer : IComparable<MPMapPointer>
    {
        public string Name { get; set; }
        public bool IsMap { get; set; }
        public Guid MapID { get; set; }
        public List<MPMapPointer> Elements { get; } = new List<MPMapPointer>();

        public int CompareTo(MPMapPointer other) => this.Name.CompareTo(other.Name);

        public void RecursivelySort()
        {
            this.Elements.Sort();
            foreach (MPMapPointer mpmp in this.Elements)
            {
                mpmp.RecursivelySort();
            }
        }
    }

    public class ChatDiceRollMemory : IComparable<ChatDiceRollMemory>
    {
        public int UseFrequency { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public DiceRollInformation RollInfo { get; set; }

        public int CompareTo(ChatDiceRollMemory other) => other.UseFrequency.CompareTo(this.UseFrequency);

        public readonly struct DiceRollInformation
        {
            public int NumDice { get; }
            public int DieSide { get; }
            public int ExtraValue { get; }
            public bool IsCompound { get; }

            public DiceRollInformation(int numDice, int dieSide, int extraValue, bool isCompound)
            {
                this.NumDice = numDice;
                this.DieSide = dieSide;
                this.ExtraValue = extraValue;
                this.IsCompound = isCompound;
            }
        }
    }
}
