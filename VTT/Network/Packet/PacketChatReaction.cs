namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketChatReaction : PacketBase
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

        public override void Decode(BinaryReader br)
        {
            this.Reacter = br.ReadGuid();
            this.CLIndex = br.ReadInt32();
            this.EmojiIndex = br.ReadInt32();
            this.IsAddition = br.ReadBoolean();
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.Reacter);
            bw.Write(this.CLIndex);
            bw.Write(this.EmojiIndex);
            bw.Write(this.IsAddition);
        }
    }
}
