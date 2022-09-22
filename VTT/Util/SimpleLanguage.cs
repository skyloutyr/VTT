namespace VTT.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class SimpleLanguage
    {
        public Dictionary<string, string> Language { get; } = new Dictionary<string, string>();

        public void LoadFile(string fName)
        {
            fName = "VTT.Embed.Lang." + fName + ".txt";
            bool exists = IOVTT.DoesResourceExist(fName);
            if (!exists)
            {
                fName = "VTT.Embed.Lang.en-EN.txt";
            }

            this.Load(IOVTT.ResourceToString(fName));
        }

        public void Load(string contents)
        {
            this.Language.Clear();
            foreach (string line in contents.Split('\n'))
            {
                int kIdx = line.IndexOf('=');
                if (kIdx != -1)
                {
                    string key = line[..kIdx];
                    string value = line[(kIdx + 1)..].Replace("\\n", "\n");
                    this.Language[key] = value;
                }
            }
        }

        public string Translate(string sIn) => this.Language.ContainsKey(sIn) ? this.Language[sIn] : sIn;
    }
}
