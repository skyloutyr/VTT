namespace VTT.Util
{
    using TKVec2 = OpenTK.Mathematics.Vector2;
    using TKVec3 = OpenTK.Mathematics.Vector3;
    using TKVec4 = OpenTK.Mathematics.Vector4;

    using SVec2 = System.Numerics.Vector2;
    using SVec3 = System.Numerics.Vector3;
    using SVec4 = System.Numerics.Vector4;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System.Linq;
    using System;
    using System.IO;

    public static class Extensions
    {
        public static SVec2 SystemVector(this TKVec2 tkVec) => new SVec2(tkVec.X, tkVec.Y);
        public static SVec3 SystemVector(this TKVec3 tkVec) => new SVec3(tkVec.X, tkVec.Y, tkVec.Z);
        public static SVec4 SystemVector(this TKVec4 tkVec) => new SVec4(tkVec.X, tkVec.Y, tkVec.Z, tkVec.W);

        public static TKVec4 GLVector(this SVec4 tkVec) => new TKVec4(tkVec.X, tkVec.Y, tkVec.Z, tkVec.W);
        public static TKVec3 GLVector(this SVec3 tkVec) => new TKVec3(tkVec.X, tkVec.Y, tkVec.Z);
        public static TKVec2 GLVector(this SVec2 tkVec) => new TKVec2(tkVec.X, tkVec.Y);

        public static TKVec4 Vec4(this Color color) => ((SVec4)color).GLVector();
        public static TKVec3 Vec3(this Color color) => ((SVec4)color).GLVector().Xyz;

        public static TKVec2 MinXY(this TKVec2 self)
        {
            float min = MathF.Min(self.X, self.Y);
            return new TKVec2(min, min);
        }

        public static TKVec2 ComponentMin(params TKVec2[] anyVecAmt)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            foreach (TKVec2 v in anyVecAmt)
            {
                minX = MathF.Min(minX, v.X);
                minY = MathF.Min(minY, v.Y);
            }

            return new TKVec2(minX, minY);
        }

        public static TKVec2 ComponentMax(params TKVec2[] anyVecAmt)
        {
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            foreach (TKVec2 v in anyVecAmt)
            {
                maxX = MathF.Max(maxX, v.X);
                maxY = MathF.Max(maxY, v.Y);
            }

            return new TKVec2(maxX, maxY);
        }

        public static float Red(this Color self) => self.ToPixel<Rgba32>().R / 255.0f; 
        public static float Green(this Color self) => self.ToPixel<Rgba32>().G / 255.0f; 
        public static float Blue(this Color self) => self.ToPixel<Rgba32>().B / 255.0f; 
        public static float Alpha(this Color self) => self.ToPixel<Rgba32>().A / 255.0f;

        public static byte RedB(this Color self) => self.ToPixel<Rgba32>().R;
        public static byte GreenB(this Color self) => self.ToPixel<Rgba32>().G;
        public static byte BlueB(this Color self) => self.ToPixel<Rgba32>().B;
        public static byte AlphaB(this Color self) => self.ToPixel<Rgba32>().A;

        public static uint Argb(this Color color) => ((uint)color.AlphaB() << 24) | ((uint)color.RedB() << 16) | ((uint)color.GreenB() << 8) | color.BlueB();
        public static uint Rgba(this TKVec4 clrVec)
        {
            unchecked
            {
                uint br = (byte)(clrVec.X * 255);
                uint bg = (byte)(clrVec.Y * 255);
                uint bb = (byte)(clrVec.Z * 255);
                uint ba = (byte)(clrVec.W * 255);
                return (br << 24) | (bg << 16) | (bb << 8) | ba;
            }
        }
        public static uint Rgba(this Color color) => ((uint)color.RedB() << 24) | ((uint)color.GreenB() << 16) | ((uint)color.Blue() << 8) | color.AlphaB();
        public static uint Abgr(this Color color) => ((uint)color.AlphaB() << 24) | ((uint)color.BlueB() << 16) | ((uint)color.GreenB() << 8) | color.RedB();
        public static uint Bgra(this Color color) => ((uint)color.BlueB() << 24) | ((uint)color.GreenB() << 16) | ((uint)color.RedB() << 8) | color.AlphaB();

        public static OpenTK.Mathematics.Color4 ToGLColor(this Color self)
        {
            TKVec4 v4 = self.Vec4();
            return new OpenTK.Mathematics.Color4(v4.X, v4.Y, v4.Z, v4.W);
        }

        public static string Capitalize(this string self) => string.IsNullOrEmpty(self) ? self : char.ToUpper(self[0]) + self[1..];

        public static string CapitalizeWords(this string self)
        {
            if (string.IsNullOrEmpty(self))
            {
                return self;
            }

            string[] words = self.Split(' ');
            return string.Join(' ', words.Select(w => w.Capitalize()));
        }

        public static Color FromArgb(uint argb) => FromArgb((byte)((argb & 0xFF000000) >> 24), (byte)((argb & 0xFF0000) >> 16), (byte)((argb & 0xFF00) >> 8), (byte)(argb & 0xFF));
        public static Color FromAbgr(uint abgr) => FromArgb((byte)((abgr & 0xFF000000) >> 24), (byte)(abgr & 0xFF), (byte)((abgr & 0xFF00) >> 8), (byte)((abgr & 0xFF0000) >> 16));

        public static Color FromArgb(byte a, byte r, byte g, byte b) => new Color(new SVec4(r / 255F, g / 255F, b / 255F, a / 255F));
        public static Color FromArgb(float a, float r, float g, float b) => new Color(new SVec4(r, g, b, a));
        public static Color FromHex(string hex)
        {
            if (hex.StartsWith('#'))
            {
                hex = hex[1..];
            }

            if (hex.StartsWith("0x"))
            {
                hex = hex[2..];
            }

            float a = 1.0f;
            float r;
            float g;
            float b;
            if (hex.Length == 8)
            {
                a = byte.Parse(hex[0..2], System.Globalization.NumberStyles.HexNumber) / 255f;
                r = byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber) / 255f;
                g = byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber) / 255f;
                b = byte.Parse(hex[6..8], System.Globalization.NumberStyles.HexNumber) / 255f;
            }
            else
            {
                r = byte.Parse(hex[0..2], System.Globalization.NumberStyles.HexNumber) / 255f;
                g = byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber) / 255f;
                b = byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber) / 255f;
            }

            return FromArgb(a, r, g, b);
        }

        public static Color FromVec4(TKVec4 vec) => FromArgb(vec.W, vec.X, vec.Y, vec.Z);
        public static Color FromVec3(TKVec3 vec) => FromArgb(1, vec.X, vec.Y, vec.Z);
        public static Color ContrastBlackOrWhite(this Color self)
        {
            int r = self.RedB();
            int g = self.GreenB();
            int b = self.BlueB();
            int yiq = ((r * 299) + (g * 587) + (b * 114)) / 1000;
            return yiq >= 128 ? Color.Black : Color.White;
        }

        public static Color Darker(this Color self, float darkenBy = 0.15f)
        {
            SVec4 sV = ((SVec4)self);
            float b = 1.0f - darkenBy;
            sV *= new SVec4(b, b, b, 1.0f);
            return new Color(sV);
        }

        public static Color Mix(this Color self, Color other, float a)
        {
            SVec4 sV = (SVec4)self;
            SVec4 oV = (SVec4)other;
            SVec4 r = SVec4.Lerp(sV, oV, a);
            return new Color(r);
        }

        public static Guid ReadGuid(this BinaryReader reader) => new Guid(reader.ReadBytes(16));
        public static void Write(this BinaryWriter writer, Guid id) => writer.Write(id.ToByteArray());
        public static Color ReadColor(this BinaryReader reader) => FromArgb(reader.ReadUInt32());
        public static void Write(this BinaryWriter writer, Color c) => writer.Write(c.Argb());
        public static T ReadEnumSmall<T>(this BinaryReader reader) where T : struct, Enum => (T)Enum.ToObject(typeof(T), reader.ReadByte());
        public static T ReadEnum<T>(this BinaryReader reader) where T : struct, Enum => (T)Enum.ToObject(typeof(T), reader.ReadInt32());
        public static void Write<T>(this BinaryWriter writer, T val) where T : struct, Enum => writer.Write(Convert.ToInt32(val));
        public static void WriteEnumSmall<T>(this BinaryWriter writer, T val) where T : struct, Enum => writer.Write(Convert.ToByte(val));

        public static void Write(this BinaryWriter bw, TKVec2 vec)
        {
            bw.Write(vec.X);
            bw.Write(vec.Y);
        }

        public static void Write(this BinaryWriter bw, TKVec3 vec)
        {
            bw.Write(vec.X);
            bw.Write(vec.Y);
            bw.Write(vec.Z);
        }

        public static void Write(this BinaryWriter bw, TKVec4 vec)
        {
            bw.Write(vec.X);
            bw.Write(vec.Y);
            bw.Write(vec.Z);
            bw.Write(vec.W);
        }

        public static void Write(this BinaryWriter bw, SVec2 vec)
        {
            bw.Write(vec.X);
            bw.Write(vec.Y);
        }

        public static void Write(this BinaryWriter bw, SVec3 vec)
        {
            bw.Write(vec.X);
            bw.Write(vec.Y);
            bw.Write(vec.Z);
        }

        public static void Write(this BinaryWriter bw, SVec4 vec)
        {
            bw.Write(vec.X);
            bw.Write(vec.Y);
            bw.Write(vec.Z);
            bw.Write(vec.W);
        }

        public static TKVec2 ReadGlVec2(this BinaryReader br) => new(br.ReadSingle(), br.ReadSingle());
        public static TKVec3 ReadGlVec3(this BinaryReader br) => new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        public static TKVec4 ReadGlVec4(this BinaryReader br) => new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        public static SVec2 ReadSysVec2(this BinaryReader br) => new(br.ReadSingle(), br.ReadSingle());
        public static SVec3 ReadSysVec3(this BinaryReader br) => new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        public static SVec4 ReadSysVec4(this BinaryReader br) => new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
    }
}
