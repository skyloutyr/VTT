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
            ConsoleColor GetColor(LogLevel level)
            {
                switch (level)
                {
                    case LogLevel.Off:
                    {
                        return ConsoleColor.Black;
                    }

                    case LogLevel.Debug:
                    {
                        return ConsoleColor.DarkGray;
                    }

                    case LogLevel.Info:
                    {
                        return ConsoleColor.White;
                    }

                    case LogLevel.Warn:
                    {
                        return ConsoleColor.Yellow;
                    }

                    case LogLevel.Error:
                    {
                        return ConsoleColor.Red;
                    }

                    case LogLevel.Fatal:
                    {
                        return ConsoleColor.DarkRed;
                    }

                    default:
                    {
                        return ConsoleColor.Black;
                    }
                }
            }

            ConsoleColor clr = System.Console.ForegroundColor;
            System.Console.ForegroundColor = GetColor(l);
            System.Console.WriteLine(msg);
            System.Console.ForegroundColor = clr;
        };

        public static LogListener Debug => (l, msg) =>
        {
            System.Diagnostics.Debugger.Log(0, null, msg);
        };

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
