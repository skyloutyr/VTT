namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketChangeTurnTrackerRound : PacketBaseWithCodec
    {
        public override uint PacketID => 91;

        public Guid MapID { get; set; }
        public int NewRound { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Debug, $"Got turn tracker round change request (to {this.NewRound})");
            Map m = isServer ? server.GetExistingMap(this.MapID) : client.CurrentMapIfMatches(this.MapID);
            if (m == null)
            {
                l.Log(LogLevel.Warn, "Turn tracker round request map was null!");
                return;
            }

            if (isServer)
            {
                if (!this.Sender.IsAdmin)
                {
                    l.Log(LogLevel.Warn, "Client tried to edit a turn tracker round without permissions!");
                    return;
                }
            }

            m.TurnTracker.ChangeRound(this.NewRound);
            if (isServer)
            {
                m.NeedsSave = true;
                // No broadcast call here since TurnTracker.ChangeRound already broadcasts on server (inconsistent, but needed for automation)
            }
        }

        public override void LookupData(Codec c)
        {
            this.MapID = c.Lookup(this.MapID);
            this.NewRound = c.Lookup(this.NewRound);
        }
    }
}
