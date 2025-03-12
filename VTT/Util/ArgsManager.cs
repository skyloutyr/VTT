namespace VTT.Util
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;

    public static class ArgsManager
    {
        private static readonly Dictionary<LaunchArgumentKey, object> args = new Dictionary<LaunchArgumentKey, object>();

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

        public static bool TryGetValue<T>(LaunchArgumentKey key, out T value)
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

                    args[LaunchArgumentKey.DebugMode] = b;
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

                    args[LaunchArgumentKey.ShowConsole] = b;
                    break;
                }

                case "-server":
                {
                    if (int.TryParse(value, out int i))
                    {
                        args[LaunchArgumentKey.HeadlessServerPort] = i;
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

                    args[LaunchArgumentKey.DoQuickLaunch] = b;
                    break;
                }

                case "-debuggerlogging":
                {
                    if (!bool.TryParse(value, out bool b))
                    {
                        if (int.TryParse(value, out int i))
                        {
                            b = i > 0;
                        }
                    }

                    args[LaunchArgumentKey.EnableDebuggerLogging] = b;
                    break;
                }

                case "-serverpersistance":
                {
                    if (!bool.TryParse(value, out bool b))
                    {
                        if (int.TryParse(value, out int i))
                        {
                            b = i > 0;
                        }
                    }

                    args[LaunchArgumentKey.ExplicitServerDataPersistance] = b;
                    break;
                }

                case "-connect":
                {
                    if (IPEndPoint.TryParse(value, out IPEndPoint ep) && ep != null)
                    {
                        args[LaunchArgumentKey.ConnectToEndPoint] = ep;
                    }

                    break;
                }

                case "-loglevel":
                {
                    if (Enum.TryParse(value, out LogLevel level))
                    {
                        args[LaunchArgumentKey.LoggingLevel] = level;
                    }

                    break;
                }

                case "-gldebug":
                {
                    args[LaunchArgumentKey.GLDebugMode] = value;
                    break;
                }

                case "-nocache":
                {
                    args[LaunchArgumentKey.ServerCacheSize] = -1L;
                    break;
                }

                case "-servercache":
                {
                    if (args.TryGetValue(LaunchArgumentKey.ServerCacheSize, out object val) && val is long l && l == -1)
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
                        args[LaunchArgumentKey.ServerCacheSize] = num;
                    }
                    catch
                    {
                        args[LaunchArgumentKey.ServerCacheSize] = 1024L * 1024L * 1024L;
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
                        args[LaunchArgumentKey.NetworkTimeoutSpan] = num;
                    }
                    catch
                    {
                        args[LaunchArgumentKey.NetworkTimeoutSpan] = (long)TimeSpan.FromMinutes(1).TotalMilliseconds;
                    }

                    break;
                }

                case "-serverstorage":
                {
                    args[LaunchArgumentKey.ServerStorage] = value;
                    break;
                }

                case "-clientstorage":
                {
                    args[LaunchArgumentKey.ClientStorage] = value;
                    break;
                }
            }
        }
    }

    public enum LaunchArgumentKey
    {
        DebugMode,
        ShowConsole,
        HeadlessServerPort,
        DoQuickLaunch,
        EnableDebuggerLogging,
        ConnectToEndPoint,
        LoggingLevel,
        GLDebugMode,
        ServerCacheSize,
        NetworkTimeoutSpan,
        ServerStorage,
        ClientStorage,
        ExplicitServerDataPersistance
    }
}
