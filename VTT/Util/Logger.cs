namespace VTT.Util
{
    using System;
    using System.IO;

    public class Logger
    {
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

        public static LogListener Console => (l, msg) =>
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

            ConsoleColor clr = System.Console.ForegroundColor;
            System.Console.ForegroundColor = GetColor(l);
            System.Console.WriteLine(msg);
            System.Console.ForegroundColor = clr;
        };

        public static LogListener Debug => (l, msg) => System.Diagnostics.Debugger.Log(0, null, msg + "\n");

        public class FileLogListener
        {
            private readonly StreamWriter _fs;

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
            }

            public void WriteLine(LogLevel level, string line)
            {
                this._fs.WriteLine(line);
                this._fs.Flush();
            }

            public void Close()
            {
                this._fs.Flush();
                this._fs.Close();
                this._fs.Dispose();
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
