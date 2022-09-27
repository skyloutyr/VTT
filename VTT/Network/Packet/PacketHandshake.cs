namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using VTT.Control;

    public class PacketHandshake : PacketBase
    {
        public Guid ClientID { get; set; }
        public ulong ClientVersion { get; set; }
        public override uint PacketID => 0;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(VTT.Util.LogLevel.Debug, "Client handshake initiated for " + this.ClientID);
                ServerClient sc = (ServerClient)server.FindSession(sessionID);
                ClientInfo ci = server.GetOrCreateClientInfo(this.ClientID);
                sc.Info = ci;

                if (!server.ClientsByID.TryAdd(sc.ID, sc))
                {
                    server.Logger.Log(VTT.Util.LogLevel.Error, "Could not authorise client " + this.ClientID + ", it is likely that a client with the same ID already exists!");
                    new PacketDisconnectReason() { DCR = DisconnectReason.AlreadyConnected }.Send(sc);
                    sc.Disconnect();
                    return;
                }

                if (Program.GetVersionBytes() != this.ClientVersion)
                {
                    server.Logger.Log(VTT.Util.LogLevel.Error, "Could not authorise client " + this.ClientID + ": Client-Server version mismatch!");
                    new PacketDisconnectReason() { DCR = DisconnectReason.ProtocolMismatch }.Send(sc);
                    sc.Disconnect();
                    return;
                }

                if (ci.IsBanned)
                {
                    server.Logger.Log(VTT.Util.LogLevel.Error, "Could not authorise client " + this.ClientID + ": Client is banned on this server!");
                    new PacketDisconnectReason() { DCR = DisconnectReason.Banned }.Send(sc);
                    sc.Disconnect();
                    return;
                }

                server.ClientInfos[ci.ID] = ci;
                PacketClientInfo pci = new PacketClientInfo() { IsAdmin = sc.IsAdmin, IsObserver = sc.IsObserver, Session = sessionID, IsServer = isServer };
                pci.Send(sc);
                new PacketClientData() { InfosToUpdate = server.ClientInfos.Values.ToList() }.Send(sc);
                server.Logger.Log(VTT.Util.LogLevel.Debug, "Client handshake completion sent");
                if (!server.Maps.ContainsKey(sc.ClientMapID))
                {
                    sc.ClientMapID = server.Settings.DefaultMapID;
                    sc.SaveClientData();
                }

                Map m = server.Maps[sc.ClientMapID];
                PacketMap mp = new PacketMap() { Map = m, Session = sessionID, IsServer = IsServer };
                mp.Send(sc); // Send the client current map information, wait for MapAck packet
                sc.ClientMapID = m.ID;
                sc.SaveClientData();
                server.Logger.Log(VTT.Util.LogLevel.Debug, "Client map changed to " + m.ID);

                if (server.ServerChat.Count > 0)
                {
                    int chatIndex = server.ServerChat.Count - 1;
                    int c = 0;
                    while (c < 24 && chatIndex >= 0)
                    {
                        ++c;
                        ChatLine cl = server.ServerChat[chatIndex--];
                        if (sc.IsAdmin || cl.SenderID.Equals(sc.ID) || cl.DestID.Equals(sc.ID) || cl.DestID.Equals(Guid.Empty))
                        {
                            PacketChatLine pcl = new PacketChatLine() { Line = cl };
                            pcl.Send(sc);
                        }
                    }
                }

                new PacketCommunique() { Request = RequestType.ChatMoveToEndRequest }.Send(sc);
                if (sc.IsAdmin)
                {
                    PacketAssetDef pad = new PacketAssetDef() { ActionType = AssetDefActionType.Initialize, Dir = server.AssetManager.Root };
                    pad.Send(sc);
                    server.Logger.Log(VTT.Util.LogLevel.Debug, "AssetRef data sent");
                    PacketMapPointer pmp = new PacketMapPointer() { Data = server.Maps.Select(kv => (kv.Key, kv.Value.Folder, kv.Value.Name)).ToList(), IsServer = true, Remove = false, Session = sessionID };
                    pmp.Send(sc);
                }

                foreach (TextJournal tj in server.Journals.Values)
                {
                    if (tj.IsPublic || sc.ID.Equals(tj.OwnerID) || sc.IsAdmin)
                    {
                        new PacketFullJournal() { Journal = tj }.Send(sc);
                    }
                }

                sc.Info.IsLoggedOn = true;
                new PacketClientOnlineNotification() { ClientID = sc.ID, Status = true }.Broadcast();
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.ClientID = new Guid(br.ReadBytes(16));
            this.ClientVersion = br.ReadUInt64();
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.ClientID.ToByteArray());
            bw.Write(this.ClientVersion);
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
