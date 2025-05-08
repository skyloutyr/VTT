namespace VTT.Network.Packet
{
    using System;
    using VTT.Util;

    public class PacketChatQuery : PacketBaseWithCodec
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

        public override void LookupData(Codec c)
        {
            this.QueryID = c.Lookup(this.QueryID);
            this.ConstructNewQuery = c.Lookup(this.ConstructNewQuery);
            this.QueryData = c.Lookup(this.QueryData);
        }
    }
}
