namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketPing : PacketBase
    {
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

        public override void Decode(BinaryReader br)
        {
            this.Ping = new Ping();
            DataElement data = new DataElement();
            data.Read(br);
            this.Ping.Deserialize(data);
        }

        public override void Encode(BinaryWriter bw) => this.Ping.Serialize().Write(bw);
    }
}
