namespace VTT.Network.VSCC
{
    using Newtonsoft.Json.Linq;
    using VTT.Network.Packet;
    using VTT.Render.Gui;

    public class IntegratorMessage : IIntegrator
    {
        public bool Accepts(string type) => type.Equals("message") || type.Equals("chat_message") || type.Equals("chatmessage");
        public bool Process(JObject data, string fullMessage, VSCCIntegration integration)
        {
            if (data.ContainsKey("text"))
            {
                string msg = string.Empty;
                if (data.ContainsKey("gmr") && data["gmr"].ToObject<bool>())
                {
                    msg += "[d:gm]";
                }

                msg += IIntegrator.SanitizeInput(data["text"].ToObject<string>());
                GuiRenderer.Instance.SendChat(msg);
            }

            return true;
        }
    }
}
