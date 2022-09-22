namespace VTT.Util
{
    using System;
    using System.Collections.Generic;
    using System.Net;

    public static class ArgsManager
    {
        public static Dictionary<string, object> Args = new Dictionary<string, object>();

        public static void Parse(string[] args)
        {
            for (int i = 0; i < args.Length; i += 2)
            {
                if (i + 1 < args.Length)
                {
                    ReadArg(args[i], args[i + 1]);
                }
            }
        }

        public static bool TryGetValue<T>(string key, out T value)
        {
            if (Args.ContainsKey(key))
            {
                object o = Args[key];
                if (o.GetType() == typeof(T))
                {
                    value = (T)o;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static void ReadArg(string key, string value)
        {
            switch (key.ToLower())
            {
                case "-debug":
                {
                    bool b;
                    if (!bool.TryParse(value, out b))
                    {
                        if (int.TryParse(value, out int i))
                        {
                            b = i > 0;
                        }
                    }

                    Args["debug"] = b;
                    break;
                }

                case "-server":
                {
                    if (int.TryParse(value, out int i))
                    {
                        Args["server"] = i;
                    }

                    break;
                }

                case "-quick":
                {
                    bool b;
                    if (!bool.TryParse(value, out b))
                    {
                        if (int.TryParse(value, out int i))
                        {
                            b = i > 0;
                        }
                    }

                    Args["quick"] = b;
                    break;
                }

                case "-connect":
                {
                    if(IPEndPoint.TryParse(value, out IPEndPoint ep) && ep != null)
                    {
                        Args["connect"] = ep;
                    }

                    break;
                }

                case "-loglevel":
                {
                    if (Enum.TryParse(value, out LogLevel level))
                    {
                        Args["loglevel"] = level;
                    }

                    break;
                }

                case "-gldebug":
                {
                    Args["gldebug"] = value;
                    break;
                }
            }
        }
    }

    public readonly struct BaseArgsData
    {
        public string Key { get; }
        public object Value { get; }

        public BaseArgsData(string key, object value)
        {
            this.Key = key;
            this.Value = value;
        }

        public static ArgsData<T> ToData<T>(BaseArgsData self)
        {
            return new ArgsData<T>(self.Key, (T)self.Value);
        }
    }

    public readonly struct ArgsData<T>
    {
        public string Key { get; }
        public T Value { get; }

        public ArgsData(string key, T value)
        {
            this.Key = key;
            this.Value = value;
        }
    }
}
