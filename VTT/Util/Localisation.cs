namespace VTT.Util
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using VTT.Network;

    public static class Localisation
    {
        public static SimpleLanguage English { get; set; } = new SimpleLanguage("en-EN", true) { Identifier = "English (International)" };
        public static SimpleLanguage CurrentLocale => Client.Instance?.Lang ?? English;

        public static Dictionary<string, SimpleLanguage> Locales { get; } = new Dictionary<string, SimpleLanguage>();
        public static List<SimpleLanguage> AllLocales { get; } = new List<SimpleLanguage>();

        public static event Action<SimpleLanguage, List<string>> OnLanguageLoad;

        public static void GatherAll()
        {
            // Built-in
            AllLocales.Add(Locales["en-EN"] = English);
            AllLocales.Add(Locales["ru-RU"] = new SimpleLanguage("ru-RU") { Identifier = "Русский (Россия)" });
            string langDir = Path.Combine(IOVTT.AppDir, "Languages");
            if (Directory.Exists(langDir))
            {
                foreach (string file in Directory.EnumerateFiles(langDir, "*.txt"))
                {
                    string fName = Path.GetFileNameWithoutExtension(file);
                    if (!Locales.ContainsKey(fName))
                    {
                        try
                        {
                            SimpleLanguage lang = new SimpleLanguage(fName);
                            using StreamReader sr = new StreamReader(File.OpenRead(file));
                            while (true)
                            {
                                string line = sr.ReadLine() ?? throw new Exception("No identifier found!");
                                if (string.IsNullOrEmpty(line))
                                {
                                    continue;
                                }

                                if (line.StartsWith("#"))
                                {
                                    continue;
                                }

                                if (line.StartsWith("identifier="))
                                {
                                    lang.Identifier = line[11..];
                                    break;
                                }
                            }

                            AllLocales.Add(Locales[fName] = lang);
                        }
                        catch (Exception e)
                        {
                            Client.Instance.Logger.Log(LogLevel.Error, $"Malformed language file {file}!");
                            Client.Instance.Logger.Exception(LogLevel.Error, e);
                        }
                    }
                }
            }
        }

        public static SimpleLanguage SwitchLanguage(string locale)
        {
            SimpleLanguage l = GetOrDefault(locale);
            if (!l.Loaded)
            {
                LoadLanguage(l);
            }

            foreach (SimpleLanguage lang in Locales.Values)
            {
                if (lang != l && !lang.IsDefault)
                {
                    lang.Unload();
                }
            }

            return l;
        }

        public static void LoadLanguage(SimpleLanguage lang)
        {
            lang.LoadEmbeddedInformation();
            List<string> eventData = new List<string>();
            OnLanguageLoad?.Invoke(lang, eventData);
            foreach (string s in eventData)
            {
                lang.LoadData(s);
            }

            string expectedFSLangPath = Path.Combine(IOVTT.AppDir, "Languages", $"{lang.Locale}.txt");
            if (File.Exists(expectedFSLangPath))
            {
                lang.LoadData(File.ReadAllText(expectedFSLangPath));
            }
        }

        public static SimpleLanguage GetOrDefault(string locale) => Locales.TryGetValue(locale, out SimpleLanguage ret) ? ret : English;
    }
}
