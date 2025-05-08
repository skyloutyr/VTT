namespace VTT.Network.Packet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketChatQueryLines : PacketBase
    {
        public const int ChatLinesRequestedForQuery = 25;
        public override uint PacketID => 81;

        public Guid QueryID { get; set; }
        public List<ChatLine> Lines { get; } = new List<ChatLine>();

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                this.Lines.AddRange(server.ProvideChatQueryLines(this.Sender.ID, this.QueryID, ChatLinesRequestedForQuery));
                this.Send(this.Sender);
            }
            else
            {
                Client.Instance.Frontend.Renderer.GuiRenderer.ReceiveChatSearchQueryLines(this.QueryID, this.Lines);
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.QueryID = br.ReadGuid();
            if (!this.IsServer)
            {
                int cnt = br.ReadInt32();
                for (int i = 0; i < cnt; ++i)
                {
                    ChatLine cl = new ChatLine();
                    cl.Index = br.ReadInt32();
                    cl.ReadNetwork(br);
                    this.Lines.Add(cl);
                }
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.QueryID);
            if (this.IsServer)
            {
                bw.Write(this.Lines.Count);
                foreach (ChatLine cl in this.Lines)
                {
                    bw.Write(cl.Index);
                    cl.WriteNetwork(bw);
                }
            }
        }
    }
}
