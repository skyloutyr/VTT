namespace VTT
{
    using Antlr4.Runtime.Misc;
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Threading;
    using VTT.Network;
    using VTT.Util;

    public static class Program
    {
        public static Assembly Code { get; internal set; }

        public static Version Version { get; internal set; }

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
#if DEBUG
            Run(args);
#else
            try
            {
                Run(args);
            }
            catch (Exception e)
            {
                try
                {
                    W32ConsoleInterlop.ShowConsole(true); // Always show console when exception occured
                }
                catch
                {
                    // NOOP
                }

                WriteCrashreport(e);
                Console.WriteLine("A critical exception occured!");
                Console.WriteLine(e.Message);
                foreach (string s in e.StackTrace.Split('\n'))
                {
                    Console.WriteLine(s);
                }

                Console.ReadKey();
                throw;
            }
#endif
        }

        private static void Run(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");
            Code = Assembly.GetExecutingAssembly();
            Console.Clear();
            Version = new Version(1, 2, 23);
            ArgsManager.Parse(args);
            IOVTT.LoadLocations();
            if (ArgsManager.TryGetValue(LaunchArgumentKey.HeadlessServerPort, out int port))
            {
                Server s = new Server(IPAddress.Any, port);
                AutoResetEvent are = new AutoResetEvent(false);
                s.Create(are);
                are.WaitOne();
                s.Dispose();
            }
            else
            {
                try
                {
                    bool haveDebug = ArgsManager.TryGetValue<bool>(LaunchArgumentKey.DebugMode, out _); // if debug is present always show
                    bool haveConsole = ArgsManager.TryGetValue(LaunchArgumentKey.ShowConsole, out bool bfConsole);

                    if ((!haveDebug && !haveConsole) || (haveConsole && !bfConsole)) // Hide console if we are not in debug and console arg not present or if the arg is present and explicitly set to false
                    {
                        W32ConsoleInterlop.ShowConsole(false);
                    }
                    else
                    {
                        W32ConsoleInterlop.ShowConsole(true);
                    }
                }
                catch
                {
                    // NOOP
                }

                Client c = new Client();
                if (ArgsManager.TryGetValue(LaunchArgumentKey.ConnectToEndPoint, out IPEndPoint ip))
                {
                    c.Connect(ip);
                }
                else
                {
                    if (ArgsManager.TryGetValue(LaunchArgumentKey.DoQuickLaunch, out bool b) && b)
                    {
                        int sport = 23551;
                        Server s = new Server(IPAddress.Any, sport);
                        s.LocalAdminID = c.ID;
                        s.Create();
                        c.Connect(new IPEndPoint(IPAddress.Loopback, sport));
                    }
                }

                c.Frontend.Run();
            }
        }

        private static void WriteCrashreport(Exception e)
        {
            if (e != null)
            {
                string eT = e.ToString();
                if (e != null && !string.IsNullOrEmpty(e.StackTrace))
                {
                    foreach (string l in e.StackTrace.Split('\n'))
                    {
                        eT += l + '\n';
                    }
                }

                string dateTimeString = DateTimeOffset.Now.ToString("dd-MM-yyyy-HH-mm-ss");
                File.WriteAllText(Path.Combine(IOVTT.AppDir, "crash-" + dateTimeString + ".txt"), eT);
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                WriteCrashreport(e.ExceptionObject as Exception);
                Client.Instance?.CloseLogger();
                Server.Instance?.CloseLogger();
                if (ArgsManager.TryGetValue(LaunchArgumentKey.DebugMode, out bool b) && !Debugger.IsAttached)
                {
                    try
                    {
                        W32MinidumpInterlop.GenerateMiniDump("dump-" + DateTimeOffset.Now.ToString("dd-MM-yyyy-HH-mm-ss") + ".dmp");
                    }
                    catch
                    {
                        // NOOP
                    }
                }
            }
            catch
            {
                // NOOP
            }
        }

        public static ulong GetVersionBytes() => (((ulong)(ushort)Version.Major) << 48) | (((ulong)(ushort)Version.Minor) << 32) | (uint)Version.Build;
    }
}
