namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    internal class PacketDeleteMapObject : PacketBase
    {
        public List<(Guid, Guid)> DeletedObjects { get; set; } = new List<(Guid, Guid)>();
        public Guid SenderID { get; set; }
        public override uint PacketID => 33;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(LogLevel.Info, "Got object deletion request");
                List<(Guid, Guid)> broadcastChanges = new List<(Guid, Guid)>();
                foreach ((Guid, Guid) d in this.DeletedObjects)
                {
                    if (server.TryGetMap(d.Item1, out Map m))
                    {
                        if (m.GetObject(d.Item2, out MapObject mo))
                        {
                            if (this.Sender.IsAdmin || mo.CanEdit(this.Sender.ID))
                            {
                                broadcastChanges.Add((d.Item1, d.Item2));
                                m.RemoveObject(mo);
                            }
                            else
                            {
                                server.Logger.Log(LogLevel.Error, "Client requested a deletion for object without sufficient permissions to do so!");
                                continue;
                            }
                        }
                        else
                        {
                            server.Logger.Log(LogLevel.Error, "Client asked deletion for a non-existing object!");
                            continue;
                        }
                    }
                    else
                    {
                        server.Logger.Log(LogLevel.Error, "Client asked object deletion for a non-existing map!");
                        continue;
                    }
                }

                server.Logger.Log(LogLevel.Info, "Notifying clients of " + broadcastChanges.Count + " deletions");
                new PacketDeleteMapObject() { DeletedObjects = broadcastChanges, SenderID = this.SenderID }.Broadcast();
            }
            else
            {
                client.Logger.Log(LogLevel.Info, "Got object deletion notification");
                foreach ((Guid, Guid) d in this.DeletedObjects)
                {
                    Map m = client.CurrentMap;
                    if (m != null && d.Item1.Equals(m.ID))
                    {
                        if (m.GetObject(d.Item2, out MapObject mo))
                        {
                            m.RemoveObject(mo);
                        }
                        else
                        {
                            client.Logger.Log(LogLevel.Warn, "Got object deletion for non-existing object, discarding");
                        }
                    }
                    else
                    {
                        client.Logger.Log(LogLevel.Info, "Got object deletion change for other map, discarding");
                    }
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.SenderID = new Guid(br.ReadBytes(16));
            int amt = br.ReadInt32();
            while (amt-- > 0)
            {
                Guid mID = new Guid(br.ReadBytes(16));
                Guid oID = new Guid(br.ReadBytes(16));
                this.DeletedObjects.Add((mID, oID));
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.SenderID.ToByteArray());
            bw.Write(this.DeletedObjects.Count);
            foreach ((Guid, Guid) id in this.DeletedObjects)
            {
                bw.Write(id.Item1.ToByteArray());
                bw.Write(id.Item2.ToByteArray());
            }
        }
    }
}
