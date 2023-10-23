namespace VTT.Util
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;

    public static class ArgsManager
    {
        private static readonly Dictionary<string, object> args = new Dictionary<string, object>();

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
            if (args.ContainsKey(key))
            {
                object o = args[key];
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
                    if (!bool.TryParse(value, out bool b))
                    {
                        if (int.TryParse(value, out int i))
                        {
                            b = i > 0;
                        }
                    }

                    args["debug"] = b;
                    break;
                }

                case "-console":
                {
                    if (!bool.TryParse(value, out bool b))
                    {
                        if (int.TryParse(value, out int i))
                        {
                            b = i > 0;
                        }
                    }

                    args["console"] = b;
                    break;
                }

                case "-server":
                {
                    if (int.TryParse(value, out int i))
                    {
                        args["server"] = i;
                    }

                    break;
                }

                case "-quick":
                {
                    if (!bool.TryParse(value, out bool b))
                    {
                        if (int.TryParse(value, out int i))
                        {
                            b = i > 0;
                        }
                    }

                    args["quick"] = b;
                    break;
                }

                case "-connect":
                {
                    if (IPEndPoint.TryParse(value, out IPEndPoint ep) && ep != null)
                    {
                        args["connect"] = ep;
                    }

                    break;
                }

                case "-loglevel":
                {
                    if (Enum.TryParse(value, out LogLevel level))
                    {
                        args["loglevel"] = level;
                    }

                    break;
                }

                case "-gldebug":
                {
                    args["gldebug"] = value;
                    break;
                }

                case "-nocache":
                {
                    args["servercache"] = -1L;
                    break;
                }

                case "-servercache":
                {
                    if (args.TryGetValue("servercache", out object val) && val is long l && l == -1)
                    {
                        break;
                    }

                    try
                    {
                        StringBuilder sb = new StringBuilder();
                        int idx = value.Length - 1;
                        while (!char.IsDigit(value[idx]) && idx >= 0)
                        {
                            sb.Insert(0, value[idx]);
                            --idx;
                        }

                        string type = sb.ToString().Trim();
                        long num = long.Parse(value[..(idx + 1)]);
                        long mul = 1;
                        switch (type)
                        {
                            case "b":
                            case "B":
                            {
                                mul = 1;
                                break;
                            }

                            case "kb":
                            case "kB":
                            case "Kb":
                            case "KB":
                            {
                                mul = 1024;
                                break;
                            }

                            case "mb":
                            case "mB":
                            case "Mb":
                            case "MB":
                            {
                                mul = 1024 * 1024;
                                break;
                            }

                            case "gb":
                            case "gB":
                            case "Gb":
                            case "GB":
                            {
                                mul = 1024 * 1024 * 1024;
                                break;
                            }
                        }

                        num *= mul;
                        args["servercache"] = num;
                    }
                    catch
                    {
                        args["servercache"] = 1024 * 1024 * 1024;
                    }

                    break;
                }

                case "-timeout":
                {
                    try
                    {
                        StringBuilder sb = new StringBuilder();
                        int idx = value.Length - 1;
                        while (!char.IsDigit(value[idx]) && idx >= 0)
                        {
                            sb.Insert(0, value[idx]);
                            --idx;
                        }

                        string type = sb.ToString().Trim();
                        long num = long.Parse(value[..(idx + 1)]);
                        long mul = 1;
                        switch (type)
                        {
                            case "ms":
                            {
                                mul = 1;
                                break;
                            }

                            case "s":
                            {
                                mul = 1000;
                                break;
                            }

                            case "m":
                            {
                                mul = 60000;
                                break;
                            }

                            case "h":
                            {
                                mul = 3600000;
                                break;
                            }
                        }

                        num *= mul;
                        args["timeout"] = num;
                    }
                    catch
                    {
                        args["timeout"] = (long)TimeSpan.FromMinutes(1).TotalMilliseconds;
                    }

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

        public static ArgsData<T> ToData<T>(BaseArgsData self) => new ArgsData<T>(self.Key, (T)self.Value);
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
