namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Generic;
    using VTT.Control;
    using VTT.Util;

    internal class PacketDeleteMapObject : PacketBaseWithCodec
    {
        public override uint PacketID => 33;

        public List<(Guid, Guid)> DeletedObjects { get; set; } = new List<(Guid, Guid)>();

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
                new PacketDeleteMapObject() { DeletedObjects = broadcastChanges }.Broadcast();
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

        public override void LookupData(Codec c)
        {
            this.DeletedObjects = c.Lookup(this.DeletedObjects, x =>
            {
                Guid i1 = c.Lookup(x.Item1);
                Guid i2 = c.Lookup(x.Item2);
                return x = (i1, i2);
            });
        }
    }
}
