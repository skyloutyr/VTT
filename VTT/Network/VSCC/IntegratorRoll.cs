namespace VTT.Network.VSCC
{
    using Newtonsoft.Json.Linq;
    using System;
    using VTT.Render.Gui;

    public class IntegratorRoll : IIntegrator
    {
        public bool Accepts(string type) => type.Equals("roll");

        public bool Process(JObject data, string fullMessage, VSCCIntegration integration)
        {
            if (data.ContainsKey("numDice") && data.ContainsKey("numSides"))
            {
                int dice = Math.Min(data["numDice"].ToObject<int>(), 10000);
                int side = data["numSides"].ToObject<int>();
                string message = $"[m:DiceRolls]";
                if (data.ContainsKey("gmr") && data["gmr"].ToObject<bool>())
                {
                    message += "[d:gm]";
                }

                for (int i = 0; i < dice; ++i)
                {
                    message += $"[roll(1, {side})]";
                }

                GuiRenderer.Instance.SendChat(message);
                return true;
            }

            return false;
        }
    }
}
