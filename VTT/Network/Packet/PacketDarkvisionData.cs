namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketDarkvisionData : PacketBase
    {
        public Guid MapID { get; set; }
        public Guid PlayerID { get; set; }
        public Guid ObjectID { get; set; }
        public float Value { get; set; }

        public bool Deletion { get; set; }
        public override uint PacketID => 30;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Debug, "Got Darkvision Data packet");
            bool allowed = !isServer;
            if (isServer)
            {
                allowed = this.Sender.IsAdmin;
            }

            if (!allowed)
            {
                l.Log(LogLevel.Warn, "Client asked for darkvision change without permisions.");
                return;
            }

            Map m = !isServer ? client.CurrentMapIfMatches(this.MapID) : null;
            if (isServer)
            {
                server.TryGetMap(this.MapID, out m);
            }

            if (m != null)
            {
                lock (m.Lock)
                {
                    if (!this.Deletion)
                    {
                        m.DarkvisionData[this.PlayerID] = (this.ObjectID, this.Value);
                    }
                    else
                    {
                        m.DarkvisionData.Remove(this.PlayerID);
                    }
                }

                if (isServer)
                {
                    m.NeedsSave = true;
                    this.Broadcast(c => c.ClientMapID.Equals(m.ID));
                }
            }
            else
            {
                l.Log(LogLevel.Warn, "Got darkvision data packet for non-existing map, discarding.");
                return;
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.MapID = new Guid(br.ReadBytes(16));
            this.PlayerID = new Guid(br.ReadBytes(16));
            this.Deletion = br.ReadBoolean();
            if (!this.Deletion)
            {
                this.ObjectID = new Guid(br.ReadBytes(16));
                this.Value = br.ReadSingle();
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.MapID.ToByteArray());
            bw.Write(this.PlayerID.ToByteArray());
            bw.Write(this.Deletion);
            if (!this.Deletion)
            {
                bw.Write(this.ObjectID.ToByteArray());
                bw.Write(this.Value);
            }
        }
    }
}
