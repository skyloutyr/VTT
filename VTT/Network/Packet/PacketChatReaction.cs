namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;

    public class PacketChatReaction : PacketBaseWithCodec
    {
        public override uint PacketID => 79;

        public Guid Reacter { get; set; }
        public int CLIndex { get; set; }
        public int EmojiIndex { get; set; }
        public bool IsAddition { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            ChatLine cl = isServer ? server.ServerChat[this.CLIndex] : client.Chat.Find(x => x.Index == this.CLIndex);
            if (cl != null)
            {
                if (this.IsAddition)
                {
                    cl.Reactions.AddReaction(this.Reacter, this.EmojiIndex);
                }
                else
                {
                    cl.Reactions.RemoveReaction(this.Reacter, this.EmojiIndex);
                }
            }

            if (isServer)
            {
                this.Broadcast();
                if (cl != null)
                {
                    server.ServerChatExtras.NotifyOfLineDataChange(cl);
                }
            }
        }

        public override void LookupData(Codec c)
        {
            this.Reacter = c.Lookup(this.Reacter);
            this.CLIndex = c.Lookup(this.CLIndex);
            this.EmojiIndex = c.Lookup(this.EmojiIndex);
            this.IsAddition = c.Lookup(this.IsAddition);
        }
    }
}
