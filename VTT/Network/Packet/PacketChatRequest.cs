namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;

    public class PacketChatRequest : PacketBase
    {
        public int Index { get; set; }
        public override uint PacketID => 22;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(VTT.Util.LogLevel.Debug, "Client asked for more chat");
                if (this.Index > 0)
                {
                    int chatIndex = this.Index;
                    int c = 0;
                    while (c < 24 && chatIndex > 0)
                    {
                        ++c;
                        ChatLine cl = server.ServerChat[--chatIndex];
                        if (this.Sender.IsAdmin || this.Sender.ID.Equals(cl.SenderID) || this.Sender.ID.Equals(cl.DestID) || cl.DestID.Equals(Guid.Empty))
                        {
                            PacketChatLine pcl = new PacketChatLine() { Line = cl };
                            pcl.Send(this.Sender);
                        }
                    }
                }
            }
        }

        public override void Decode(BinaryReader br) => this.Index = br.ReadInt32();
        public override void Encode(BinaryWriter bw) => bw.Write(this.Index);
    }
}
