namespace VTT.Util
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.IO;
    using System.Linq;

    public static class IOVTT
    {
        static IOVTT()
        {
            AppDir = AppDomain.CurrentDomain.BaseDirectory;
            CommonDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VTT");
            ServerDir = Path.Combine(CommonDir, "Server");
            ClientDir = Path.Combine(CommonDir, "Client");

            Directory.CreateDirectory(CommonDir);
            Directory.CreateDirectory(ServerDir);
            Directory.CreateDirectory(ClientDir);
        }

        public static string AppDir { get; set; }
        public static string ServerDir { get; set; }
        public static string ClientDir { get; set; }
        public static string CommonDir { get; set; }

        public static string OpenLogFile(bool server)
        {
            try
            {
                string baseDir = server ? ServerDir : ClientDir;
                baseDir = Path.Combine(baseDir, "Logs");
                Directory.CreateDirectory(baseDir);
                string logFile = Path.Combine(baseDir, "latest.txt");
                if (File.Exists(logFile))
                {
                    MoveLogFileRecursively(baseDir, logFile, 0);
                }

                return logFile;
            }
            catch
            {
                string baseDir = server ? ServerDir : ClientDir;
                string file = Path.Combine(baseDir, Path.GetRandomFileName() + ".txt");
                return file;
            }
        }

        private static readonly string[] _logNames = { "old.txt", "older.txt", "oldest.txt" };
        private static void MoveLogFileRecursively(string baseDir, string cLogFile, int depth)
        {
            string nLogFile = Path.Combine(baseDir, _logNames[depth]);
            if (File.Exists(nLogFile) && depth != 2)
            {
                MoveLogFileRecursively(baseDir, nLogFile, depth + 1);
            }

            File.Copy(cLogFile, nLogFile, true);
        }

        public static Stream ResourceToStream(string resourcePath) => Program.Code.GetManifestResourceStream(resourcePath);

        public static byte[] ResourceToBytes(string resourcePath)
        {
            using Stream s = Program.Code.GetManifestResourceStream(resourcePath);
            using MemoryStream ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }

        public static string ResourceToString(string resourcePath) => new StreamReader(Program.Code.GetManifestResourceStream(resourcePath)).ReadToEnd();

        public static Image<T> ResourceToImage<T>(string resourcePath) where T : unmanaged, IPixel<T> => Image.Load<T>(Program.Code.GetManifestResourceStream(resourcePath));

        public static string[] ResourceToLines(string resourcePath) => ResourceToString(resourcePath).Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

        public static bool DoesResourceExist(string resource) => Program.Code.GetManifestResourceNames().Any(s => s.Equals(resource));
    }
}
