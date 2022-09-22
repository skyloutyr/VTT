namespace VTT.Util
{
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.IO;

    public static class ObjectBinaryConverter
    {
        public static Dictionary<byte, Func<BinaryReader, object>> Readers { get; } = new Dictionary<byte, Func<BinaryReader, object>>();
        public static Dictionary<byte, Action<BinaryWriter, object>> Writers { get; } = new Dictionary<byte, Action<BinaryWriter, object>>();

        public static Dictionary<Type, byte> IDsByType { get; } = new Dictionary<Type, byte>();
        public static Dictionary<byte, Type> TypesByID { get; } = new Dictionary<byte, Type>();

        private static byte nextID;

        private static void RegisterConversion(Type t, Func<BinaryReader, object> reader, Action<BinaryWriter, object> writer)
        {
            byte id = nextID++;
            IDsByType[t] = id;
            TypesByID[id] = t;
            Readers[id] = reader;
            Writers[id] = writer;
        }

        private static void RegisterConversion<T>(Func<BinaryReader, T> reader, Action<BinaryWriter, T> writer)
        {
            Type t = typeof(T);
            byte id = nextID++;
            IDsByType[t] = id;
            TypesByID[id] = t;
            Readers[id] = r => reader(r);
            Writers[id] = (w, o) => writer(w, (T)o);
        }

        static ObjectBinaryConverter()
        {
            // Native types
            RegisterConversion(r => r.ReadBoolean(), (w, b) => w.Write(b));
            RegisterConversion(r => r.ReadByte(), (w, b) => w.Write(b));
            RegisterConversion(r => r.ReadSByte(), (w, b) => w.Write(b));
            RegisterConversion(r => r.ReadChar(), (w, b) => w.Write(b));
            RegisterConversion(r => r.ReadUInt16(), (w, b) => w.Write(b));
            RegisterConversion(r => r.ReadUInt32(), (w, b) => w.Write(b));
            RegisterConversion(r => r.ReadUInt64(), (w, b) => w.Write(b));
            RegisterConversion(r => r.ReadInt16(), (w, b) => w.Write(b));
            RegisterConversion(r => r.ReadInt32(), (w, b) => w.Write(b));
            RegisterConversion(r => r.ReadInt64(), (w, b) => w.Write(b));
            RegisterConversion(r => r.ReadSingle(), (w, b) => w.Write(b));
            RegisterConversion(r => r.ReadDouble(), (w, b) => w.Write(b));
            RegisterConversion(r => r.ReadString(), (w, b) => w.Write(b));
            RegisterConversion(r => r.ReadDecimal(), (w, b) => w.Write(b));

            // Complex types
            RegisterConversion(r => new Vector2(r.ReadSingle(), r.ReadSingle()), (w, v) => { w.Write(v.X); w.Write(v.Y); });
            RegisterConversion(r => new Vector2d(r.ReadDouble(), r.ReadDouble()), (w, v) => { w.Write(v.X); w.Write(v.Y); });
            RegisterConversion(r => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
            (w, v) => {
                w.Write(v.X);
                w.Write(v.Y);
                w.Write(v.Z);
            });

            RegisterConversion(r => new Vector3d(r.ReadDouble(), r.ReadDouble(), r.ReadDouble()),
            (w, v) => {
                w.Write(v.X);
                w.Write(v.Y);
                w.Write(v.Z);
            });

            RegisterConversion(r => new Vector4(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
            (w, v) => {
                w.Write(v.X);
                w.Write(v.Y);
                w.Write(v.Z);
                w.Write(v.W);
            });

            RegisterConversion(r => new Vector4d(r.ReadDouble(), r.ReadDouble(), r.ReadDouble(), r.ReadDouble()),
            (w, v) => {
                w.Write(v.X);
                w.Write(v.Y);
                w.Write(v.Z);
                w.Write(v.W);
            });

            RegisterConversion(r => new Color(new Rgba32(r.ReadUInt32())), (w, v) => w.Write(v.ToPixel<Rgba32>().PackedValue));
        }

        public static void Write<T>(BinaryWriter bw, T o)
        {
            Type t = o.GetType();
            if (IDsByType.ContainsKey(t))
            {
                Writers[IDsByType[t]](bw, o);
            }
        }

        public static void WriteWithPrefix(BinaryWriter bw, object o)
        {
            Type t = o.GetType();
            if (IDsByType.ContainsKey(t))
            {
                bw.Write(IDsByType[t]);
                Writers[IDsByType[t]](bw, o);
            }
        }

        public static byte[] WriteWithPrefix(object o)
        {
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(ms);
            WriteWithPrefix(bw, o);
            return ms.ToArray();
        }

        public static T Read<T>(BinaryReader br)
        {
            Type t = typeof(T);
            if (IDsByType.ContainsKey(t))
            {
                return (T)Readers[IDsByType[t]](br);
            }

            return default;
        }

        public static object ReadWithPrefix(BinaryReader br)
        {
            byte id = br.ReadByte();
            if (TypesByID.ContainsKey(id))
            {
                return Readers[id](br);
            }

            br.BaseStream.Position -= 1;
            return default;
        }

        public static object ReadWithPrefix(byte[] data)
        {
            using MemoryStream ms = new MemoryStream(data);
            using BinaryReader br = new BinaryReader(ms);
            return ReadWithPrefix(br);
        }
    }
}
