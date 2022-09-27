namespace VTT.Network
{
    using NetCoreServer;
    using Newtonsoft.Json;
    using VTT.Util;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using VTT.Asset;
    using VTT.Control;
    using VTT.Network.Packet;

    public class Server : TcpServer
    {
        public static Server Instance { get; set; }

        public Guid ID { get; set; }
        public ServerSettings Settings { get; set; }
        public Logger Logger { get; set; }


        public object clientsLock = new object();
        public ConcurrentDictionary<Guid, ServerClient> ClientsByID { get; } = new ConcurrentDictionary<Guid, ServerClient>();

        public AssetManager AssetManager { get; } = new AssetManager() { IsServer = true };
        public Dictionary<Guid, Map> Maps { get; } = new Dictionary<Guid, Map>();

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

        public Server(IPAddress address, int port) : base(address, port) => Instance = this;

        public bool TryGetMap(Guid mapID, out Map map)
        {
            lock (this.mapsLock)
            {
                if (this.Maps.ContainsKey(mapID))
                {
                    map = this.Maps[mapID];
                    return true;
                }
            }

            map = null;
            return false;
        }

        public bool AddMap(Map m)
        {
            lock (this.mapsLock)
            {
                return this.Maps.TryAdd(m.ID, m);
            }
        }

        public void RemoveMap(Guid mapID)
        {
            lock (this.mapsLock)
            {
                if (this.Maps.Remove(mapID, out Map m))
                {
                    string mapsLoc = Path.Combine(IOVTT.ServerDir, "Maps");
                    Directory.CreateDirectory(mapsLoc);
                    string fileLoc = Path.Combine(mapsLoc, m.ID + ".ued");
                    if (File.Exists(fileLoc))
                    {
                        File.Delete(fileLoc);
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

            this.Logger = new Logger() { Prefix = "Client", TimeFormat = "HH:mm:ss.fff", ActiveLevel = ll };
            this.Logger.OnLog += Logger.Console;
            this.Logger.OnLog += Logger.Debug;
            Logger.FileLogListener fll = new Logger.FileLogListener(IOVTT.OpenLogFile(true));
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
            this.Logger.Log(LogLevel.Info, "Server creation complete");
            this.running = true;
            new Thread(this.RunWorker) { IsBackground = true, Priority = ThreadPriority.Lowest }.Start();
            this.Start();
            this.WaitHandle = wh;
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

        private readonly Stack<TextJournal> _deletions = new Stack<TextJournal>();
        private int _kaTimer;
        protected void RunWorker()
        {
            string mapsLoc = Path.Combine(IOVTT.ServerDir, "Maps");
            while (this.running)
            {
                lock (this.mapsLock)
                {
                    foreach (Map m in this.Maps.Values)
                    {
                        if (m.NeedsSave)
                        {
                            m.NeedsSave = false;
                            Directory.CreateDirectory(mapsLoc);
                            string fPath = Path.Combine(mapsLoc, m.ID.ToString() + ".ued");
                            if (File.Exists(fPath))
                            {
                                File.Move(fPath, fPath + ".bak", true);
                            }

                            m.Save(fPath);
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
                    }
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
            if (!Guid.Equals(Guid.Empty, this.LocalAdminID))
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
            if (File.Exists(expectedFile))
            {
                using (BinaryReader br = new BinaryReader(File.OpenRead(expectedFile)))
                {
                    int idx = 0;
                    while (br.BaseStream.Position < br.BaseStream.Length - 1)
                    {
                        ChatLine cl = new ChatLine() { Index = idx++ };
                        cl.Read(br);
                        this.ServerChat.Add(cl);
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
            string mapsLoc = Path.Combine(IOVTT.ServerDir, "Maps");
            this.Logger.Log(LogLevel.Info, "Scanning for maps at " + mapsLoc);
            Directory.CreateDirectory(mapsLoc);
            foreach (string file in Directory.EnumerateFiles(mapsLoc))
            {
                if (file.EndsWith(".ued"))
                {
                    Map m = null;
                    try
                    {
                        this.Logger.Log(LogLevel.Info, "Found map candidate " + file);
                        using BinaryReader br = new BinaryReader(File.OpenRead(file));
                        m = new Map() { IsServer = true };
                        DataElement de = new DataElement();
                        de.Read(br);
                        m.Deserialize(de);
                        this.Maps[m.ID] = m;
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
                                this.Maps[m.ID] = m;
                                this.Logger.Log(LogLevel.Info, "Loaded map " + m.ID + "(" + m.Name + ")");
                            }
                            catch
                            {
                                this.Logger.Log(LogLevel.Error, "Map backup could not be loaded.");
                                continue;
                            }
                        }
                    }

                    if (m == null)
                    {
                        continue;
                    }

                    string expectedFOW = Path.Combine(mapsLoc, m.ID + "_fow.png");
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
                            expectedFOW = Path.Combine(mapsLoc, m.ID + "_fow.png.bak");
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
                }
            }

            if (!this.Maps.ContainsKey(this.Settings.DefaultMapID)) // Have a default map setup, but no such map exists, setup a default one
            {
                this.Logger.Log(LogLevel.Warn, "Default map ID exists, but no map was found, creating empty");
                Map m = new Map() { 
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

                Directory.CreateDirectory(mapsLoc);
                m.Save(Path.Combine(mapsLoc, m.ID.ToString() + ".ued"));
                this.Maps[m.ID] = m;
            }
        }

        protected override TcpSession CreateSession() => new ServerClient(this);

        protected override void OnStopped()
        {
            this.running = false;
            this.Logger.Log(LogLevel.Info, "Server shut down");
            if (this.WaitHandle != null)
            {
                this.WaitHandle.Set();
            }
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
        }

        protected override void OnDisconnected()
        {
            Network.Server.Instance.Logger.Log(LogLevel.Info, "Client disconnected");
            base.OnDisconnected();
            Network.Server.Instance.ClientsByID.TryRemove(this.ID, out ServerClient sc);
            sc.Info.IsLoggedOn = false;
            new PacketClientOnlineNotification() { ClientID = sc.ID, Status = false }.Broadcast();
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
            this.IsAdmin = Guid.Equals(this.ID, Network.Server.Instance.LocalAdminID);
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
                Network.Server.Instance.Logger.Log(LogLevel.Warn, "Server settings don't exist, creating defaults");
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
            Network.Server.Instance.Logger.Log(LogLevel.Info, "Saved server settings");
            string expectedLocation = Path.Combine(IOVTT.ServerDir, "Settings.json");
            File.WriteAllText(expectedLocation, JsonConvert.SerializeObject(this));
        }
    }
}
