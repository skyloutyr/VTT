namespace VTT.Network
{
    using NetCoreServer;
    using Newtonsoft.Json;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using VTT.Asset;
    using VTT.Control;
    using VTT.Network.Packet;
    using VTT.Network.UndoRedo;
    using VTT.Util;

    public class Server : TcpServer
    {
        public static Server Instance { get; set; }

        public Guid ID { get; set; }
        public ServerSettings Settings { get; set; }
        public Logger Logger { get; set; }


        public object clientsLock = new object();
        public ConcurrentDictionary<Guid, ServerClient> ClientsByID { get; } = new ConcurrentDictionary<Guid, ServerClient>();

        public AssetManager AssetManager { get; } = new AssetManager() { IsServer = true };
        public MusicPlayer MusicPlayer { get; } = new MusicPlayer();
        private Dictionary<Guid, ServerMapPointer> Maps { get; } = new Dictionary<Guid, ServerMapPointer>();

        public object mapsLock = new object();

        public ConcurrentDictionary<Guid, ClientInfo> ClientInfos { get; } = new ConcurrentDictionary<Guid, ClientInfo>();

        public object chatLock = new object();
        public List<ChatLine> ServerChat { get; } = new List<ChatLine>();
        public ConcurrentQueue<ChatLine> AppendedChat { get; } = new ConcurrentQueue<ChatLine>();
        public ConcurrentDictionary<Guid, TextJournal> Journals { get; } = new ConcurrentDictionary<Guid, TextJournal>();

        public AutoResetEvent WaitHandle { get; set; }

        public Guid LocalAdminID { get; set; }

        public volatile bool running;

        public NetworkMonitor NetworkIn { get; } = new NetworkMonitor();
        public NetworkMonitor NetworkOut { get; } = new NetworkMonitor();

        public long TimeoutInterval { get; set; } = (long)TimeSpan.FromMinutes(1).TotalMilliseconds;

        public Server(IPAddress address, int port) : base(address, port)
        {
            Instance = this;
            this.MapsRoot = Path.Combine(IOVTT.ServerDir, "Maps");
        }

        public string MapsRoot { get; set; }

        public bool TryGetMap(Guid mapID, out Map map)
        {
            lock (this.mapsLock)
            {
                this.Maps.TryGetValue(mapID, out ServerMapPointer smp);
                map = this.GetOrLoadMap(smp);
            }

            return map != null;
        }

        public bool HasMap(Guid mapID) => this.Maps.ContainsKey(mapID);

        public Map GetExistingMap(Guid mapID)
        {
            this.TryGetMap(mapID, out Map m);
            return m;
        }

        public bool TryGetMapPointer(Guid mapID, out ServerMapPointer smp)
        {
            lock (this.mapsLock)
            {
                return this.Maps.TryGetValue(mapID, out smp);
            }
        }

        public IEnumerable<ServerMapPointer> EnumerateMapData()
        {
            lock (this.mapsLock) // Need a lock in case of collection changes
            {
                foreach (ServerMapPointer smp in this.Maps.Values)
                {
                    yield return smp;
                }
            }

            yield break;
        }

        public bool AddMap(Map m)
        {
            lock (this.mapsLock)
            {
                ServerMapPointer smp = new ServerMapPointer(m.ID, m.Name, m.Folder, Path.Combine(this.MapsRoot, m.ID + ".ued")) { Loaded = true, Map = m };
                return this.Maps.TryAdd(m.ID, smp);
            }
        }

        public void RemoveMap(Guid mapID)
        {
            lock (this.mapsLock)
            {
                if (this.Maps.Remove(mapID, out ServerMapPointer smp))
                {
                    string mapsLoc = Path.Combine(IOVTT.ServerDir, "Maps");
                    Directory.CreateDirectory(mapsLoc);
                    string fileLoc = Path.Combine(mapsLoc, smp.MapID + ".ued");
                    string jsLoc = Path.Combine(mapsLoc, smp.MapID + ".json");
                    if (File.Exists(fileLoc))
                    {
                        File.Delete(fileLoc);
                    }

                    if (File.Exists(jsLoc))
                    {
                        File.Delete(jsLoc);
                    }

                    if (File.Exists(fileLoc + ".bak"))
                    {
                        File.Delete(fileLoc + ".bak");
                    }
                }
            }
        }

        public void Create(AutoResetEvent wh = null)
        {
            ID = Guid.NewGuid();
            LogLevel ll = LogLevel.Off;
            if (!ArgsManager.TryGetValue("loglevel", out ll))
            {
                ll = LogLevel.Off;
            }

            if (ArgsManager.TryGetValue("timeout", out long ti))
            {
                this.TimeoutInterval = ti;
            }

            if (ArgsManager.TryGetValue("servercache", out long sc))
            {
                if (sc == -1)
                {
                    this.AssetManager.ServerAssetCache.Enabled = false;
                }
                else
                {
                    this.AssetManager.ServerAssetCache.Enabled = true;
                    this.AssetManager.ServerAssetCache.MaxCacheLength = sc;
                }
            }

            AppDomain.CurrentDomain.ProcessExit += this.Cleanup;
            this.Logger = new Logger() { Prefix = "Server", TimeFormat = "HH:mm:ss.fff", ActiveLevel = ll };
            this.Logger.OnLog += Logger.Console;
            this.Logger.OnLog += Logger.Debug;
            Logger.FileLogListener fll = this._fll = new Logger.FileLogListener(IOVTT.OpenLogFile(true));
            this.Logger.OnLog += fll.WriteLine; this.Logger.Log(LogLevel.Info, DateTime.Now.ToString("ddd, dd MMM yyy HH’:’mm’:’ss ‘GMT’"));
            this.Logger.OnLog += VTTLogListener.Instance.WriteLine;
            this.Settings = ServerSettings.Load();
            this.AssetManager.Load();
            this.OptionNoDelay = true;
            this.OptionKeepAlive = true;
            this.LoadAllClients();
            this.LoadAllMaps();
            this.LoadChat();
            this.LoadJournals();
            this.LoadMusicPlayer();
            this.Logger.Log(LogLevel.Info, "Server creation complete");
            this.running = true;
            new Thread(this.RunWorker) { IsBackground = true, Priority = ThreadPriority.Lowest }.Start();
            this.Start();
            this.WaitHandle = wh;
        }

        private Logger.FileLogListener _fll;
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

        private void LoadJournals()
        {
            string loc = Path.Combine(IOVTT.ServerDir, "Journals");
            Directory.CreateDirectory(loc);
            foreach (string file in Directory.EnumerateFiles(loc))
            {
                if (file.EndsWith(".ued"))
                {
                    try
                    {
                        using Stream s = File.OpenRead(file);
                        using BinaryReader br = new BinaryReader(s);
                        TextJournal tj = new TextJournal();
                        DataElement data = new DataElement(br);
                        tj.Deserialize(data);
                        this.Journals[tj.SelfID] = tj;
                    }
                    catch (Exception e)
                    {
                        this.Logger.Log(LogLevel.Error, "Error reading text journal at " + file);
                        this.Logger.Exception(LogLevel.Error, e);
                    }
                }
            }
        }

        private void LoadMusicPlayer()
        {
            string fLoc = Path.Combine(IOVTT.ServerDir, "music_player.ued");
            if (File.Exists(fLoc))
            {
                using Stream s = File.OpenRead(fLoc);
                using BinaryReader br = new BinaryReader(s);
                DataElement de = new DataElement(br);
                this.MusicPlayer.Deserialize(de);
            }
        }

        private readonly Stack<TextJournal> _deletions = new Stack<TextJournal>();
        private readonly Stack<ServerClient> _dcRequests = new Stack<ServerClient>();
        private int _kaTimer;
        protected void RunWorker()
        {
            string mapsLoc = Path.Combine(IOVTT.ServerDir, "Maps");
            while (this.running)
            {
                lock (this.mapsLock)
                {
                    foreach (ServerMapPointer smp in this.Maps.Values)
                    {
                        if (smp.Loaded)
                        {
                            Map m = smp.Map;
                            if (m.NeedsSave)
                            {
                                m.NeedsSave = false;
                                Directory.CreateDirectory(mapsLoc);
                                string fPath = Path.Combine(mapsLoc, m.ID.ToString() + ".ued");
                                string jPath = Path.Combine(mapsLoc, m.ID.ToString() + ".json");
                                if (File.Exists(fPath))
                                {
                                    File.Move(fPath, fPath + ".bak", true);
                                }

                                m.Save(fPath);
                                smp.MapName = m.Name;
                                smp.MapFolder = m.Folder;
                                File.WriteAllText(jPath, JsonConvert.SerializeObject(smp));
                            }

                            if (m.FOW != null && (m.FOW.NeedsSave || m.FOW.IsDeleted))
                            {
                                if (m.FOW.NeedsSave)
                                {
                                    Directory.CreateDirectory(mapsLoc);
                                    string name = m.ID.ToString() + "_fow.png";
                                    if (File.Exists(name))
                                    {
                                        File.Move(name, name + ".bak", true);
                                    }

                                    m.FOW.Write(Path.Combine(mapsLoc, name));
                                }

                                if (m.FOW.IsDeleted)
                                {
                                    string name = m.ID.ToString() + "_fow.png";
                                    name = Path.Combine(mapsLoc, name);
                                    if (File.Exists(name))
                                    {
                                        File.Delete(name);
                                    }

                                    if (File.Exists(name + ".bak"))
                                    {
                                        File.Delete(name + ".bak");
                                    }

                                    m.FOW = null;
                                }
                            }
                        }
                    }
                }

                if (!this.AppendedChat.IsEmpty)
                {
                    this.SaveChat();
                }

                foreach (TextJournal tj in this.Journals.Values)
                {
                    if (tj.NeedsSave)
                    {
                        string loc = Path.Combine(IOVTT.ServerDir, "Journals");
                        Directory.CreateDirectory(loc);
                        tj.Serialize().Write(Path.Combine(loc, tj.SelfID + ".ued"));
                        tj.NeedsSave = false;
                    }

                    if (tj.NeedsDeletion)
                    {
                        string loc = Path.Combine(IOVTT.ServerDir, "Journals");
                        Directory.CreateDirectory(loc);
                        loc = Path.Combine(loc, tj.SelfID + ".ued");
                        if (File.Exists(loc))
                        {
                            File.Delete(loc);
                        }

                        this._deletions.Push(tj);
                    }
                }

                while (this._deletions.Count > 0)
                {
                    this.Journals.TryRemove(this._deletions.Pop().SelfID, out _);
                }

                if (++this._kaTimer >= 5)
                {
                    this._kaTimer = 0;
                    lock (clientsLock)
                    {
                        new PacketKeepalivePing() { Side = true }.Broadcast();
                        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        foreach (ServerClient sc in this.ClientsByID.Values)
                        {
                            if (now - sc.LastPingResponseTime > this.TimeoutInterval)
                            {
                                new PacketDisconnectReason() { DCR = DisconnectReason.Timeout }.Send(sc);
                                this._dcRequests.Push(sc);
                            }
                        }
                    }

                    while (this._dcRequests.Count > 0)
                    {
                        this._dcRequests.Pop().Disconnect();
                    }
                }

                this.AssetManager?.ServerSoundHeatmap?.Pulse();

                if (this.MusicPlayer.NeedsSave)
                {
                    this.MusicPlayer.Serialize().Write(Path.Combine(IOVTT.ServerDir, "music_player.ued"));
                    this.MusicPlayer.NeedsSave = false;
                }

                Thread.Sleep(1000);
            }
        }

        public void LoadAllClients()
        {
            string clientsLoc = Path.Combine(IOVTT.ServerDir, "Clients");
            this.ClientInfos[Guid.Empty] = ClientInfo.Empty;

            if (Directory.Exists(clientsLoc))
            {
                foreach (string file in Directory.EnumerateFiles(clientsLoc))
                {
                    Guid id = Guid.Parse(Path.GetFileNameWithoutExtension(file));
                    ClientInfo ci = JsonConvert.DeserializeObject<ClientInfo>(File.ReadAllText(file));
                    ci.ID = id;
                    this.ClientInfos[id] = ci;
                }
            }
        }

        public Guid GetAnyAdmin()
        {
            if (!Equals(Guid.Empty, this.LocalAdminID))
            {
                return this.LocalAdminID;
            }

            foreach (ClientInfo ci in this.ClientInfos.Values)
            {
                if (ci.IsAdmin)
                {
                    return ci.ID;
                }
            }

            return Guid.Empty;
        }

        public ClientInfo GetOrCreateClientInfo(Guid id)
        {
            if (this.ClientInfos.TryGetValue(id, out ClientInfo ci) && ci != null)
            {
                return ci;
            }

            ci = new ClientInfo() { ID = id };
            this.ClientInfos.TryAdd(id, ci);
            return ci;
        }

        public void LoadChat()
        {
            string chatLoc = Path.Combine(IOVTT.ServerDir, "Chat");
            string expectedFile = Path.Combine(chatLoc, "log.bin");
            bool TryLoadChat(string path, out List<ChatLine> outLst)
            {
                List<ChatLine> ret = new List<ChatLine>();
                outLst = ret;
                if (File.Exists(path))
                {
                    try
                    {
                        using (BinaryReader br = new BinaryReader(File.OpenRead(path)))
                        {
                            if (br.BaseStream.CanRead && br.BaseStream.Length > 0)
                            {
                                int idx = 0;
                                while (br.BaseStream.Position < br.BaseStream.Length - 1)
                                {
                                    ChatLine cl = new ChatLine() { Index = idx++ };
                                    cl.Read(br);
                                    ret.Add(cl);
                                }

                                return true;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        this.Logger.Log(LogLevel.Error, "Could not load server chat!");
                        this.Logger.Exception(LogLevel.Error, e);
                    }
                }

                return false;
            }

            if (TryLoadChat(expectedFile, out List<ChatLine> lines))
            {
                this.ServerChat.AddRange(lines);
            }
            else
            {
                string pathBak = expectedFile + ".bak";
                if (File.Exists(pathBak))
                {
                    File.Move(pathBak, expectedFile, true);
                    if (TryLoadChat(expectedFile, out lines))
                    {
                        this.ServerChat.AddRange(lines);
                    }
                }
            }
        }

        public void SaveChat()
        {
            string chatLoc = Path.Combine(IOVTT.ServerDir, "Chat");
            string expectedFile = Path.Combine(chatLoc, "log.bin");
            Directory.CreateDirectory(chatLoc);
            if (!File.Exists(expectedFile))
            {
                File.Create(expectedFile).Dispose();
            }
            else
            {
                File.Copy(expectedFile, expectedFile + ".bak", true);
            }

            using (FileStream fs = new FileStream(expectedFile, FileMode.Append))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    while (!this.AppendedChat.IsEmpty)
                    {
                        if (this.AppendedChat.TryDequeue(out ChatLine cl))
                        {
                            cl.Write(bw);
                        }
                    }

                    bw.Flush();
                }
            }
        }

        public void LoadAllMaps()
        {
            this.Logger.Log(LogLevel.Info, "Scanning for maps at " + this.MapsRoot);
            Directory.CreateDirectory(this.MapsRoot);
            Dictionary<Guid, (string, string)> mapsPtrs = new Dictionary<Guid, (string, string)>();
            foreach (string file in Directory.EnumerateFiles(this.MapsRoot))
            {
                if (file.EndsWith(".ued"))
                {
                    try
                    {
                        Guid gId = Guid.Parse(Path.GetFileNameWithoutExtension(file));
                        if (mapsPtrs.TryGetValue(gId, out (string, string) dat))
                        {
                            // Have value, update it
                            mapsPtrs[gId] = (file, dat.Item2);
                        }
                        else
                        {
                            mapsPtrs[gId] = (file, string.Empty);
                        }

                        this.Logger.Log(LogLevel.Info, "Found map candidate " + file);
                    }
                    catch
                    {
                        this.Logger.Log(LogLevel.Error, "A file in the maps folder doesn't follow correct map filename format - " + file);
                    }
                }

                if (file.EndsWith(".ued.bak"))
                {
                    try
                    {
                        string fnext = Path.GetFileNameWithoutExtension(file);
                        fnext = fnext[..^4];
                        Guid gId = Guid.Parse(fnext);
                        if (mapsPtrs.TryGetValue(gId, out (string, string) dat))
                        {
                            // Have value, update it
                            mapsPtrs[gId] = (dat.Item1, file);
                        }
                        else
                        {
                            mapsPtrs[gId] = (string.Empty, file);
                        }
                    }
                    catch
                    {
                        this.Logger.Log(LogLevel.Error, "A backup file in the maps folder doesn't follow correct map filename format - " + file);
                    }
                }
            }

            Parallel.ForEach(mapsPtrs, kv =>
            {
                string file, js;
                if (string.IsNullOrEmpty(kv.Value.Item1) || !File.Exists(kv.Value.Item1)) // Do not have main map data
                {
                    if (!string.IsNullOrEmpty(kv.Value.Item2) && File.Exists(kv.Value.Item2)) // But have the backup
                    {
                        this.Logger.Log(LogLevel.Warn, $"Map {kv.Key} doesn't have main data. Restoring backup!");
                        file = kv.Value.Item2;
                        js = file.Replace(".ued.bak", ".json");
                        File.Copy(file, file[..^4]); // Restore backup
                    }
                    else
                    {
                        // Unreachable, but maybe fs shenanigans?
                        return;
                    }
                }
                else
                {
                    file = kv.Value.Item1;
                    js = file.Replace(".ued", ".json");
                }

                Guid mID = kv.Key;
                if (!File.Exists(js))
                {
                    ServerMapPointer smp = new ServerMapPointer(mID, string.Empty, string.Empty, file);
                    Map m = this.LoadMap(smp);
                    // Create a json for map metadata
                    smp.MapName = m.Name;
                    smp.MapFolder = m.Folder;
                    File.WriteAllText(js, JsonConvert.SerializeObject(smp));
                    lock (this.mapsLock)
                    {
                        this.Maps.Add(mID, smp);
                    }
                }
                else
                {
                    ServerMapPointer smp = JsonConvert.DeserializeObject<ServerMapPointer>(File.ReadAllText(js));
                    if (smp.MapID != mID) // Uh-oh, IDs don't match
                    {
                        this.Logger.Log(LogLevel.Error, $"Map IDs don't match physical IDs - expected {mID}, got {smp.MapID}!");
                        this.Logger.Log(LogLevel.Error, $"This will cause issues as it means map's ID doesn't match that of the fs. Map can't be loaded.");
                        smp.MapID = mID;
                        smp.Valid = false;
                    }
                    else
                    {
                        smp.Loaded = false;
                        smp.Valid = true;
                        smp.MapDiskLocation = file;
                    }

                    lock (this.mapsLock)
                    {
                        this.Maps.Add(mID, smp);
                    }
                }
            });

            if (!this.Maps.TryGetValue(this.Settings.DefaultMapID, out ServerMapPointer defaultMapPtr)) // Have a default map setup, but no such map exists, setup a default one
            {
                this.Logger.Log(LogLevel.Warn, "Default map ID exists, but no map was found, creating empty");
                Map m = new Map()
                {
                    IsServer = true,
                    ID = this.Settings.DefaultMapID,
                    BackgroundColor = Color.Black,
                    GridColor = Color.White,
                    GridEnabled = true,
                    SunEnabled = true,
                    GridUnit = 5,
                    GridSize = 1,
                    Name = "New Map"
                };

                ServerMapPointer smp = new ServerMapPointer(m.ID, m.Name, m.Folder, Path.Combine(this.MapsRoot, m.ID.ToString() + ".ued"));
                Directory.CreateDirectory(this.MapsRoot);
                m.Save(smp.MapDiskLocation);
                File.WriteAllText(Path.Combine(this.MapsRoot, m.ID.ToString() + ".json"), JsonConvert.SerializeObject(smp));
                this.Maps[m.ID] = smp;
            }
            else
            {
                this.LoadMap(defaultMapPtr);
            }
        }

        private Map GetOrLoadMap(ServerMapPointer mapPointer) => mapPointer == null ? null : mapPointer.Loaded ? mapPointer.Map : mapPointer.Valid ? this.LoadMap(mapPointer) : null;

        private Map LoadMap(ServerMapPointer mapPointerData)
        {
            Map m = null;
            string file = mapPointerData.MapDiskLocation;
            try
            {
                using BinaryReader br = new BinaryReader(File.OpenRead(file));
                m = new Map() { IsServer = true };
                DataElement de = new DataElement();
                de.Read(br);
                m.Deserialize(de);
                this.Logger.Log(LogLevel.Info, "Loaded map " + m.ID + "(" + m.Name + ")");
            }
            catch
            {
                this.Logger.Log(LogLevel.Error, "Map could not be loaded.");
                string bF = file + ".bak";
                if (File.Exists(bF))
                {
                    try
                    {
                        this.Logger.Log(LogLevel.Info, "Trying to load map backup.");
                        using BinaryReader br = new BinaryReader(File.OpenRead(bF));
                        m = new Map() { IsServer = true };
                        DataElement de = new DataElement();
                        de.Read(br);
                        m.Deserialize(de);
                        this.Logger.Log(LogLevel.Info, "Loaded map " + m.ID + "(" + m.Name + ")");
                    }
                    catch
                    {
                        this.Logger.Log(LogLevel.Error, "Map backup could not be loaded.");
                        return null;
                    }
                }
            }

            if (m == null)
            {
                return null;
            }

            mapPointerData.Loaded = true;
            mapPointerData.Map = m;

            string expectedFOW = Path.Combine(this.MapsRoot, m.ID + "_fow.png");
            if (File.Exists(expectedFOW))
            {
                this.Logger.Log(LogLevel.Info, "Map FOW canvas loaded");
                FOWCanvas fowc = new FOWCanvas();
                try
                {
                    fowc.Read(expectedFOW);
                    m.FOW = fowc;
                }
                catch
                {
                    this.Logger.Log(LogLevel.Error, "FOW canvas could not be loaded, trying to load backup.");
                    expectedFOW = Path.Combine(this.MapsRoot, m.ID + "_fow.png.bak");
                    if (File.Exists(expectedFOW))
                    {
                        fowc = new FOWCanvas();
                        try
                        {
                            fowc.Read(expectedFOW);
                            m.FOW = fowc;
                        }
                        catch
                        {
                            this.Logger.Log(LogLevel.Error, "FOW canvas backup could not be loaded.");
                            m.FOW = null;
                        }
                    }
                    else
                    {
                        m.FOW = null;
                    }
                }
            }

            return m;
        }


        protected override TcpSession CreateSession() => new ServerClient(this);

        protected override void OnStopped()
        {
            this.running = false;
            this.Logger.Log(LogLevel.Info, "Server shut down");
            this.WaitHandle?.Set();
        }

        protected override void OnError(SocketError error)
        {
            base.OnError(error);
            this.Logger.Log(LogLevel.Error, "Server TCP socket error " + error);
        }

        public void Delete()
        {
            this.Stop();
            this.running = false;
            this.Logger.Log(LogLevel.Info, "Server shut down");
        }
    }

    public class ServerClient : TcpSession
    {
        public ClientInfo Info { get; set; }
        public long LastPingResponseTime { get; set; }
        public ActionMemory ActionMemory { get; set; }

        public Guid ID
        {
            get => this.Info.ID;
            set => this.Info.ID = value;
        }

        public Guid ClientMapID
        {
            get => this.Info.MapID;
            set => this.Info.MapID = value;
        }

        public bool IsAdmin
        {
            get => this.Info.IsAdmin;
            set => this.Info.IsAdmin = true;
        }

        public bool IsObserver
        {
            get => this.Info.IsObserver;
            set => this.Info.IsObserver = value;
        }

        public bool CanDraw
        {
            get => this.Info.CanDraw;
            set => this.Info.CanDraw = value;
        }

        public PacketNetworkManager LocalNetManager { get; set; }

        public string Name
        {
            get => this.Info.Name;
            set => this.Info.Name = value;
        }
        public Color Color
        {
            get => this.Info.Color;
            set => this.Info.Color = value;
        }

        public Server Container { get; set; }

        public ServerClient(TcpServer server) : base(server)
        {
            this.Container = (Server)server;
            this.LocalNetManager = new PacketNetworkManager() { IsServer = true };
            this.ActionMemory = new ActionMemory(this);
        }

        public void SetClientInfo(ClientInfo info)
        {
            this.Info = info;
            this.EnsureDataCorrectness();
        }

        protected override void OnError(SocketError error)
        {
            base.OnError(error);
            Network.Server.Instance.Logger.Log(LogLevel.Warn, "Client socket errpr " + error);
        }

        protected override void OnConnected() // C->S connection
        {
            Network.Server.Instance.Logger.Log(LogLevel.Info, "Client connected with session id " + this.Id.ToString());
            Network.Server.Instance.Logger.Log(LogLevel.Info, "Connecting client IP address is " + this.Socket.RemoteEndPoint.ToString());
            base.OnConnected();
            this.LastPingResponseTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        protected override void OnDisconnected()
        {
            Network.Server.Instance.Logger.Log(LogLevel.Info, "Client disconnected");
            base.OnDisconnected();
            Guid id = this.ID;
            Network.Server.Instance.ClientsByID.TryRemove(id, out ServerClient sc);
            if (sc != null)
            {
                sc.Info.IsLoggedOn = false;
            }
            else
            {
                if (this.Info != null)
                {
                    this.Info.IsLoggedOn = false;
                }
            }

            new PacketClientOnlineNotification() { ClientID = sc?.ID ?? id, Status = false }.Broadcast();
        }

        protected override void OnSent(long sent, long pending)
        {
            base.OnSent(sent, pending);
            this.Container.NetworkOut.Increment(sent);
        }


        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            this.Container.NetworkIn.Increment(size);
            base.OnReceived(buffer, offset, size);
            foreach (PacketBase packet in this.LocalNetManager.Receive(buffer, offset, size))
            {
                packet.Sender = this;
                packet.Act(this.Id, (Server)this.Server, null, true);
            }
        }

        public void EnsureDataCorrectness()
        {
            if (this.ClientMapID.Equals(Guid.Empty))
            {
                this.ClientMapID = this.Container.Settings.DefaultMapID;
                this.SaveClientData();
            }
            else
            {
                if (!this.Container.TryGetMap(this.ClientMapID, out _))
                {
                    this.ClientMapID = this.Container.Settings.DefaultMapID;
                    this.SaveClientData();
                }
            }

            bool aOld = this.IsAdmin;
            this.IsAdmin = Equals(this.ID, Network.Server.Instance.LocalAdminID);
            if (this.IsAdmin != aOld)
            {
                this.SaveClientData();
            }
        }

        public void SaveClientData()
        {
            string clientLoc = Path.Combine(IOVTT.ServerDir, "Clients", this.ID.ToString() + ".json");
            Directory.CreateDirectory(Path.Combine(IOVTT.ServerDir, "Clients"));
            try
            {
                File.WriteAllText(clientLoc, JsonConvert.SerializeObject(this.Info));
                Network.Server.Instance.Logger.Log(LogLevel.Debug, "Client data for " + this.ID + " saved");
            }
            catch (Exception e)
            {
                Network.Server.Instance.Logger.Log(LogLevel.Error, "Could not save client data for " + this.ID);
                Network.Server.Instance.Logger.Exception(LogLevel.Error, e);
            }
        }
    }

    public class ServerSettings
    {
        [JsonConverter(typeof(GUIDConverter))]
        public Guid DefaultMapID { get; set; }

        public static ServerSettings Load()
        {
            string expectedLocation = Path.Combine(IOVTT.ServerDir, "Settings.json");
            try
            {
                return JsonConvert.DeserializeObject<ServerSettings>(File.ReadAllText(expectedLocation));
            }
            catch
            {
                Server.Instance.Logger.Log(LogLevel.Warn, "Server settings don't exist, creating defaults");
            }

            ServerSettings ret = new ServerSettings()
            {
                DefaultMapID = Guid.NewGuid()
            };

            ret.Save();
            return ret;
        }

        public void Save()
        {
            Server.Instance.Logger.Log(LogLevel.Info, "Saved server settings");
            string expectedLocation = Path.Combine(IOVTT.ServerDir, "Settings.json");
            File.WriteAllText(expectedLocation, JsonConvert.SerializeObject(this));
        }
    }

    public class ServerMapPointer
    {
        public Guid MapID { get; set; }
        public string MapName { get; set; }
        public string MapFolder { get; set; }

        [JsonIgnore]
        public string MapDiskLocation { get; set; }

        [JsonIgnore]
        public bool Loaded { get; set; }

        [JsonIgnore]
        public Map Map { get; set; }

        [JsonIgnore]
        public bool Valid { get; set; } = true;

        public ServerMapPointer(Guid mapID, string mapName, string mapFolder, string mapDiskLocation)
        {
            this.MapID = mapID;
            this.MapName = mapName;
            this.MapFolder = mapFolder;
            this.MapDiskLocation = mapDiskLocation;
        }
    }
}
