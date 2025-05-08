namespace VTT.Network.Packet
{
    using System.Numerics;
    using System;
    using System.Collections.Generic;
    using VTT.Control;

    public class PacketFOWRequest : PacketBaseWithCodec
    {
        public override uint PacketID => 38;

        public bool RequestType { get; set; }
        public List<Vector2> Polygon { get; set; } = new List<Vector2>();

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                Map m = server.GetExistingMap(this.Sender.ClientMapID);
                server.Logger.Log(Util.LogLevel.Debug, "Got client FOW change message");
                if (this.Sender.IsAdmin)
                {
                    if (m.FOW != null)
                    {
                        lock (m.FOW.Lock)
                        {
                            if (m.FOW.ProcessPolygon(this.Polygon.ToArray(), this.RequestType))
                            {
                                PacketFOWData pfowd = new PacketFOWData() { Image = m.FOW?.Canvas, MapID = m.ID, Status = m.FOW != null && !m.FOW.IsDeleted };
                                pfowd.Broadcast(c => c.ClientMapID.Equals(m.ID));
                            }
                        }
                    }
                    else
                    {
                        server.Logger.Log(Util.LogLevel.Warn, "Client asked for FOW change of a map without FOW enabled!");
                        PacketFOWData pfowd = new PacketFOWData() { Image = m.FOW?.Canvas, MapID = m.ID, Status = m.FOW != null && !m.FOW.IsDeleted };
                        pfowd.Send(this.Sender);
                    }
                }
                else
                {
                    server.Logger.Log(Util.LogLevel.Warn, "Client asked for FOW change without permissions");
                    PacketFOWData pfowd = new PacketFOWData() { Image = m.FOW?.Canvas, MapID = m.ID, Status = m.FOW != null && !m.FOW.IsDeleted };
                    pfowd.Send(this.Sender);
                }
            }
        }

        public override void LookupData(Codec c)
        {
            this.RequestType = c.Lookup(this.RequestType);
            this.Polygon = c.Lookup(this.Polygon, c.Lookup);
        }
    }
}
