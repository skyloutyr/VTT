namespace VTT.Network.VSCC
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using VTT.Network.Packet;
    using VTT.Render.Gui;

    public class IntegratorMacro : IIntegrator
    {
        private readonly string[] _types = { "r20command", "command", "roll20command", "roll20_command", "r20_command", "command_r20", "command_roll20", "macro", "advcommand", "adv_command", "advancedcommand", "advanced_command" };
        private readonly Dictionary<string, Action<JObject, VSCCIntegration, bool>> _templates = new Dictionary<string, Action<JObject, VSCCIntegration, bool>>();

        public IntegratorMacro()
        {
            this._templates["default"] = this.ProcessDefault;
            this._templates["description"] = this.ProcessDesc;
            this._templates["simple"] = this.ProcessSimple;
            this._templates["atkdmg"] = this.ProcessAtkDmg;
            this._templates["spell"] = this.ProcessSpell;
        }

        public bool Accepts(string type) => this._types.Any(t => t.Equals(type));
        public bool Process(JObject data, string fullMessage, VSCCIntegration integration)
        {
            if (data.ContainsKey("template") && data.ContainsKey("data"))
            {
                string template = data["template"].ToObject<string>().ToLower();
                bool gr = data.ContainsKey("gmr") && data["gmr"].ToObject<bool>();
                JObject dataObject = (JObject)data["data"];
                if (this._templates.ContainsKey(template))
                {
                    Client.Instance.Logger.Log(Util.LogLevel.Debug, $"Using {template}");
                    this._templates[template](dataObject, integration, gr);
                    return true;
                }
            }
            else
            {
                Client.Instance.Logger.Log(Util.LogLevel.Debug, "Malformed data, no template and/or data member present");
            }

            return false;
        }

        public void ProcessDefault(JObject data, VSCCIntegration integration, bool gr)
        {
            string msg = "[m:Default]";
            if (gr)
            {
                msg += "[d:gm]";
            }

            foreach (KeyValuePair<string, JToken> kv in data)
            {
                msg += $"[p:{kv.Key}]";
                msg += $"[r:{IIntegrator.SanitizeInput(kv.Value.ToObject<string>())}]";
            }

            PacketChatMessage pcm = new PacketChatMessage() { Message = msg };
            pcm.Send();
        }

        public void ProcessDesc(JObject data, VSCCIntegration integration, bool gr)
        {
            string msg = string.Empty;
            if (gr)
            {
                msg += "[d:gm]";
            }

            msg += data["desc"].ToObject<string>();
            GuiRenderer.Instance.SendChat(msg);
        }

        public void ProcessSimple(JObject data, VSCCIntegration integration, bool gr)
        {
            if (data.ContainsKey("r1"))
            {
                string r1 = IIntegrator.SanitizeR20Harsh(data["r1"].ToObject<string>());
                string r2 = data.ContainsKey("r2") ? IIntegrator.SanitizeR20Harsh(data["r2"].ToObject<string>()) : r1;
                string rname = " ";
                if (data.ContainsKey("name") || data.ContainsKey("rname"))
                {
                    rname = data.ContainsKey("name") ? data["name"].ToObject<string>() : data["rname"].ToObject<string>();
                }

                string cName = " ";
                if (data.ContainsKey("cname") || data.ContainsKey("charname"))
                {
                    cName = data.ContainsKey("cname") ? data["cname"].ToObject<string>() : data["charname"].ToObject<string>();
                }

                if (data.ContainsKey("mod"))
                {
                    rname += $" ({data["mod"].ToObject<string>()})";
                }

                string finalText = $"[m:Simple]";
                if (gr)
                {
                    finalText += "[d:gm]";
                }

                finalText += $"[p:{rname}][p:{cName}][{r1}][{r2}]";
                GuiRenderer.Instance.SendChat(finalText);
            }
        }

        public void ProcessAtkDmg(JObject data, VSCCIntegration integration, bool gr)
        {
            if (data.ContainsKey("r1"))
            {
                string r1 = IIntegrator.SanitizeR20Harsh(data["r1"].ToObject<string>());
                string r2 = data.ContainsKey("r2") ? IIntegrator.SanitizeR20Harsh(data["r2"].ToObject<string>()) : r1;
                string dmg1 = IIntegrator.SanitizeR20Harsh(data["dmg1"].ToObject<string>());
                string dmg2 = data.ContainsKey("dmg2") ? IIntegrator.SanitizeR20Harsh(data["dmg2"].ToObject<string>()) : "p:null";
                string crit1 = IIntegrator.SanitizeR20Harsh(data["crit1"].ToObject<string>());
                string crit2 = data.ContainsKey("crit2") ? IIntegrator.SanitizeR20Harsh(data["crit2"].ToObject<string>()) : "p:null";
                string rname = this.GetOrDefault(data, " ", "name", "rname");
                string cName = this.GetOrDefault(data, " ", "cname", "charname");
                string desc = this.GetOrDefault(data, " ", "desc");
                string mod = this.GetOrDefault(data, " ", "mod");
                string dmg1type = this.GetOrDefault(data, " ", "dmg1type");
                string finalText = $"[m:AtkDmg]";
                if (gr)
                {
                    finalText += "[d:gm]";
                }

                finalText += $"[p:{rname}][p:{mod}][p:{cName}][p:{desc}][{r1}][{r2}][{dmg1}][{dmg2}][{crit1}][{crit2}][p:{dmg1type}]";
                GuiRenderer.Instance.SendChat(finalText);
            }
        }

        public void ProcessSpell(JObject data, VSCCIntegration integration, bool gr)
        {
            string castingTime = this.GetOrDefault(data, " ", "castingtime");
            string cname = this.GetOrDefault(data, " ", "cname", "charname", "charactername");
            string desc = this.GetOrDefault(data, " ", "desc", "description");
            string duration = this.GetOrDefault(data, " ", "duration");
            string name = this.GetOrDefault(data, " ", "name");
            string range = this.GetOrDefault(data, " ", "range");
            string schoollevel = this.GetOrDefault(data, " ", "level");
            string target = this.GetOrDefault(data, " ", "target");
            string concentration = this.GetOrDefault(data, "0", "concentration");
            string material = this.GetOrDefault(data, "0", "m");
            string somatic = this.GetOrDefault(data, "0", "s");
            string verbal = this.GetOrDefault(data, "0", "v");
            string ritual = this.GetOrDefault(data, "0", "ritual");

            string finalText = $"[m:Spell]";
            if (gr)
            {
                finalText += "[d:gm]";
            }

            finalText += $"[p:{name}][p:{schoollevel}][p:{cname}][p:{castingTime}][p:{range}][p:{target}][p:{duration}][p:{verbal}][p:{somatic}][p:{material}][p:{concentration}][p:{ritual}][p:{desc}]";
            GuiRenderer.Instance.SendChat(finalText);
        }

        private T GetOrDefault<T>(JObject data, T defaultVal, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (data.ContainsKey(key))
                {
                    return data[key].ToObject<T>();
                }
            }

            return defaultVal;
        }
    }
}
