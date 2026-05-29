namespace VTT.Network.HTTPAPI
{
    using System;
    using System.Collections.Generic;
    using VTT.Control;
    using VTT.Network.Packet;
    using VTT.Util;

    [APIMethodDocs(
        Name = "Message",
        Tags = new string[] { "Authorized", "Action", "Noreturn" },
        Returns = new string[] {
            "200 - OK if the operation was successful.",
            "401 - Unauthorized if no authorization was provided.",
            "WS: { \"status\": STATUSCODE }"
        },

        Desc = "Sends a chat message to the server from a specified user. The message syntax is exactly the same as if it were sent by the actual client. The client does not need to be online to send this message. Note that the MSG parameter may not include the & symbol if sent by POST or non-json WS. It may be escaped with a \\a syntax."
    )]
    public class MethodChatMessage : APIMethod
    {
        public override string Identifier => "message";

        [APIMethodDocs(Name = "message", ValueKey = "%MSG%", Desc = "The chat message to send.")]
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
