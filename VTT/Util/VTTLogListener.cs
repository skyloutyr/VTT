namespace VTT.Util
{
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

        private static readonly System.Numerics.Vector4[] colors = {
            Color.Black.Vec4().SystemVector(),
            Color.DarkGray.Vec4().SystemVector(),
            Color.White.Vec4().SystemVector(),
            Color.Yellow.Vec4().SystemVector(),
            Color.Red.Vec4().SystemVector(),
            Color.DarkRed.Vec4().SystemVector()
        };

        public System.Numerics.Vector4 GetColor(LogLevel level) => Enum.IsDefined(level) ? colors[(int)level] : colors[2];

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
