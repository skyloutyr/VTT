namespace VTT.Network.Packet
{
    using VTT.Util;
    using System;
    using System.IO;
    using VTT.Control;

    public class PacketToggleTurnTrackerVisibility : PacketBase
    {
        public bool Action { get; set; }
        public override uint PacketID => 58;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = isServer ? server.Logger : client.Logger;
            Map m = isServer ? server.Maps[this.Sender.ClientMapID] : client.CurrentMap;
            if (isServer && !this.Sender.IsAdmin)
            {
                l.Log(LogLevel.Warn, "A client asked to toggle turn order visibility without permissions!");
                return;
            }

            m.TurnTracker.Visible = this.Action;
            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast(p => p.ClientMapID.Equals(m.ID));
            }
        }

        public override void Decode(BinaryReader br) => this.Action = br.ReadBoolean();
        public override void Encode(BinaryWriter bw) => bw.Write(this.Action);
    }
}
