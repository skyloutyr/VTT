namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;

    public class PacketPing : PacketBaseWithCodec
    {
        public override uint PacketID => 52;

        public Ping Ping { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                this.Broadcast(c => c.ClientMapID.Equals(this.Sender.ClientMapID));
            }
            else
            {
                client.Frontend.Renderer.PingRenderer.AddPing(this.Ping);
            }
        }

        public override void LookupData(Codec c) => c.Lookup(this.Ping ??= new Ping());
    }
}
