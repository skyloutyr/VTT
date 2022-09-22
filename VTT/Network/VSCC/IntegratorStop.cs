namespace VTT.Network.VSCC
{
    using Newtonsoft.Json.Linq;

    public class IntegratorStop : IIntegrator
    {
        public bool Accepts(string type) => type.Equals("stop") || type.Equals("exit") || type.Equals("close");

        public bool Process(JObject data, string fullMessage, VSCCIntegration integration)
        {
            integration.Destroy();
            return true;
        }
    }
}
