namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketMoveTurnTrackerEntry : PacketBaseWithCodec
    {
        public override uint PacketID => 48;

        public int IndexFrom { get; set; }
        public int IndexTo { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Debug, "Got turn tracker move request");
            Map m = isServer ? server.GetExistingMap(this.Sender.ClientMapID) : client.CurrentMap;
            if (isServer && !this.Sender.IsAdmin)
            {
                l.Log(LogLevel.Warn, "Client asked for turn tracker modification without permissions!");
                return;
            }

            if (this.IndexFrom == this.IndexTo)
            {
                return;
            }

            if (this.IndexTo < 0)
            {
                l.Log(LogLevel.Warn, "Invalid turn tracker moveTo index!");
            }

            lock (m.TurnTracker.Lock)
            {
                TurnTracker.Entry e = m.TurnTracker.GetAt(this.IndexFrom);
                m.TurnTracker.Entries.Remove(e);
                m.TurnTracker.Add(e, this.IndexTo - (this.IndexTo >= this.IndexFrom ? 1 : 0));
            }

            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast(c => c.ClientMapID.Equals(m.ID));
            }
        }

        public override void LookupData(Codec c)
        {
            this.IndexFrom = c.Lookup(this.IndexFrom);
            this.IndexTo = c.Lookup(this.IndexTo);
        }
    }
}
