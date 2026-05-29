namespace VTT.Network
{
    using NetCoreServer;
    using Newtonsoft.Json;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.IO;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using VTT.Network.Packet;
    using VTT.Network.UndoRedo;
    using VTT.Util;

    public class ServerClient
    {
        public ClientInfo Info { get; set; }
        public long LastPingResponseTime { get; set; }
        public long PersonalTimeoutInterval { get; set; }
        public ActionMemory ActionMemory { get; set; }
        public bool IsAuthorized { get; set; }

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

        public Image<Rgba32> Image
        {
            get => this.Info.Image;
            set => this.Info.Image = value;
        }

        public Server Container { get; set; }
        public TcpSession Session { get; set; }
        public byte[] SessionAuthToken => this.Info?.SessionAuthToken;

        public ServerClient(TcpServer server, TcpSession session)
        {
            this.Container = (Server)server;
            this.Session = session;
            this.LocalNetManager = new PacketNetworkManager() { IsServer = true };
            this.ActionMemory = new ActionMemory(this);
            this.IsAuthorized = false;
        }

        public void SetClientInfo(ClientInfo info)
        {
            this.Info = info;
            this.EnsureDataCorrectness();
        }

        public void OnError(SocketError error) => Server.Instance.Logger.Log(LogLevel.Warn, "Client socket error " + error);

        public void OnConnected() // C->S connection
        {
            Server.Instance.Logger.Log(LogLevel.Info, "Client connected with session id " + this.Session.Id.ToString());
            Server.Instance.Logger.Log(LogLevel.Info, "Connecting client IP address is " + this.Session.Socket.RemoteEndPoint.ToString());
            this.LastPingResponseTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        public void OnDisconnected()
        {
            Server.Instance.Logger.Log(LogLevel.Info, "Client disconnected");
            if (this.Info != null)
            {
                Guid id = this.ID;
                Server.Instance.ClientsByID.TryRemove(id, out ServerClient sc);
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
        }

        public void OnSent(long sent, long pending)
        {
            this.Container.NetworkOut.Increment(sent);
        }

        public void OnReceived(byte[] buffer, long offset, long size)
        {
            this.Container.NetworkIn.Increment(size);
            foreach (PacketBase packet in this.LocalNetManager.Receive(buffer, offset, size))
            {
                if (!this.IsAuthorized && packet is not PacketHandshake)
                {
                    this.Container?.Logger.Log(LogLevel.Error, $"Client tried sending a packet before handshake, disconnecting!");
                    new PacketDisconnectReason() { DCR = DisconnectReason.ProtocolMismatch }.Send(this);
                    this.Session.Disconnect();
                    return;
                }

                packet.Sender = this;
                packet.Act(this.Session.Id, this.Container, null, true);
            }

            if (this.LocalNetManager.IsInvalidProtocol) // Do not notify the client of disconnection, since protocol is invalid
            {
                this.Container?.Logger.Log(LogLevel.Error, $"Client did not follow VTT's network protocol, disconnecting!");
                this.Session.Disconnect();
                return;
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
            this.IsAdmin = Equals(this.ID, Server.Instance.LocalAdminID);
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
                Server.Instance.Logger.Log(LogLevel.Debug, "Client data for " + this.ID + " saved");
            }
            catch (Exception e)
            {
                Server.Instance.Logger.Log(LogLevel.Error, "Could not save client data for " + this.ID);
                Server.Instance.Logger.Exception(LogLevel.Error, e);
            }
        }

        internal void Disconnect() => this.Session.Disconnect();
    }
}
