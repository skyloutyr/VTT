namespace VTT.Network.Packet
{
    using OpenTK.Mathematics;
    using System;
    using System.IO;
    using VTT.Control;

    public class PacketEnableDisableFow : PacketBase
    {
        public bool Status { get; set; }
        public Vector2 Size { get; set; }
        public override uint PacketID => 36;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer) // Client ask server
            {
                server.Logger.Log(Util.LogLevel.Debug, "Got client FOW change request");
                if (this.Sender.IsAdmin)
                {
                    Map m = server.GetExistingMap(this.Sender.ClientMapID);
                    if (this.Status)
                    {
                        if (this.Size.X < 32 || this.Size.Y < 32 || this.Size.X > 4096 || this.Size.Y > 4096)
                        {
                            server.Logger.Log(Util.LogLevel.Error, "Invalid FOW map size specified!");
                            PacketFOWData pfowd = new PacketFOWData() { MapID = m.ID, Status = m.FOW != null && !m.FOW.IsDeleted, Image = m.FOW?.Canvas };
                            pfowd.Send(this.Sender);
                        }
                        else
                        {
                            m.FOW = new FOWCanvas((int)this.Size.X, (int)this.Size.Y);
                            m.FOW.NeedsSave = true;
                            PacketFOWData pfowd = new PacketFOWData() { MapID = m.ID, Status = true, Image = m.FOW.Canvas };
                            pfowd.Broadcast(sc => sc.ClientMapID.Equals(m.ID));
                        }
                    }
                    else
                    {
                        if (m.FOW != null)
                        {
                            m.FOW.IsDeleted = true;
                            m.FOW.NeedsSave = false;
                            PacketFOWData pfowd = new PacketFOWData() { MapID = m.ID, Status = false };
                            pfowd.Broadcast(sc => sc.ClientMapID.Equals(m.ID));
                        }
                    }
                }
                else
                {
                    server.Logger.Log(Util.LogLevel.Warn, "Client asked for FOW changes without permissions!");
                    Map m = server.GetExistingMap(this.Sender.ClientMapID);
                    PacketFOWData pfowd = new PacketFOWData() { MapID = m.ID, Status = m.FOW != null && !m.FOW.IsDeleted, Image = m.FOW?.Canvas };
                    pfowd.Send(this.Sender);
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.Status = br.ReadBoolean();
            this.Size = new Vector2(br.ReadSingle(), br.ReadSingle());
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.Status);
            bw.Write(this.Size.X);
            bw.Write(this.Size.Y);
        }
    }
}
