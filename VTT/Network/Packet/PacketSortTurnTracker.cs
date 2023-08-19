namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;

    public class PacketSortTurnTracker : PacketBase
    {
        public override uint PacketID => 56;
        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(Util.LogLevel.Debug, "Got turn tracker sort request");
                if (this.Sender.IsAdmin)
                {
                    Map m = server.GetExistingMap(this.Sender.ClientMapID);
                    lock (m.TurnTracker.Lock)
                    {
                        m.TurnTracker.Sort();
                    }

                    m.NeedsSave = true;
                    new PacketFullTurnTrackerUpdate() { Data = m.TurnTracker.Serialize() }.Broadcast(c => c.ClientMapID.Equals(m.ID));
                }
                else
                {
                    server.Logger.Log(Util.LogLevel.Warn, "Client asked for turn tracker sorting without permissions!");
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
        }

        public override void Encode(BinaryWriter bw)
        {
        }
    }
}
