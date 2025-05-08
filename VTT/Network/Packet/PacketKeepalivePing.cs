namespace VTT.Network.Packet
{
    using System;

    public class PacketKeepalivePing : PacketBaseWithCodec
    {
        public override uint PacketID => 41;

        public bool Side { get; set; }

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

        public override void LookupData(Codec c) => this.Side = c.Lookup(this.Side);
    }
}
