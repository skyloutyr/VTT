﻿namespace VTT.Network.Packet
{
    using System.Numerics;
    using System;
    using VTT.Control;

    public class PacketEnableDisableFow : PacketBaseWithCodec
    {
        public override uint PacketID => 36;

        public bool Status { get; set; }
        public Vector2 Size { get; set; }

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

        public override void LookupData(Codec c)
        {
            this.Status = c.Lookup(this.Status);
            this.Size = c.Lookup(this.Size);
        }
    }
}
