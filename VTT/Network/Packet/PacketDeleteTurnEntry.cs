namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketDeleteTurnEntry : PacketBase
    {
        public int EntryIndex { get; set; }
        public override uint PacketID => 34;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.GetContextLogger();
            l.Log(LogLevel.Debug, "Got turn tracker entry deletion request");
            bool allowed = !isServer || this.Sender.IsAdmin;
            if (allowed)
            {
                Map m = isServer ? server.Maps[this.Sender.ClientMapID] : client.CurrentMap;
                if (m == null)
                {
                    l.Log(LogLevel.Warn, "Turn tracker packet got null map!");
                    return;
                }

                lock (m.TurnTracker.Lock)
                {
                    m.TurnTracker.Remove(this.EntryIndex);
                }

                if (isServer)
                {
                    m.NeedsSave = true;
                    this.Broadcast(p => p.ClientMapID.Equals(m.ID));
                }
            }
            else
            {
                l.Log(LogLevel.Warn, "Client asked to delete turn tracker entry without permissions!");
            }
        }

        public override void Decode(BinaryReader br) => this.EntryIndex = br.ReadInt32();

        public override void Encode(BinaryWriter bw) => bw.Write(this.EntryIndex);
    }
}
