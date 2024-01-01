namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using System.Linq;
    using VTT.Control;
    using VTT.Util;

    public class PacketHandshake : PacketBase
    {
        public Guid ClientID { get; set; }
        public ulong ClientVersion { get; set; }
        public byte[] ClientSecret { get; set; }
        public override uint PacketID => 0;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(Util.LogLevel.Debug, "Client handshake initiated for " + this.ClientID);
                ServerClient sc = (ServerClient)server.FindSession(sessionID);

                if (ClientID.Equals(Guid.Empty))
                {
                    server.Logger.Log(Util.LogLevel.Error, "Could not authorise client " + this.ClientID + ", illegal client ID!");
                    new PacketDisconnectReason() { DCR = DisconnectReason.IllegalOperation }.Send(sc);
                    sc.Disconnect();
                    return;
                }

                ClientInfo ci = server.GetOrCreateClientInfo(this.ClientID);
                sc.Info = ci;

                if (!server.ClientsByID.TryAdd(sc.ID, sc))
                {
                    server.Logger.Log(Util.LogLevel.Error, "Could not authorise client " + this.ClientID + ", it is likely that a client with the same ID already exists!");
                    new PacketDisconnectReason() { DCR = DisconnectReason.AlreadyConnected }.Send(sc);
                    sc.Disconnect();
                    return;
                }

                if (Program.GetVersionBytes() != this.ClientVersion)
                {
                    server.Logger.Log(Util.LogLevel.Error, "Could not authorise client " + this.ClientID + ": Client-Server version mismatch!");
                    new PacketDisconnectReason() { DCR = DisconnectReason.ProtocolMismatch }.Send(sc);
                    sc.Disconnect();
                    return;
                }

                if (this.ClientSecret == null)
                {
                    server.Logger.Log(Util.LogLevel.Error, "Could not authorise client " + this.ClientID + ": Client secret not specified!");
                    new PacketDisconnectReason() { DCR = DisconnectReason.ProtocolMismatch }.Send(sc);
                    sc.Disconnect();
                    return;
                }

                if (ci.IsBanned)
                {
                    server.Logger.Log(Util.LogLevel.Error, "Could not authorise client " + this.ClientID + ": Client is banned on this server!");
                    new PacketDisconnectReason() { DCR = DisconnectReason.Banned }.Send(sc);
                    sc.Disconnect();
                    return;
                }

                if (ci.Secret != null)
                {
                    if (ci.Secret.Length != 32 || this.ClientSecret.Length != 32)
                    {
                        server.Logger.Log(Util.LogLevel.Error, "Could not authorise client " + this.ClientID + ": Client secret format incorrect!");
                        new PacketDisconnectReason() { DCR = DisconnectReason.ProtocolMismatch }.Send(sc);
                        sc.Disconnect();
                        return;
                    }

                    for (int i = 0; i < 32; ++i)
                    {
                        if (ci.Secret[i] != this.ClientSecret[i])
                        {
                            server.Logger.Log(Util.LogLevel.Error, "Could not authorise client " + this.ClientID + ": Client secret mismatch!");
                            new PacketDisconnectReason() { DCR = DisconnectReason.ProtocolMismatch }.Send(sc);
                            sc.Disconnect();
                            return;
                        }
                    }
                }
                else // No secret for a client, assume benign first connection and authorise
                {
                    ci.Secret = this.ClientSecret; // Save happens later, may as well just set the data
                }

                server.ClientInfos[ci.ID] = ci;
                PacketClientInfo pci = new PacketClientInfo() { IsAdmin = sc.IsAdmin, IsObserver = sc.IsObserver, Session = sessionID, IsServer = isServer };
                pci.Send(sc);
                new PacketClientData() { InfosToUpdate = server.ClientInfos.Values.ToList() }.Send(sc);
                server.Logger.Log(Util.LogLevel.Debug, "Client handshake completion sent");
                if (!server.TryGetMap(sc.ClientMapID, out Map m))
                {
                    sc.ClientMapID = server.Settings.DefaultMapID;
                    m = server.GetExistingMap(server.Settings.DefaultMapID);
                    sc.SaveClientData();
                }

                PacketMap mp = new PacketMap() { Map = m, Session = sessionID, IsServer = IsServer };
                mp.Send(sc); // Send the client current map information, wait for MapAck packet
                sc.ClientMapID = m.ID;
                sc.SaveClientData();
                server.Logger.Log(Util.LogLevel.Debug, "Client map changed to " + m.ID);

                if (server.ServerChat.Count > 0)
                {
                    int chatIndex = server.ServerChat.Count - 1;
                    int c = 0;
                    while (c < 24 && chatIndex >= 0)
                    {
                        ChatLine cl = server.ServerChat[chatIndex--];
                        if (sc.IsAdmin || cl.SenderID.Equals(sc.ID) || cl.DestID.Equals(sc.ID) || cl.DestID.Equals(Guid.Empty))
                        {
                            PacketChatLine pcl = new PacketChatLine() { Line = cl };
                            pcl.Send(sc);
                            ++c;
                        }
                    }
                }

                new PacketCommunique() { Request = RequestType.ChatMoveToEndRequest }.Send(sc);
                if (sc.IsAdmin)
                {
                    PacketAssetDef pad = new PacketAssetDef() { ActionType = AssetDefActionType.Initialize, Dir = server.AssetManager.Root };
                    pad.Send(sc);
                    server.Logger.Log(Util.LogLevel.Debug, "AssetRef data sent");
                    PacketMapPointer pmp = new PacketMapPointer() { Data = server.EnumerateMapData().Select(x => (x.MapID, x.MapFolder, x.MapName)).ToList(), IsServer = true, Remove = false, Session = sessionID };
                    pmp.Send(sc);
                    new PacketSetDefaultMap() { MapID = server.Settings.DefaultMapID }.Send(sc);
                }

                foreach (TextJournal tj in server.Journals.Values)
                {
                    if (tj.IsPublic || sc.ID.Equals(tj.OwnerID) || sc.IsAdmin)
                    {
                        new PacketFullJournal() { Journal = tj }.Send(sc);
                    }
                }

                new PacketMusicPlayerFullData() { SerializedMusicPlayer = server.MusicPlayer.Serialize() }.Send(sc);
                new PacketMusicPlayerSetIndex() { Index = server.MusicPlayer.CurrentTrackPosition }.Send(sc);

                sc.Info.IsLoggedOn = true;
                new PacketClientOnlineNotification() { ClientID = sc.ID, Status = true }.Broadcast();
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.ClientID = br.ReadGuid();
            this.ClientVersion = br.ReadUInt64();
            if (br.BaseStream.CanRead)
            {
                this.ClientSecret = br.ReadBytes(32);
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.ClientID);
            bw.Write(this.ClientVersion);
            bw.Write(this.ClientSecret);
        }
    }

    public enum AssetDefActionType
    {
        Initialize,
        Add,
        AddDir,
        Remove,
        RemoveDir
    }
}
