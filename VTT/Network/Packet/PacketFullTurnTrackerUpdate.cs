namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketFullTurnTrackerUpdate : PacketBase
    {
        public DataElement Data { get; set; }
        public override uint PacketID => 40;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(LogLevel.Info, "Got full explicit turn tracker synchronization command!");
                this.Data = server.Maps[this.Sender.ClientMapID].TurnTracker.Serialize();
                this.Broadcast(c => c.ClientMapID.Equals(this.Sender.ClientMapID));
            }
            else
            {
                Map m = client.CurrentMap;
                if (m == null)
                {
                    client.Logger.Log(LogLevel.Warn, "Got turn tracker update packet for non-existing map.");
                    return;
                }

                lock (m.TurnTracker.Lock)
                {
                    m.TurnTracker.Deserialize(this.Data);
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            if (!this.IsServer)
            {
                this.Data = new DataElement();
                this.Data.Read(br);
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            if (this.IsServer)
            {
                this.Data.Write(bw);
            }
        }
    }
}
