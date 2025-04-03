namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Util;

    public class PacketChatQuery : PacketBase
    {
        public override uint PacketID => 80;

        public Guid QueryID { get; set; }
        public bool ConstructNewQuery { get; set; }
        public DataElement QueryData { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.HandleChatQueryData(this.QueryID, this.QueryData, this.ConstructNewQuery);
            }
            else
            {
                this.ContextLogger.Log(LogLevel.Warn, "Client got a chat query packet, should be impossible!");
                Client.Instance.Frontend.Renderer.GuiRenderer.ReceiveChatSearchQueryData(this.QueryData); // ?
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.QueryID = br.ReadGuid();
            this.ConstructNewQuery = br.ReadBoolean();
            this.QueryData = new DataElement(br);
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.QueryID);
            bw.Write(this.ConstructNewQuery);
            this.QueryData.Write(bw);
        }
    }
}
