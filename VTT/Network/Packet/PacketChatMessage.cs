namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketChatMessage : PacketBaseWithCodec
    {
        public override uint PacketID => 21;

        public string Message { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(LogLevel.Debug, "Got chat message from " + this.Sender.ID + ": " + this.Message);
                if (string.IsNullOrEmpty(this.Message))
                {
                    server.Logger.Log(LogLevel.Warn, "Got empty message, not allowed");
                    return;
                }

                ChatLine cl;
                lock (server.chatLock)
                {
                    cl = ChatParser.Parse(this.Message, this.Sender.Color, this.Sender.Name);
                    cl.Index = server.ServerChat.Count;
                    cl.SenderID = this.Sender.ID;
                    cl.SendTime = DateTime.Now;
                    server.AppendedChat.Enqueue(cl);
                    server.ServerChat.Add(cl);
                }


                new PacketChatLine() { Line = cl }.Broadcast(c => c.IsAdmin || cl.CanSee(c.ID));
            }
        }

        public override void LookupData(Codec c) => this.Message = c.Lookup(this.Message);
    }
}
