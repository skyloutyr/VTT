namespace VTT.Util
{
    using VTT.Util;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;

    public class VTTLogListener
    {
        public const int MaxLogLines = 100;

        public static VTTLogListener Instance { get; } = new VTTLogListener();

        public List<Tuple<System.Numerics.Vector4, string>> Logs { get; } = new List<Tuple<System.Numerics.Vector4, string>>();
        public object lockV = new object();

        public void Flush()
        {
        }

        public System.Numerics.Vector4 GetColor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Off:
                {
                    return Color.Black.Vec4().SystemVector();
                }

                case LogLevel.Debug:
                {
                    return Color.DarkGray.Vec4().SystemVector();
                }

                case LogLevel.Info:
                {
                    return Color.White.Vec4().SystemVector();
                }

                case LogLevel.Warn:
                {
                    return Color.Yellow.Vec4().SystemVector();
                }

                case LogLevel.Error:
                {
                    return Color.Red.Vec4().SystemVector();
                }

                case LogLevel.Fatal:
                {
                    return Color.DarkRed.Vec4().SystemVector();
                }

                default:
                {
                    return Color.Black.Vec4().SystemVector();
                }
            }
        }

        public void Write(LogLevel level, string text)
        {
            lock (this.lockV)
            {
                this.Logs.Add(new(this.GetColor(level), text));
                if (this.Logs.Count > MaxLogLines)
                {
                    this.Logs.RemoveAt(0);
                }
            }
        }

        public void WriteLine(LogLevel level, string line)
        {
            lock (this.lockV)
            {
                this.Logs.Add(new(this.GetColor(level), line));
                if (this.Logs.Count > MaxLogLines)
                {
                    this.Logs.RemoveAt(0);
                }
            }
        }
    }
}
