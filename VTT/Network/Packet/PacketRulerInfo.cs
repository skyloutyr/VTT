namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketRulerInfo : PacketBaseWithCodec
    {
        public override uint PacketID => 54;

        public RulerInfo Info { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                this.Broadcast(c => c.ClientMapID.Equals(this.Sender.ClientMapID)); // Just pass to all clients on the same map
                Map m = server.GetExistingMap(this.Sender.ClientMapID);
                int pmIndex = m.PermanentMarks.FindIndex(p => p.SelfID.Equals(this.Info.SelfID));
                if (this.Info.KeepAlive)
                {
                    if (!this.Info.IsDead && pmIndex == -1)
                    {
                        m.PermanentMarks.Add(this.Info);
                        m.NeedsSave = true;
                    }

                    if (this.Info.IsDead && pmIndex != -1)
                    {
                        m.PermanentMarks.RemoveAt(pmIndex);
                        m.NeedsSave = true;
                    }
                }
                else
                {
                    if (pmIndex != -1)
                    {
                        m.PermanentMarks.RemoveAt(pmIndex);
                        m.NeedsSave = true;
                    }
                }
            }
            else
            {
                client.Frontend.Renderer.RulerRenderer.InfosToActUpon.Enqueue(this.Info);
            }
        }

        public override void LookupData(Codec c) => c.Lookup((ICustomNetworkHandler)(this.Info ??= new RulerInfo()));
    }
}
