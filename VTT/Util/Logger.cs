namespace VTT.Util
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Threading;

    public class Logger
    {
        static Logger()
        {
            consoleMsgThread = new Thread(ProcessConsole) { IsBackground = true };
            consoleMsgThread.Start();
        }

        public string Prefix { get; set; }
        public string TimeFormat { get; set; }

        public delegate void LogListener(LogLevel level, string message);

        public event LogListener OnLog;

        public LogLevel ActiveLevel { get; set; }

        public void Log(LogLevel level, string message)
        {
            if (this.ActiveLevel != LogLevel.Off || this.ActiveLevel < level)
            {
                message = $"{DateTimeOffset.Now.ToString(this.TimeFormat)} [{this.Prefix}] {message}";
                this.OnLog?.Invoke(level, message);
            }
        }

        public void Exception(LogLevel level, Exception e)
        {
            if (e?.StackTrace != null)
            {
                foreach (string line in e.StackTrace.Split('\n'))
                {
                    Log(level, line);
                }
            }
        }

        private static readonly BlockingCollection<(LogLevel, string)> consoleMsgQueue = new BlockingCollection<(LogLevel, string)>();
        private static readonly Thread consoleMsgThread;
        private static void ProcessConsole()
        {
            static ConsoleColor GetColor(LogLevel level)
            {
                return level switch
                {
                    LogLevel.Off => ConsoleColor.Black,
                    LogLevel.Debug => ConsoleColor.DarkGray,
                    LogLevel.Info => ConsoleColor.White,
                    LogLevel.Warn => ConsoleColor.Yellow,
                    LogLevel.Error => ConsoleColor.Red,
                    LogLevel.Fatal => ConsoleColor.DarkRed,
                    _ => ConsoleColor.White
                };
            }

            while (true)
            {
                (LogLevel, string) msg = consoleMsgQueue.Take();
                ConsoleColor clr = System.Console.ForegroundColor;
                System.Console.ForegroundColor = GetColor(msg.Item1);
                System.Console.WriteLine(msg.Item2);
                System.Console.ForegroundColor = clr;
            }
        }

        public static LogListener Console => (l, msg) => consoleMsgQueue.Add((l, msg));
        public static LogListener Debug => (l, msg) => System.Diagnostics.Debugger.Log(0, null, msg + "\n");

        public class FileLogListener
        {
            private readonly StreamWriter _fs;
            private readonly BlockingCollection<(LogLevel, string)> _internalQueue = new BlockingCollection<(LogLevel, string)>();
            private readonly Thread _writerThread;
            private volatile bool _forcedStop;

            public FileLogListener(string file)
            {
                if (!File.Exists(file))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    this._fs = File.CreateText(file);
                }
                else
                {
                    this._fs = new StreamWriter(File.Open(file, FileMode.Create, FileAccess.Write));
                }

                this._writerThread = new Thread(ProcessWrites) { IsBackground = true };
                this._writerThread.Start();
            }

            public void Close()
            {
                if (!this._forcedStop)
                {
                    this._forcedStop = true;
                    try
                    {
                        this._internalQueue.Add((LogLevel.Off, "Logging to filesystem stopped."));
                    }
                    catch
                    {
                        // NOOP - object may be disposed already if unlucky
                    }

                    this._writerThread.Join();
                }
            }

            private void ProcessWrites()
            {
                while (!this._forcedStop)
                {
                    (LogLevel, string) msg = this._internalQueue.Take();
                    this._fs.WriteLine(msg.Item2);
                }

                // Try a cleanup of the underlying collection first
                while (this._internalQueue.Count > 0)
                {
                    bool b = this._internalQueue.TryTake(out (LogLevel, string) msg, 100);
                    if (b)
                    {
                        this._fs.WriteLine(msg.Item2);
                    }
                    else
                    {
                        break;
                    }
                }

                try
                {
                    this._fs.Flush();
                    this._fs.Close();
                    this._fs.Dispose();
                    this._internalQueue.Dispose();
                }
                catch
                {
                    // NOOP
                }
            }

            public void WriteLine(LogLevel level, string line)
            {
                if (!this._forcedStop)
                {
                    this._internalQueue.Add((level, line));
                }
            }
        }
    }

    public enum LogLevel
    {
        Off,
        Debug,
        Info,
        Warn,
        Error,
        Fatal
    }
}
