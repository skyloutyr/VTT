namespace VTT.Util
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using SVec2 = System.Numerics.Vector2;
    using SVec3 = System.Numerics.Vector3;
    using SVec4 = System.Numerics.Vector4;

    public static class Extensions
    {
        public static double ElapsedMillis(this Stopwatch self) => (double)self.ElapsedTicks / TimeSpan.TicksPerMillisecond;

        public static SVec2 Normalized(this SVec2 vec) => SVec2.Normalize(vec); 
        public static SVec3 Normalized(this SVec3 vec) => SVec3.Normalize(vec); 
        public static SVec4 Normalized(this SVec4 vec) => SVec4.Normalize(vec);
        public static Quaternion Normalized(this Quaternion q) => Quaternion.Normalize(q);

        public static SVec3 Xyz(this SVec4 vec) => new SVec3(vec.X, vec.Y, vec.Z);
        public static SVec2 Xy(this SVec3 vec) => new SVec2(vec.X, vec.Y);
        public static SVec2 Xy(this SVec4 vec) => new SVec2(vec.X, vec.Y);
        public static SVec2 Zw(this SVec4 vec) => new SVec2(vec.Z, vec.W);
        public static SVec2 PerpendicularRight(this SVec2 vec) => new SVec2(vec.Y, -vec.X);
        public static SVec2 PerpendicularLeft(this SVec2 vec) => new SVec2(-vec.Y, vec.X);

        public static SVec4 Vec4(this Color color) => ((SVec4)color);
        public static SVec3 Vec3(this Color color)
        {
            SVec4 v4 = color.Vec4();
            return new SVec3(v4.X, v4.Y, v4.Z);
        }

        public static SVec2 MinXY(this SVec2 self)
        {
            float min = MathF.Min(self.X, self.Y);
            return new SVec2(min, min);
        }

        public static SVec2 Abs(this SVec2 self) => new SVec2(MathF.Abs(self.X), MathF.Abs(self.Y));
        public static SVec3 Abs(this SVec3 self) => new SVec3(MathF.Abs(self.X), MathF.Abs(self.Y), MathF.Abs(self.Z));
        public static SVec4 Abs(this SVec4 self) => new SVec4(MathF.Abs(self.X), MathF.Abs(self.Y), MathF.Abs(self.Z), MathF.Abs(self.W));
        public static SVec2 Floor(this SVec2 self) => new SVec2(MathF.Floor(self.X), MathF.Floor(self.Y));
        public static SVec3 Floor(this SVec3 self) => new SVec3(MathF.Floor(self.X), MathF.Floor(self.Y), MathF.Floor(self.Z));
        public static SVec4 Floor(this SVec4 self) => new SVec4(MathF.Floor(self.X), MathF.Floor(self.Y), MathF.Floor(self.Z), MathF.Floor(self.W));
        public static SVec2 Ceil(this SVec2 self) => new SVec2(MathF.Ceiling(self.X), MathF.Ceiling(self.Y));
        public static SVec3 Ceil(this SVec3 self) => new SVec3(MathF.Ceiling(self.X), MathF.Ceiling(self.Y), MathF.Ceiling(self.Z));
        public static SVec4 Ceil(this SVec4 self) => new SVec4(MathF.Ceiling(self.X), MathF.Ceiling(self.Y), MathF.Ceiling(self.Z), MathF.Ceiling(self.W));
        public static SVec2 Round(this SVec2 self) => new SVec2(MathF.Round(self.X), MathF.Round(self.Y));
        public static SVec3 Round(this SVec3 self) => new SVec3(MathF.Round(self.X), MathF.Round(self.Y), MathF.Round(self.Z));
        public static SVec4 Round(this SVec4 self) => new SVec4(MathF.Round(self.X), MathF.Round(self.Y), MathF.Round(self.Z), MathF.Round(self.W));
        public static SVec2 Clamp(this SVec2 self, SVec2 min, SVec2 max) => new SVec2(Math.Clamp(self.X, min.X, max.X), Math.Clamp(self.Y, min.Y, max.Y));
        public static SVec3 Clamp(this SVec3 self, SVec3 min, SVec3 max) => new SVec3(Math.Clamp(self.X, min.X, max.X), Math.Clamp(self.Y, min.Y, max.Y), Math.Clamp(self.Z, min.Z, max.Z));
        public static SVec4 Clamp(this SVec4 self, SVec4 min, SVec4 max) => new SVec4(Math.Clamp(self.X, min.X, max.X), Math.Clamp(self.Y, min.Y, max.Y), Math.Clamp(self.Z, min.Z, max.Z), Math.Clamp(self.W, min.W, max.W));
        public static SVec2 Min(this SVec2 self, SVec2 other) => new SVec2(MathF.Min(self.X, other.X), MathF.Min(self.Y, other.Y));
        public static SVec3 Min(this SVec3 self, SVec3 other) => new SVec3(MathF.Min(self.X, other.X), MathF.Min(self.Y, other.Y), MathF.Min(self.Z, other.Z));
        public static SVec4 Min(this SVec4 self, SVec4 other) => new SVec4(MathF.Min(self.X, other.X), MathF.Min(self.Y, other.Y), MathF.Min(self.Z, other.Z), MathF.Min(self.W, other.W));
        public static SVec2 Max(this SVec2 self, SVec2 other) => new SVec2(MathF.Max(self.X, other.X), MathF.Max(self.Y, other.Y));
        public static SVec3 Max(this SVec3 self, SVec3 other) => new SVec3(MathF.Max(self.X, other.X), MathF.Max(self.Y, other.Y), MathF.Max(self.Z, other.Z));
        public static SVec4 Max(this SVec4 self, SVec4 other) => new SVec4(MathF.Max(self.X, other.X), MathF.Max(self.Y, other.Y), MathF.Max(self.Z, other.Z), MathF.Max(self.W, other.W));
        public static SVec2 Mod(this SVec2 self, float val) => new SVec2(self.X % val, self.Y % val);
        public static SVec3 Mod(this SVec3 self, float val) => new SVec3(self.X % val, self.Y % val, self.Z % val);
        public static SVec4 Mod(this SVec4 self, float val) => new SVec4(self.X % val, self.Y % val, self.Z % val, self.W % val);

        public static SVec2 ComponentMin(params SVec2[] anyVecAmt)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            foreach (SVec2 v in anyVecAmt)
            {
                minX = MathF.Min(minX, v.X);
                minY = MathF.Min(minY, v.Y);
            }

            return new SVec2(minX, minY);
        }

        public static SVec2 ComponentMax(params SVec2[] anyVecAmt)
        {
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            foreach (SVec2 v in anyVecAmt)
            {
                maxX = MathF.Max(maxX, v.X);
                maxY = MathF.Max(maxY, v.Y);
            }

            return new SVec2(maxX, maxY);
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
        public static uint Rgba(this SVec4 clrVec)
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

        public static Color FromVec4(SVec4 vec) => FromArgb(vec.W, vec.X, vec.Y, vec.Z);
        public static Color FromVec3(SVec3 vec) => FromArgb(1, vec.X, vec.Y, vec.Z);
        public static Color ContrastBlackOrWhite(this Color self)
        {
            int r = self.RedB();
            int g = self.GreenB();
            int b = self.BlueB();
            int yiq = ((r * 299) + (g * 587) + (b * 114)) / 1000;
            return yiq >= 128 ? Color.Black : Color.White;
        }

        public static Color Invert(this Color self)
        {
            SVec4 sV = ((SVec4)self);
            return new Color(new SVec4(1, 1, 1, 2 * sV.W) - sV);
        }

        public static Color Darker(this Color self, float darkenBy = 0.15f)
        {
            SVec4 sV = ((SVec4)self);
            float b = 1.0f - darkenBy;
            sV *= new SVec4(b, b, b, 1.0f);
            return new Color(sV);
        }

        public static Color Brighter(this Color self, float brightenBy = 0.15f)
        {
            SVec4 sV = ((SVec4)self);
            float b = 1.0f + brightenBy;
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

        public static void WriteArray<T>(this BinaryWriter bw, T[] collection, Action<BinaryWriter, T> writer)
        {
            bw.Write(collection.Length);
            for (int i = 0; i < collection.Length; ++i)
            {
                writer(bw, collection[i]);
            }
        }

        public static T[] ReadArray<T>(this BinaryReader br, Func<BinaryReader, T> reader)
        {
            int n = br.ReadInt32();
            T[] ret = new T[n];
            for (int i = 0; i < n; ++i)
            {
                ret[i] = reader(br);
            }

            return ret;
        }

        public static SVec2 ReadGlVec2(this BinaryReader br) => new(br.ReadSingle(), br.ReadSingle());
        public static SVec3 ReadGlVec3(this BinaryReader br) => new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        public static SVec4 ReadGlVec4(this BinaryReader br) => new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        public static SVec2 ReadSysVec2(this BinaryReader br) => new(br.ReadSingle(), br.ReadSingle());
        public static SVec3 ReadSysVec3(this BinaryReader br) => new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        public static SVec4 ReadSysVec4(this BinaryReader br) => new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

        public static Matrix4x4 ClearRotation(this Matrix4x4 self)
        {
            return new Matrix4x4(
                1, 0, 0, self.M14,
                0, 1, 0, self.M24,
                0, 0, 1, self.M34,
                self.M41, self.M42, self.M43, self.M44
            );
        }

        public static Matrix4x4 ClearTranslation(this Matrix4x4 self)
        {
            return new Matrix4x4(
                self.M11, self.M12, self.M13, self.M14,
                self.M21, self.M22, self.M23, self.M24,
                self.M31, self.M32, self.M33, self.M34,
                0, 0, 0, self.M44
            );
        }

        public static Vector3 ArbitraryOrthogonal(this Vector3 vec)
        {
            bool b0 = (vec.X < vec.Y) && (vec.X < vec.Z);
            bool b1 = (vec.Y <= vec.X) && (vec.Y < vec.Z);
            bool b2 = (vec.Z <= vec.X) && (vec.Z <= vec.Y);
            return Vector3.Cross(vec, new Vector3(b0 ? 1 : 0, b1 ? 1 : 0, b2 ? 1 : 0));
        }

        public static bool IsEmpty(this Guid id) => Guid.Equals(id, Guid.Empty);
    }
}
