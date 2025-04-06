namespace VTT.Util
{
    using System.Collections.Generic;
    using System.IO;

    public class SimpleLanguage
    {
        public Dictionary<string, string> Language { get; } = new Dictionary<string, string>();

        public string Locale { get; }
        public bool IsDefault { get; }
        public bool Loaded { get; set; }
        public string Identifier { get; set; }

        public SimpleLanguage(string identifier, bool isDefault = false) 
        { 
            this.Locale = identifier;
            this.IsDefault = isDefault;
        }

        public void LoadEmbeddedInformation()
        {
            string fName = $"VTT.Embed.Lang.{this.Locale}.txt";
            Stream s = IOVTT.ResourceToStream(fName);
            if (s != null)
            {
                using StreamReader sr = new StreamReader(s);
                this.LoadData(sr.ReadToEnd());
                s.Dispose();
            }
        }

        public void LoadData(string contents)
        {
            foreach (string line in contents.Split('\n'))
            {
                int kIdx = line.IndexOf('=');
                if (kIdx != -1)
                {
                    string key = line[..kIdx];
                    string value = line[(kIdx + 1)..].Replace("\\n", "\n").Replace("\r", "");
                    this.Language[key] = value;
                }
            }
        }

        public void Unload() => this.Language.Clear();

        public string Translate(string sIn) => this.Language.TryGetValue(sIn, out string sVal) ? sVal : this.IsDefault ? sIn : Localisation.English.Translate(sIn);
        public string Translate(string sIn, params object[] format) => this.Language.TryGetValue(sIn, out string sVal) ? string.Format(sVal, format) : this.IsDefault ? sIn : Localisation.English.Translate(sIn, format);
    }
}
