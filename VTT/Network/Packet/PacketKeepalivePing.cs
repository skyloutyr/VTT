namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class PacketKeepalivePing : PacketBase
    {
        public bool Side { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                if (!this.Side)
                {
                    this.Send(this.Sender);
                }
            }
            else
            {
                if (this.Side)
                {
                    this.Send();
                }
            }
        }

        public override void Decode(BinaryReader br) => this.Side = br.ReadBoolean();
        public override void Encode(BinaryWriter bw) => bw.Write(this.Side);
    }
}
