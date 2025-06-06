﻿namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketMoveTurnToIndex : PacketBaseWithCodec
    {
        public override uint PacketID => 47;

        public int Index { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Debug, "Got turn move request");
            Map m = isServer ? server.GetExistingMap(this.Sender.ClientMapID) : client.CurrentMap;
            if (isServer && !this.Sender.IsAdmin)
            {
                l.Log(LogLevel.Warn, "Client asked for turn tracker modification without permissions!");
                return;
            }

            lock (m.TurnTracker.Lock)
            {
                m.TurnTracker.MoveTo(this.Index);
            }

            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast(c => c.ClientMapID.Equals(m.ID));
            }
            else
            {
                m.TurnTracker.Pulse();
            }
        }

        public override void LookupData(Codec c) => this.Index = c.Lookup(this.Index);
    }
}
