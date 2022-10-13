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
        public override uint PacketID => 41;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                if (!this.Side)
                {
                    this.Send(this.Sender);
                }
                else
                {
                    ServerClient sc = this.Sender;
                    if (sc != null)
                    {
                        sc.LastPingResponseTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    }
                }
            }
            else
            {
                if (this.Side)
                {
                    this.Send();
                }
                else
                {
                    NetClient nc = client.NetClient;
                    if (nc != null)
                    {
                        nc.LastPingResponseTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    }
                }
            }
        }

        public override void Decode(BinaryReader br) => this.Side = br.ReadBoolean();
        public override void Encode(BinaryWriter bw) => bw.Write(this.Side);
    }
}
