namespace VTT.Network
{
    using NetCoreServer;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using OpenTK.Mathematics;
    using OpenTK.Windowing.Common;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Net;
    using VTT.Asset;
    using VTT.Control;
    using VTT.Network.Packet;
    using VTT.Network.VSCC;
    using VTT.Render;
    using VTT.Util;

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

        public SimpleLanguage Lang { get; } = new SimpleLanguage();

        public NetClient NetClient { get; set; }

        public AssetManager AssetManager { get; } = new AssetManager() { IsServer = false };

        public Map CurrentMap { get; set; }
        public ClientSettings Settings { get; set; }
        public ClientWindow Frontend { get; set; }

        public ConcurrentDictionary<Guid, TextJournal> Journals { get; } = new ConcurrentDictionary<Guid, TextJournal>();
        public ConcurrentDictionary<Guid, ClientInfo> ClientInfos { get; } = new ConcurrentDictionary<Guid, ClientInfo>();

        public object ServerMapPointersLock { get; } = new object();
        public SortedDictionary<string, List<(Guid, string)>> ServerMapPointers { get; } = new SortedDictionary<string, List<(Guid, string)>>();

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
            if (!ArgsManager.TryGetValue("loglevel", out LogLevel ll))
            {
                ll = LogLevel.Off;
            }

            if (ArgsManager.TryGetValue("timeout", out long ti))
            {
                this.TimeoutInterval = ti;
            }

            this.Logger = new Logger() { Prefix = "Client", TimeFormat = "HH:mm:ss.fff", ActiveLevel = ll };
            this.Logger.OnLog += Logger.Console;
            this.Logger.OnLog += Logger.Debug;
            Logger.FileLogListener fll = new Logger.FileLogListener(IOVTT.OpenLogFile(false));
            this.Logger.OnLog += fll.WriteLine;
            this.Logger.OnLog += VTTLogListener.Instance.WriteLine;
            this.Logger.Log(LogLevel.Info, DateTime.Now.ToString("ddd, dd MMM yyy HH:mm:ss GMT"));
            this.ID = IDUtil.GetDeviceID();
            this.Logger.Log(LogLevel.Info, "Self-assigned id is " + this.ID.ToString());
            this.Settings = ClientSettings.Load();
            if (this.Settings.Sensitivity is < 0.1f or > 10f)
            {
                this.Settings.Sensitivity = 1.0f;
            }

            this.Lang.LoadFile(this.Settings.Language ?? "en-EN");
            this.Frontend = new ClientWindow();
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
                    this.NetClient?.Disconnect();
                    this.NetClient?.Dispose();
                }
                finally
                {
                    this.NetClient = null;
                }

                lock (this.ServerMapPointersLock)
                {
                    this.ServerMapPointers.Clear();
                }

                this.ClientInfos.Clear();
                lock (this.chatLock)
                {
                    this.Chat.Clear();
                }

                this.SetCurrentMap(null, () => { });
                this.AssetManager.ClientAssetLibrary.Clear();
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
                    this.Frontend.Renderer.RulerRenderer.ActiveInfos.Clear();
                    this.Frontend.Renderer.RulerRenderer.ActiveInfos.AddRange(map.PermanentMarks);
                });
            }

            Map cMapRef = this.CurrentMap;
            this.DoTask(this.AssetManager.ClientAssetLibrary.ClearAssets);
            this.DoTask(this.Frontend.Renderer.PingRenderer.ClearPings);
            this.DoTask(() => this.Frontend.Renderer.ParticleRenderer.ClearParticles(cMapRef));
            this.DoTask(() => this.Frontend.Renderer.SelectionManager.BoxSelectCandidates.Clear());
            this.DoTask(() => this.Frontend.Renderer.SelectionManager.SelectedObjects.Clear());
            this.CurrentMap = map;
            if (map != null)
            {
                this.Logger.Log(LogLevel.Info, "Current map set to " + map.ID + "(" + map.Name + ")");
            }

            this.DoTask(postSetAction);
        }

        public void DoTask(Action a) => this.Frontend.ActionsToDo.Enqueue(a);
        public void SetDisconnectReason(DisconnectReason dCR)
        {
            this.LastDisconnectReason = dCR;
            if (this.Frontend?.Renderer?.GuiRenderer != null)
            {
                this.Frontend.Renderer.GuiRenderer.showDisconnect = true;
            }
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
            new PacketHandshake() { ClientID = this.Container.ID, Session = this.Id, IsServer = false, ClientVersion = Program.GetVersionBytes() }.Send(this);
            Client.Instance.Logger.Log(LogLevel.Info, "Sending handshake");
            this.Container.SessionID = this.Id;
            this.LastPingResponseTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        protected override void OnError(System.Net.Sockets.SocketError error)
        {
            Client.Instance.Logger.Log(LogLevel.Error, "NetSocket error " + error);
            this.Disconnect();
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
                this.Disconnect();
                this.Container.Disconnect(DisconnectReason.NetworkStateCorrupted);
            }
        }
    }

    public class ClientSettings
    {
        public bool IsFullscreen { get; set; }
        public ClientSize Resolution { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public VSyncMode VSync { get; set; }

        public string Name { get; set; }
        public uint Color { get; set; }
        public bool EnableSunShadows { get; set; }
        public bool EnableDirectionalShadows { get; set; }
        public bool DisableShaderBranching { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [DefaultValue(PipelineType.Deferred)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public PipelineType Pipeline { get; set; } = PipelineType.Deferred;

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

        public float FOWAdmin { get; set; }
        public string Language { get; set; }

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

        public bool DebugSettingsEnabled { get; set; } = false;

        public float Sensitivity { get; set; }
        public float ChatBackgroundBrightness { get; set; }

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool ParticlesEnabled { get; set; } = true;

        public static ClientSettings Load()
        {
            string expectedLocation = Path.Combine(IOVTT.ClientDir, "Settings.json");
            try
            {
                return JsonConvert.DeserializeObject<ClientSettings>(File.ReadAllText(expectedLocation));
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
                FOWAdmin = 1.0f,
                Sensitivity = 1.0f,
                ChatBackgroundBrightness = 0.0f,
                Language = "en-EN",
                InterfaceSkin = UISkin.Dark,
                Pipeline = PipelineType.Deferred,
                PointShadowsQuality = GraphicsSetting.Medium,
                ScreenMode = FullscreenMode.Normal,
                RaycastMultithreading = RaycastMultithreadingType.Eager,
                LastConnectIPAddress = string.Empty,
                LastConnectPort = string.Empty,
                DebugSettingsEnabled = false,
                ParticlesEnabled = true
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

        public enum PipelineType
        {
            Forward,
            Deferred
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
}
