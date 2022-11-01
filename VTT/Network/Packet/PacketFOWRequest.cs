namespace VTT.Network.Packet
{
    using OpenTK.Mathematics;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using VTT.Control;

    public class PacketFOWRequest : PacketBase
    {
        public bool RequestType { get; set; }
        public List<Vector2> Polygon { get; set; } = new List<Vector2>();
        public override uint PacketID => 38;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                Map m = server.Maps[this.Sender.ClientMapID];
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

        public override void Decode(BinaryReader br)
        {
            this.RequestType = br.ReadBoolean();
            int amt = br.ReadInt32();
            while (amt-- > 0)
            {
                this.Polygon.Add(new Vector2(br.ReadSingle(), br.ReadSingle()));
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.RequestType);
            bw.Write(this.Polygon.Count);
            foreach (Vector2 v in this.Polygon)
            {
                bw.Write(v.X);
                bw.Write(v.Y);
            }
        }
    }
}
