namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;

    public class PacketChatLine : PacketBase
    {
        public ChatLine Line { get; set; }
        public override uint PacketID => 20;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (!isServer)
            {
                client.Logger.Log(VTT.Util.LogLevel.Debug, "Got chat line from server");
                client.AddChatLine(this.Line);
            }
        }

        public override void Decode(BinaryReader br)
        {
            int l = br.ReadInt32();
            this.Line = new ChatLine() { Index = l };
            this.Line.Read(br);
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.Line.Index);
            this.Line.Write(bw);
        }
    }
}
