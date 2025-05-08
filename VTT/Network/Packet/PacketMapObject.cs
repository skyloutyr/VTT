namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketMapObject : PacketBaseWithCodec
    {
        public override uint PacketID => 43;
        public override bool Compressed => true;

        public MapObject Obj { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (!isServer) // Server notifying client of new objects
            {
                client.DoTask(() =>
                {
                    Map m = client.CurrentMap;
                    if (m == null)
                    {
                        client.Logger.Log(LogLevel.Error, "Got map object packet but the client is not on a map!");
                        client.NetworkStateCorrupted = true;
                        return;
                    }

                    if (m.ID == this.Obj.MapID) // Async id check in case of mistimed arrival
                    {
                        Guid oID = this.Obj.ID;
                        if (m.GetObject(oID, out MapObject mo))
                        {
                            DataElement de = this.Obj.Serialize();

                            // Have to expliciclty clear and reupload all particles here
                            mo.Particles.ClearContainers();
                            mo.Deserialize(de);
                            mo.Particles.UploadAllConainers();
                        }
                        else
                        {
                            m.AddObject(this.Obj);
                        }
                    }
                });

                client.Logger.Log(LogLevel.Debug, "Got object data for " + this.Obj.ID);
            }
            else // Client wants to add a new object
            {
                ServerClient sc = this.Sender;
                if (!sc.IsAdmin)
                {
                    server.Logger.Log(LogLevel.Warn, "Client " + sc.ID + " attempted to add a new object while not being an administrator!");
                }
                else
                {
                    Guid mapID = this.Obj.MapID;
                    if (server.TryGetMap(mapID, out Map m))
                    {
                        if (this.Obj.ID.Equals(Guid.Empty))
                        {
                            this.Obj.ID = Guid.NewGuid();
                        }

                        m.AddObject(this.Obj);
                        new PacketMapObject() { Obj = this.Obj }.Broadcast(c => c.ClientMapID.Equals(mapID));
                    }
                    else
                    {
                        server.Logger.Log(LogLevel.Error, "Client " + sc.ID + " attempted to add a new object to a non-existing map!");
                    }
                }
            }
        }

        public override void LookupData(Codec c) => c.Lookup(this.Obj ??= new MapObject());
    }
}
