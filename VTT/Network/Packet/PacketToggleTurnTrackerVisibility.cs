namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketToggleTurnTrackerVisibility : PacketBaseWithCodec
    {
        public override uint PacketID => 58;

        public bool Action { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = isServer ? server.Logger : client.Logger;
            Map m = isServer ? server.GetExistingMap(this.Sender.ClientMapID) : client.CurrentMap;
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

        public override void LookupData(Codec c) => this.Action = c.Lookup(this.Action);
    }
}
