namespace VTT.Network.HTTPAPI
{
    using System;
    using System.Collections.Generic;
    using VTT.Control;
    using VTT.Network.Packet;
    using VTT.Util;

    public class MethodChatMessage : APIMethod
    {
        public override string Identifier => "message";

        private string _msg;

        public override void Act(HTTPAPIEndpoint api)
        {
            if (!this.IsIdentified)
            {
                api.SendAPIError(401, "This API method requires authorization!");
                return;
            }

            if (!string.IsNullOrEmpty(this._msg))
            {
                ChatLine cl;
                lock (api.Container.chatLock)
                {
                    cl = ChatParser.Parse(this._msg, this.IdentifiedClient.Color, this.IdentifiedClient.Name);
                    cl.Index = api.Container.ServerChat.AllChatLines.Count;
                    cl.SenderID = this.IdentifiedClient.ID;
                    cl.SendTime = DateTime.Now;
                    api.Container.ServerChat.AllChatLines.Add(cl);
                    api.Container.ServerChat.NotifyOfChange(cl);
                }

                new PacketChatLine() { Line = cl }.Broadcast(c => c.IsAdmin || cl.CanSee(c.ID));
                api.SendAPIOk(this);
            }
        }

        public override void Construct(HTTPAPIEndpoint api, Dictionary<string, string> kvs) => this.TryGet(kvs, "message", out this._msg);
    }
}
