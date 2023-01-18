namespace VTTUpdater
{
    using System;
    using System.IO.Compression;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public static class Program
    {
        private static int lastX;
        private static int lastY;
        private static bool isBackground;

        private static readonly Lazy<HttpClient> client = new Lazy<HttpClient>(() => {
            
            HttpClient c = new HttpClient();
            c.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue() { NoCache = true, NoStore = true };
            return c;
        });

        public static async Task<int> Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            ServicePointManager.ServerCertificateValidationCallback += (s, cert, chain, err) => true;
            Version v = Environment.OSVersion.Version;
            if (v < new Version(6, 2)) // Win 7 or older
            {
                ServicePointManager.Expect100Continue = true;
                AddTslProtocol(SecurityProtocolType.Tls);
                AddTslProtocol(SecurityProtocolType.Tls11);
                AddTslProtocol(SecurityProtocolType.Tls12);
                AddTslProtocol(SecurityProtocolType.Tls13);
#pragma warning disable CS0618 // Type or member is obsolete - Need this for Win7
                AddTslProtocol(SecurityProtocolType.Ssl3);
#pragma warning restore CS0618 // Type or member is obsolete
            }

            bool back = isBackground = args.Length > 0 && args[0] == "--background";
            if (!back)
            {
                Console.Clear();
                Console.SetCursorPosition(0, 0);
                Console.WriteLine("#---------------------------------------------#");
                Console.WriteLine("|                  VTT Updater                |");
                Console.WriteLine("|1.0.0                                        |");
                Console.WriteLine("|     Status:                                 |");
                Console.WriteLine("|                                             |");
                Console.WriteLine("|                                             |");
                Console.WriteLine("|     Progress:                               |");
                Console.WriteLine("|                                             |");
                Console.WriteLine("| [                                         ] |");
                Console.WriteLine("|                                             |");
                Console.WriteLine("|                                             |");
                Console.WriteLine("#---------------------------------------------#");
                (lastX, lastY) = Console.GetCursorPosition();
            }

            WriteStatusString("Startup");
            WriteStatusString("Reading Versions");
            VersionSpec? local = VersionSpec.Local();
            VersionSpec? remote = await VersionSpec.Remote();
            if (local != null && remote != null)
            {
                WriteStatusString("Comparing Versions");
                Version vl = Version.Parse(local.version);
                Version vr = Version.Parse(remote.version);
                bool haveRemote = vl.Major < vr.Major || vl.Minor < vr.Minor || vl.Build < vr.Build;
                if (haveRemote)
                {
                    if (isBackground)
                    {
                        return 1;
                    }
                    else
                    {
                        WriteStatusString("Downloading Latest");
                        MemoryStream ms = new MemoryStream();
                        IProgress<float> p = new Progress<float>(f => WriteProgressBar(f));
                        await HttpClientHelper.DownloadAsync(client.Value, $"https://github.com/skyloutyr/VSCC/releases/download/{remote.version}/VTT.zip", ms, p);
                        WriteStatusString("Extracting");
                        byte[] msBytes = ms.ToArray();
                        MemoryStream aMs = new MemoryStream(msBytes);
                        ZipArchive za = new ZipArchive(aMs, ZipArchiveMode.Read, true);
                        int pEntries = 0;
                        int tEntries = za.Entries.Count;
                        ClearProgressBar();
                        foreach (ZipArchiveEntry zae in za.Entries)
                        {
                            if (zae.Name.ToLower().Contains("updater")) // Updater can't update itself
                            {
                                continue;
                            }

                            zae.ExtractToFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, zae.FullName), true);
                            WriteProgressBar((float)pEntries / tEntries);
                            ++pEntries;
                        }

                        ClearProgressBar();
                        WriteStatusString("Done");
                        Console.WriteLine("Press any key to continue");
                        Console.ReadKey();
                        string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VTT.exe");
                        if (File.Exists(exe))
                        {
                            System.Diagnostics.Process.Start(exe);
                        }

                        return 0;
                    }
                }
                else
                {
                    if (isBackground)
                    {
                        return 0;
                    }
                    else
                    {
                        WriteStatusString("Versions Match!");
                        Console.ReadKey();
                        return 0;
                    }
                }
            }
            else
            {
                if (isBackground)
                {
                    return -1;
                }
                else
                {
                    WriteStatusString("Error");
                    Console.ReadKey();
                }
            }

            return 0;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exe)
            {
                LogException(exe);
            }
        }

        public static void LogException(Exception e)
        {
            try
            {
                string eT = e.ToString();
                if (!string.IsNullOrEmpty(e.StackTrace))
                {
                    foreach (string l in e.StackTrace.Split('\n'))
                    {
                        eT += l + '\n';
                    }
                }

                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash-updater-" + DateTimeOffset.Now.ToString("dd-MM-yyyy-HH-mm-ss") + ".txt"), eT);
            }
            catch
            {
                // NOOP - couldn't even write the crash data
            }
        }

        public static void ClearProgressBar()
        {
            if (isWriting)
            {
                return;
            }

            isWriting = true;
            if (isBackground)
            {
                return;
            }

            Console.SetCursorPosition(21, 7);
            Console.Write("    ");
            Console.SetCursorPosition(0, 8);
            Console.Write("| [                                         ] |");
            Console.SetCursorPosition(lastX, lastY);
            isWriting = false;
        }

        private static volatile bool isWriting;
        public static void WriteProgressBar(float progressValue)
        {
            if (isWriting)
            {
                return;
            }

            isWriting = true;
            if (isBackground)
            {
                return;
            }

            Console.SetCursorPosition(21, 7);
            Console.Write(((int)(Math.Clamp(progressValue, 0, 1) * 100)) + "%");
            string t0 = "                                         ";
            Console.SetCursorPosition(0, 8);
            Console.Write("| [");
            int mN = t0.Length;
            for (int i = 0; i < mN; ++i)
            {
                float ip = (float)i / mN;
                Console.SetCursorPosition(3 + i, 8);
                Console.Write(ip <= progressValue ? '█' : ' ');
            }

            Console.SetCursorPosition(3 + mN, 8);
            Console.Write("] |");
            Console.SetCursorPosition(lastX, lastY);
            isWriting = false;
        }

        public static void WriteStatusString(string statMsg)
        {
            if (isWriting)
            {
                return;
            }

            isWriting = true;
            if (isBackground)
            {
                return;
            }

            Console.SetCursorPosition(0, 4);
            Console.Write("|                                             |");
            Console.SetCursorPosition(8, 4);
            Console.Write(statMsg);
            Console.SetCursorPosition(lastX, lastY);
            isWriting = false;
        }

        public static async Task<string> Request(string url) => await client.Value.GetStringAsync(url);

        public static void AddTslProtocol(SecurityProtocolType spt)
        {
            try
            {
                ServicePointManager.SecurityProtocol |= spt;
            }
            catch
            {
                // NOOP
            }
        }
    }

    public class VersionSpec
    {
        [JsonInclude]
        public string version = string.Empty;

        [JsonInclude]
        public string link = string.Empty;

        [JsonInclude]
        public Dictionary<string, string> changelog = new Dictionary<string, string>();

        public static VersionSpec? Local() => JsonSerializer.Deserialize<VersionSpec>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Version.json")));
        public static async Task<VersionSpec?> Remote()
        {
            string s = await Program.Request("https://raw.githubusercontent.com/skyloutyr/VTT/master/VTT/Version.json");
            return JsonSerializer.Deserialize<VersionSpec>(s);
        }
    }

    public class HttpClientHelper
    {
        public static async Task DownloadAsync(HttpClient client, string requestUri, Stream destination, IProgress<float>? progress = default, CancellationToken cancellationToken = default)
        {
            using (var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                var contentLength = response.Content.Headers.ContentLength;

                using (var download = await response.Content.ReadAsStreamAsync(cancellationToken))
                {

                    // Ignore progress reporting when no progress reporter was 
                    // passed or when the content length is unknown
                    if (progress == null || !contentLength.HasValue)
                    {
                        await download.CopyToAsync(destination, cancellationToken);
                        return;
                    }

                    // Convert absolute progress (bytes downloaded) into relative progress (0% - 100%)
                    var relativeProgress = new Progress<long>(totalBytes => progress.Report((float)totalBytes / contentLength.Value));
                    // Use extension method to report progress while downloading
                    await CopyToAsync(download, destination, 8192, relativeProgress, cancellationToken);
                    progress.Report(1);
                }
            }
        }

        public static async Task CopyToAsync(Stream source, Stream destination, int bufferSize, IProgress<long>? progress = default, CancellationToken cancellationToken = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.CanRead)
                throw new ArgumentException("Has to be readable", nameof(source));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (!destination.CanWrite)
                throw new ArgumentException("Has to be writable", nameof(destination));
            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            var buffer = new byte[bufferSize];
            long totalBytesRead = 0;
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                totalBytesRead += bytesRead;
                progress?.Report(totalBytesRead);
            }
        }
    }
}