namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;

    public class PacketRulerInfo : PacketBase
    {
        public RulerInfo Info { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                this.Broadcast(c => c.ClientMapID.Equals(this.Sender.ClientMapID)); // Just pass to all clients on the same map
                Map m = server.Maps[this.Sender.ClientMapID];
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

        public override void Decode(BinaryReader br)
        {
            RulerInfo i = new RulerInfo();
            i.SelfID = new Guid(br.ReadBytes(16));
            i.Read(br);
            this.Info = i;
        }

        public override void Encode(BinaryWriter bw) => this.Info.Write(bw);
    }
}
