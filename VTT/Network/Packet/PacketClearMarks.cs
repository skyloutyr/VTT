namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;

    public class PacketClearMarks : PacketBase
    {
        public override uint PacketID => 23;
        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                if (this.Sender.IsAdmin)
                {
                    Map m = server.Maps[this.Sender.ClientMapID];
                    if (m != null)
                    {
                        m.PermanentMarks.Clear();
                        m.NeedsSave = true;
                    }

                    this.Broadcast(c => c.ClientMapID.Equals(this.Sender.ClientMapID));
                }
            }
            else
            {
                for (int i = client.Frontend.Renderer.RulerRenderer.ActiveInfos.Count - 1; i >= 0; i--)
                {
                    RulerInfo ri = client.Frontend.Renderer.RulerRenderer.ActiveInfos[i];
                    if (ri.KeepAlive)
                    {
                        ri.KeepAlive = false;
                        ri.IsDead = true;
                    }
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            // NOOP
        }
        public override void Encode(BinaryWriter bw)
        {
            // NOOP
        }
    }
}
