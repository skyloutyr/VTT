namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;

    public class PacketChatRequest : PacketBaseWithCodec
    {
        public override uint PacketID => 22;
        public int Index { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(Util.LogLevel.Debug, "Client asked for more chat");
                if (this.Index > 0)
                {
                    int chatIndex = this.Index;
                    int c = 0;
                    while (c < 24 && chatIndex > 0)
                    {
                        ++c;
                        ChatLine cl = server.ServerChat.AllChatLines[--chatIndex]; // This iterates from the end, no need for a lock
                        if (this.Sender.IsAdmin || !cl.Flags.HasFlag(ChatLine.ChatLineFlags.Deleted) || this.Sender.ID.Equals(cl.SenderID) || this.Sender.ID.Equals(cl.DestID) || cl.DestID.Equals(Guid.Empty))
                        {
                            PacketChatLine pcl = new PacketChatLine() { Line = cl };
                            pcl.Send(this.Sender);
                        }
                    }
                }
            }
        }

        public override void LookupData(Codec c) => this.Index = c.Lookup(this.Index);
    }
}
