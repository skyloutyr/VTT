namespace VTT
{
    using System;
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
            void Run()
            {
                CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");
                Code = Assembly.GetExecutingAssembly();
                Console.Clear();
                Version = new Version(1, 1, 11);
                ArgsManager.Parse(args);
                if (ArgsManager.TryGetValue("server", out int port))
                {
                    Server s = new Server(IPAddress.Any, port);
                    AutoResetEvent are = new AutoResetEvent(false);
                    s.Create(are);
                    are.WaitOne();
                    s.Dispose();
                }
                else
                {
                    Client c = new Client();
                    if (ArgsManager.TryGetValue("connect", out IPEndPoint ip))
                    {
                        c.Connect(ip);
                    }
                    else
                    {
                        if (ArgsManager.TryGetValue("quick", out bool b) && b)
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
#if DEBUG
            Run();
#else
            try
            {
                Run();
            }
            catch (Exception e)
            {
                Console.WriteLine("A critical exception occured!");
                Console.WriteLine(e.Message);
                foreach (string s in e.StackTrace.Split('\n'))
                {
                    Console.WriteLine(s);
                }

                Console.ReadKey();
            }
#endif
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                string eT = e.ExceptionObject?.ToString() ?? string.Empty;
                if (e.ExceptionObject is Exception exe && !string.IsNullOrEmpty(exe.StackTrace))
                {
                    foreach (string l in exe.StackTrace.Split('\n'))
                    {
                        eT += l + '\n';
                    }
                }

                File.WriteAllText(Path.Combine(IOVTT.AppDir, "crash-" + DateTimeOffset.Now.ToString("dd-MM-yyyy-HH-mm-ss") + ".txt"), eT);
            }
            catch
            {
                // NOOP
            }
        }

        public static ulong GetVersionBytes() => (((ulong)(ushort)Version.Major) << 48) | (((ulong)(ushort)Version.Minor) << 32) | (uint)Version.Build;
    }
}
