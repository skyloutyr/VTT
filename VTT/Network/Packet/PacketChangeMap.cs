namespace VTT.Network.Packet
{
    using Newtonsoft.Json;
    using System;
    using System.IO;
    using VTT.Util;

    public class PacketChangeMap : PacketBase
    {
        public Guid NewMapID { get; set; }
        public Guid[] Clients { get; set; }
        public override uint PacketID => 14;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer) // Client asking server to change the map
            {
                ServerClient senderSc = (ServerClient)server.FindSession(sessionID);
                server.Logger.Log(LogLevel.Debug, "Client " + senderSc.ID + " asked for a map change");
                if (!server.TryGetMap(this.NewMapID, out _))
                {
                    server.Logger.Log(LogLevel.Warn, "Client asked to move to a non-existing map!");
                    return;
                }

                if (senderSc.IsAdmin) // Only admins can force a map change on other clients
                {
                    foreach (Guid id in this.Clients)
                    {
                        if (server.ClientsByID.TryGetValue(id, out ServerClient sc))
                        {
                            server.Logger.Log(LogLevel.Debug, "Map change for " + sc.ID + " authorised");
                            sc.ClientMapID = this.NewMapID;
                            sc.SaveClientData();
                            PacketChangeMap pcm = new PacketChangeMap() { Clients = new Guid[0], IsServer = true, NewMapID = this.NewMapID, Session = sessionID };
                            pcm.Send(sc);
                            new PacketClientData() { InfosToUpdate = new System.Collections.Generic.List<ClientInfo>() { sc.Info } }.Broadcast();
                        }
                        else
                        {
                            if (server.ClientInfos.TryGetValue(id, out ClientInfo ci))
                            {
                                ci.MapID = this.NewMapID;
                                string clientLoc = Path.Combine(IOVTT.ServerDir, "Clients", ci.ID.ToString() + ".json");
                                Directory.CreateDirectory(Path.Combine(IOVTT.ServerDir, "Clients"));
                                try
                                {
                                    File.WriteAllText(clientLoc, JsonConvert.SerializeObject(ci));
                                    Server.Instance.Logger.Log(LogLevel.Debug, "Client data for " + ci.ID + " saved");
                                }
                                catch (Exception e)
                                {
                                    Server.Instance.Logger.Log(LogLevel.Error, "Could not save client data for " + ci.ID);
                                    Server.Instance.Logger.Exception(LogLevel.Error, e);
                                }

                                new PacketClientData() { InfosToUpdate = new System.Collections.Generic.List<ClientInfo>() { ci } }.Broadcast();
                            }
                        }
                    }
                }
                else // Clients are allowed to change their own map on a request
                {
                    server.Logger.Log(LogLevel.Debug, "Map change committed");
                    senderSc.ClientMapID = this.NewMapID;
                    senderSc.SaveClientData();
                    PacketChangeMap pcm = new PacketChangeMap() { Clients = new Guid[0], IsServer = true, NewMapID = this.NewMapID, Session = sessionID };
                    pcm.Send(senderSc);
                    new PacketClientData() { InfosToUpdate = new System.Collections.Generic.List<ClientInfo>() { senderSc.Info } }.Broadcast();
                }
            }
            else // Server asking the client to change map
            {
                client.Logger.Log(LogLevel.Debug, "Server asking to change map, requesting new map data");
                client.DoTask(() =>
                {
                    client.SetCurrentMap(null, () =>
                    {
                        PacketCommunique pc = new PacketCommunique() { IsServer = false, Request = RequestType.ClientMapAck, RequestData = 1, Session = sessionID }; // request data 1 resends the map data to the client
                        pc.Send(client.NetClient);
                    });
                });
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.NewMapID = new Guid(br.ReadBytes(16));
            this.Clients = new Guid[br.ReadInt32()];
            for (int i = 0; i < this.Clients.Length; ++i)
            {
                this.Clients[i] = new Guid(br.ReadBytes(16));
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.NewMapID.ToByteArray());
            bw.Write(this.Clients.Length);
            foreach (Guid id in this.Clients)
            {
                bw.Write(id.ToByteArray());
            }
        }
    }
}
