namespace VTT.Network.VSCC
{
    using Newtonsoft.Json.Linq;
    using System;
    public class IntegratorPoll : IIntegrator
    {
        public bool Accepts(string type) => type.Equals("poll") || type.Equals("listen_poll") || type.Equals("poll_listen");
        public bool Process(JObject data, string fullMessage, VSCCIntegration integration)
        {
            integration.SendError(5, "This client can't poll data");
            return true;
        }
    }
}
