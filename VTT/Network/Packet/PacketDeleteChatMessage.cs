namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;

    public class PacketDeleteChatMessage : PacketBaseWithCodec
    {
        public override uint PacketID => 90;
        public int CLIndex { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            ChatLine cl = isServer ? server.ServerChat.AllChatLines[this.CLIndex] : client.Chat.Find(x => x.Index == this.CLIndex);
            if (isServer)
            {
                if (!this.Sender.IsAdmin && !Guid.Equals(this.Sender.ID, cl.SenderID))
                {
                    this.ContextLogger.Log(Util.LogLevel.Warn, $"Client {this.Sender.ID} asked to delete a chat line without permissions!");
                    return;
                }

                lock (server.chatLock)
                {
                    cl.Flags |= ChatLine.ChatLineFlags.Deleted;
                    server.ServerChat.NotifyOfChange(cl);
                }

                this.Broadcast();
            }
            else
            {
                cl.Flags |= ChatLine.ChatLineFlags.Deleted; // For now it doesn't actually go away from memory
            }
        }

        public override void LookupData(Codec c) => this.CLIndex = c.Lookup(this.CLIndex);
    }
}
