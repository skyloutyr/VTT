namespace VTT.Util
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;

    public class AppVersion
    {
        [JsonProperty("version")]
        [JsonConverter(typeof(VersionConverter))]
        public Version Version { get; set; }


        [JsonProperty("link")]
        public string Link { get; set; }

        [JsonProperty("changelog")]
        public Dictionary<string, string> Changelog { get; set; }

        private List<(Version, string)> _formattedChangelog;

        public IEnumerable<(Version, string)> EnumerateChangelogData()
        {
            if (this._formattedChangelog == null)
            {
                this.CreateFormattedChangelog();
            }

            return this._formattedChangelog;
        }

        public void CreateFormattedChangelog()
        {
            this._formattedChangelog = new List<(Version, string)>();
            foreach (KeyValuePair<string, string> kv in this.Changelog)
            {
                Version v = Version.Parse(kv.Key);
                string chng = kv.Value;
                chng = chng.Replace(". ", ".\n");
                this._formattedChangelog.Add((v, chng));
            }

            this._formattedChangelog.Sort();
            this._formattedChangelog.Reverse();
        }
    }
}
