namespace VTT.Util
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Numerics;
    using System.Runtime.InteropServices;

    public class DataElement
    {
        private PrimitiveDataUnion _val = new PrimitiveDataUnion();

        private string _stVal;
        private byte[] _bArrVal;
        private readonly Dictionary<string, DataElement> _dataMap = new Dictionary<string, DataElement>();

        public DataType Type { get; set; }

        public DataElement() => this.Type = DataType.Map;

        public DataElement(BinaryReader br) : this() => this.Read(br);

        private static unsafe byte[] ConvertPrimitiveArrayTo<T>(T[] fArr) where T : unmanaged
        {
            if (typeof(T) == typeof(byte))
            {
                return (byte[])(object)fArr;
            }

            int bpe = sizeof(T);
            byte[] ret = new byte[fArr.Length * bpe];
            fixed (byte* bptr = ret)
            {
                fixed (T* tptr = fArr)
                {
                    Buffer.MemoryCopy(tptr, bptr, ret.Length, ret.Length);
                }
            }

            return ret;
        }

        private static unsafe T[] ConvertPrimitiveArrayFrom<T>(byte[] bArr) where T : unmanaged
        {
            if (typeof(T) == typeof(byte))
            {
                return (T[])(object)bArr;
            }

            int bpe = sizeof(T);
            T[] ret = new T[bArr.Length / bpe];
            fixed (byte* bptr = bArr)
            {
                fixed (T* tptr = ret)
                {
                    Buffer.MemoryCopy(bptr, tptr, bArr.Length, bArr.Length);
                }
            }

            return ret;
        }

        public bool Has(string name, DataType type) => this._dataMap.ContainsKey(name) && this._dataMap[name].Type == type;
        public bool GetIfPresent(string name, DataType type, out DataElement de) => this._dataMap.TryGetValue(name, out de) && de.Type == type;
        public bool GetRaw(string name, out DataElement de) => this._dataMap.TryGetValue(name, out de);
        public bool GetType(string name, out DataType type)
        {
            if (this._dataMap.TryGetValue(name, out DataElement de))
            {
                type = de.Type;
                return true;
            }

            type = DataType.Map;
            return false;
        }

        public void Remove(string name) => this._dataMap.Remove(name);

        public bool GetBool(string name, bool defaultVal = false) => this.GetIfPresent(name, DataType.Byte, out DataElement de) ? de._val.bVal == 1 : defaultVal;
        public byte GetByte(string name, byte defaultVal = 0) => this.GetIfPresent(name, DataType.Byte, out DataElement de) ? de._val.bVal : defaultVal;
        public sbyte GetSignedByte(string name, sbyte defaultVal = 0) => this.GetIfPresent(name, DataType.SByte, out DataElement de) ? de._val.sbVal : defaultVal;
        public short GetShort(string name, short defaultVal = 0) => this.GetIfPresent(name, DataType.Short, out DataElement de) ? de._val.sVal : defaultVal;
        public ushort GetUnsignedShort(string name, ushort defaultVal = 0) => this.GetIfPresent(name, DataType.UShort, out DataElement de) ? de._val.usVal : defaultVal;
        public int GetInt(string name, int defaultVal = 0) => this.GetIfPresent(name, DataType.Int, out DataElement de) ? de._val.iVal : defaultVal;
        public uint GetUnsignedInt(string name, uint defaultVal = 0) => this.GetIfPresent(name, DataType.UInt, out DataElement de) ? de._val.uiVal : defaultVal;
        public long GetLong(string name, long defaultVal = 0) => this.GetIfPresent(name, DataType.Long, out DataElement de) ? de._val.lVal : defaultVal;
        public ulong GetUnsignedLong(string name, ulong defaultVal = 0) => this.GetIfPresent(name, DataType.ULong, out DataElement de) ? de._val.ulVal : defaultVal;
        public float GetSingle(string name, float defaultVal = 0) => this.GetIfPresent(name, DataType.Float, out DataElement de) ? de._val.fVal : defaultVal;
        public double GetDouble(string name, double defaultVal = 0) => this.GetIfPresent(name, DataType.Double, out DataElement de) ? de._val.dVal : defaultVal;
        public string GetString(string name, string defaultVal = "") => this.GetIfPresent(name, DataType.String, out DataElement de) ? de._stVal : defaultVal;
        public Guid GetGuid(string name, Guid defaultVal = default) => this.GetIfPresent(name, DataType.Guid, out DataElement de) ? de._val.gVal : defaultVal;
        public Vector2 GetVec2(string name, Vector2 defaultVal = default) => this.GetIfPresent(name, DataType.Vec2, out DataElement de) ? de._val.v2Val : defaultVal;
        public Vector3 GetVec3(string name, Vector3 defaultVal = default) => this.GetIfPresent(name, DataType.Vec3, out DataElement de) ? de._val.v3Val : defaultVal;
        public Vector4 GetVec4(string name, Vector4 defaultVal = default) => this.GetIfPresent(name, DataType.Vec4, out DataElement de) ? de._val.v4Val : defaultVal;
        public Quaternion GetQuaternion(string name, Quaternion defaultVal = default) => this.GetIfPresent(name, DataType.Quaternion, out DataElement de) ? de._val.qVal : defaultVal;
        public T[] GetPrimitiveArray<T>(string name, T[] defaultVal = null) where T : unmanaged => this.GetIfPresent(name, DataType.PrimitiveArray, out DataElement de) ? ConvertPrimitiveArrayFrom<T>(de._bArrVal) : defaultVal;
        public DataElement GetMap(string name, DataElement defaultVal = null) => this.GetIfPresent(name, DataType.Map, out DataElement de) ? de : defaultVal;

        #region Legacy Pre-1.2.28 Support
        public Guid GetGuidLegacy(string name, Guid defaultVal = default)
        {
            if (this.GetRaw(name, out DataElement de))
            {
                switch (de.Type)
                {
                    case DataType.PrimitiveArray:
                    {
                        return new Guid(de._bArrVal);
                    }

                    case DataType.Guid:
                    {
                        return de._val.gVal;
                    }

                    default:
                    {
                        return defaultVal;
                    }
                }
            }
            else
            {
                return defaultVal;
            }
        }

        public Vector2 GetVec2Legacy(string name, Vector2 defaultVal = default)
        {
            if (this.GetRaw(name, out DataElement de))
            {
                switch (de.Type)
                {
                    case DataType.Map:
                    {
                        return new Vector2(de.GetSingle("x"), de.GetSingle("y"));
                    }

                    case DataType.Vec2:
                    {
                        return de._val.v2Val;
                    }

                    default:
                    {
                        return defaultVal;
                    }
                }
            }
            else
            {
                return defaultVal;
            }
        }

        public Vector3 GetVec3Legacy(string name, Vector3 defaultVal = default)
        {
            if (this.GetRaw(name, out DataElement de))
            {
                switch (de.Type)
                {
                    case DataType.Map:
                    {
                        return new Vector3(de.GetSingle("x"), de.GetSingle("y"), de.GetSingle("z"));
                    }

                    case DataType.Vec3:
                    {
                        return de._val.v3Val;
                    }

                    default:
                    {
                        return defaultVal;
                    }
                }
            }
            else
            {
                return defaultVal;
            }
        }

        public Vector4 GetVec4Legacy(string name, Vector4 defaultVal = default)
        {
            if (this.GetRaw(name, out DataElement de))
            {
                switch (de.Type)
                {
                    case DataType.Map:
                    {
                        return new Vector4(de.GetSingle("x"), de.GetSingle("y"), de.GetSingle("z"), de.GetSingle("w"));
                    }

                    case DataType.Vec4:
                    {
                        return de._val.v4Val;
                    }

                    default:
                    {
                        return defaultVal;
                    }
                }
            }
            else
            {
                return defaultVal;
            }
        }

        public Quaternion GetQuaternionLegacy(string name, Quaternion defaultVal = default)
        {
            if (this.GetRaw(name, out DataElement de))
            {
                switch (de.Type)
                {
                    case DataType.Map:
                    {
                        return new Quaternion(de.GetSingle("x"), de.GetSingle("y"), de.GetSingle("z"), de.GetSingle("w"));
                    }

                    case DataType.Quaternion:
                    {
                        return de._val.qVal;
                    }

                    default:
                    {
                        return defaultVal;
                    }
                }
            }
            else
            {
                return defaultVal;
            }
        }

        public T[] GetPrimitiveArrayWithLegacySupport<T>(string name, Func<string, DataElement, T> dataConverter, T[] defaultVal = default) where T : unmanaged
        {
            if (this.GetRaw(name, out DataElement de))
            {
                switch (de.Type)
                {
                    case DataType.PrimitiveArray:
                    {
                        return ConvertPrimitiveArrayFrom<T>(de._bArrVal);
                    }

                    case DataType.Map:
                    {
                        int amt = de.GetInt("_amount");
                        T[] ret = new T[amt];
                        while (--amt >= 0)
                        {
                            string eName = "_e" + amt;
                            ret[amt] = dataConverter(eName, de);
                        }

                        return ret;
                    }

                    default:
                    {
                        return defaultVal;
                    }
                }
            }
            else
            {
                return defaultVal;
            }
        }
        #endregion

        public void SetBool(string name, bool value, bool overrideVals = true)
        {
            const DataType dt = DataType.Byte;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.bVal = value ? (byte)1 : (byte)0;
            this._dataMap[name] = ret;
        }

        public void SetByte(string name, byte value, bool overrideVals = true)
        {
            const DataType dt = DataType.Byte;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.bVal = value;
            this._dataMap[name] = ret;
        }

        public void SetSignedByte(string name, sbyte value, bool overrideVals = true)
        {
            const DataType dt = DataType.SByte;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.sbVal = value;
            this._dataMap[name] = ret;
        }

        public void SetShort(string name, short value, bool overrideVals = true)
        {
            const DataType dt = DataType.Short;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.sVal = value;
            this._dataMap[name] = ret;
        }

        public void SetUnsignedShort(string name, ushort value, bool overrideVals = true)
        {
            const DataType dt = DataType.UShort;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.usVal = value;
            this._dataMap[name] = ret;
        }

        public void SetInt(string name, int value, bool overrideVals = true)
        {
            const DataType dt = DataType.Int;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.iVal = value;
            this._dataMap[name] = ret;
        }

        public void SetUnsignedInt(string name, uint value, bool overrideVals = true)
        {
            const DataType dt = DataType.UInt;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.uiVal = value;
            this._dataMap[name] = ret;
        }

        public void SetLong(string name, long value, bool overrideVals = true)
        {
            const DataType dt = DataType.Long;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.lVal = value;
            this._dataMap[name] = ret;
        }

        public void SetUnsignedLong(string name, ulong value, bool overrideVals = true)
        {
            const DataType dt = DataType.ULong;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.ulVal = value;
            this._dataMap[name] = ret;
        }

        public void SetSingle(string name, float value, bool overrideVals = true)
        {
            const DataType dt = DataType.Float;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.fVal = value;
            this._dataMap[name] = ret;
        }

        public void SetDouble(string name, double value, bool overrideVals = true)
        {
            const DataType dt = DataType.Double;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.dVal = value;
            this._dataMap[name] = ret;
        }

        public void SetString(string name, string value, bool overrideVals = true)
        {
            const DataType dt = DataType.String;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._stVal = value;
            this._dataMap[name] = ret;
        }

        public void SetPrimitiveArray<T>(string name, T[] value, bool overrideVals = true) where T : unmanaged
        {
            const DataType dt = DataType.PrimitiveArray;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._bArrVal = ConvertPrimitiveArrayTo(value);
            this._dataMap[name] = ret;
        }

        public void SetMap(string name, DataElement value, bool overrideVals = true)
        {
            const DataType dt = DataType.Map;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            this._dataMap[name] = value;
        }

        public void SetGuid(string name, Guid value, bool overrideVals = true)
        {
            const DataType dt = DataType.Guid;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.gVal = value;
            this._dataMap[name] = ret;
        }

        public void SetVec2(string name, Vector2 value, bool overrideVals = true)
        {
            const DataType dt = DataType.Vec2;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.v2Val = value;
            this._dataMap[name] = ret;
        }

        public void SetVec3(string name, Vector3 value, bool overrideVals = true)
        {
            const DataType dt = DataType.Vec3;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.v3Val = value;
            this._dataMap[name] = ret;
        }

        public void SetVec4(string name, Vector4 value, bool overrideVals = true)
        {
            const DataType dt = DataType.Vec4;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.v4Val = value;
            this._dataMap[name] = ret;
        }

        public void SetQuaternion(string name, Quaternion value, bool overrideVals = true)
        {
            const DataType dt = DataType.Quaternion;
            if (this._dataMap.TryGetValue(name, out DataElement de) && de.Type != dt && !overrideVals)
            {
                return; // Data type doesn't match and override is disabled
            }

            DataElement ret = new() { Type = dt };
            ret._val.qVal = value;
            this._dataMap[name] = ret;
        }

        public void Write(string file)
        {
            using BinaryWriter bw = new BinaryWriter(File.OpenWrite(file));
            this.Write(bw);
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write((byte)this.Type);
            switch (this.Type)
            {
                case DataType.Byte:
                {
                    bw.Write(this._val.bVal);
                    break;
                }

                case DataType.SByte:
                {
                    bw.Write(this._val.sbVal);
                    break;
                }

                case DataType.Short:
                {
                    bw.Write(this._val.sVal);
                    break;
                }

                case DataType.UShort:
                {
                    bw.Write(this._val.usVal);
                    break;
                }

                case DataType.Int:
                {
                    bw.Write(this._val.iVal);
                    break;
                }

                case DataType.UInt:
                {
                    bw.Write(this._val.uiVal);
                    break;
                }

                case DataType.Long:
                {
                    bw.Write(this._val.lVal);
                    break;
                }

                case DataType.ULong:
                {
                    bw.Write(this._val.ulVal);
                    break;
                }

                case DataType.Float:
                {
                    bw.Write(this._val.fVal);
                    break;
                }

                case DataType.Double:
                {
                    bw.Write(this._val.dVal);
                    break;
                }

                case DataType.String:
                {
                    bw.Write(this._stVal ?? string.Empty);
                    break;
                }

                case DataType.PrimitiveArray:
                {
                    bw.Write(this._bArrVal.Length);
                    bw.Write(this._bArrVal);
                    break;
                }

                case DataType.Guid:
                {
                    bw.Write(this._val.gVal);
                    break;
                }

                case DataType.Vec2:
                {
                    bw.Write(this._val.v2Val);
                    break;
                }

                case DataType.Vec3:
                {
                    bw.Write(this._val.v3Val);
                    break;
                }

                case DataType.Vec4:
                {
                    bw.Write(this._val.v4Val);
                    break;
                }

                case DataType.Quaternion:
                {
                    bw.Write(this._val.qVal);
                    break;
                }

                default:
                {
                    bw.Write(this._dataMap.Count);
                    foreach (KeyValuePair<string, DataElement> data in this._dataMap)
                    {
                        bw.Write(data.Key);
                        data.Value.Write(bw);
                    }

                    break;
                }
            }
        }

        public void Read(BinaryReader br)
        {
            DataType dt = (DataType)br.ReadByte();
            this.Type = dt;
            switch (this.Type)
            {
                case DataType.Byte:
                {
                    this._val.bVal = br.ReadByte();
                    break;
                }

                case DataType.SByte:
                {
                    this._val.sbVal = br.ReadSByte();
                    break;
                }

                case DataType.Short:
                {
                    this._val.sVal = br.ReadInt16();
                    break;
                }

                case DataType.UShort:
                {
                    this._val.usVal = br.ReadUInt16();
                    break;
                }

                case DataType.Int:
                {
                    this._val.iVal = br.ReadInt32();
                    break;
                }

                case DataType.UInt:
                {
                    this._val.uiVal = br.ReadUInt32();
                    break;
                }

                case DataType.Long:
                {
                    this._val.lVal = br.ReadInt64();
                    break;
                }

                case DataType.ULong:
                {
                    this._val.ulVal = br.ReadUInt64();
                    break;
                }

                case DataType.Float:
                {
                    this._val.fVal = br.ReadSingle();
                    break;
                }

                case DataType.Double:
                {
                    this._val.dVal = br.ReadDouble();
                    break;
                }

                case DataType.String:
                {
                    this._stVal = br.ReadString();
                    break;
                }

                case DataType.PrimitiveArray:
                {
                    this._bArrVal = br.ReadBytes(br.ReadInt32());
                    break;
                }

                case DataType.Guid:
                {
                    this._val.gVal = br.ReadGuid();
                    break;
                }

                case DataType.Vec2:
                {
                    this._val.v2Val = br.ReadVec2();
                    break;
                }

                case DataType.Vec3:
                {
                    this._val.v3Val = br.ReadVec3();
                    break;
                }

                case DataType.Vec4:
                {
                    this._val.v4Val = br.ReadVec4();
                    break;
                }

                case DataType.Quaternion:
                {
                    this._val.qVal = br.ReadQuat();
                    break;
                }

                default:
                {
                    int i = br.ReadInt32();
                    while (i-- > 0)
                    {
                        string key = br.ReadString();
                        DataElement elem = new DataElement();
                        elem.Read(br);
                        this._dataMap[key] = elem;
                    }

                    break;
                }
            }
        }
    }

    public enum DataType
    {
        SByte,
        Byte,
        UShort,
        Short,
        UInt,
        Int,
        ULong,
        Long,
        Float,
        Double,
        String,
        PrimitiveArray,
        Map,
        Guid,
        Vec2,
        Vec3,
        Vec4,
        Quaternion,
    }

    [StructLayout(LayoutKind.Explicit, Pack = 0, Size = 32)]
    public struct PrimitiveDataUnion
    {

        [FieldOffset(0)]
        internal sbyte sbVal;
        [FieldOffset(0)]
        internal byte bVal;
        [FieldOffset(0)]
        internal ushort usVal;
        [FieldOffset(0)]
        internal short sVal;
        [FieldOffset(0)]
        internal uint uiVal;
        [FieldOffset(0)]
        internal int iVal;
        [FieldOffset(0)]
        internal ulong ulVal;
        [FieldOffset(0)]
        internal long lVal;
        [FieldOffset(0)]
        internal float fVal;
        [FieldOffset(0)]
        internal double dVal;
        [FieldOffset(0)]
        internal Guid gVal;
        [FieldOffset(0)]
        internal Vector2 v2Val;
        [FieldOffset(0)]
        internal Vector3 v3Val;
        [FieldOffset(0)]
        internal Vector4 v4Val;
        [FieldOffset(0)]
        internal Quaternion qVal;
    }

    public interface ISerializable
    {
        DataElement Serialize();
        void Deserialize(DataElement e);
    }

    public static class DataElementExt
    {
        public static void SetColor(this DataElement self, string name, Color value, bool overrideVals = true)
        {
            Rgba32 pixel = value.ToPixel<Rgba32>();
            self.SetUnsignedInt(name, pixel.PackedValue, overrideVals);
        }

        public static void SetArray<T>(this DataElement self, string name, T[] value, Action<string, DataElement, T> dataConverter, bool overrideVals = true)
        {
            DataElement e = new DataElement();
            e.SetInt("_amount", value.Length);
            for (int i = 0; i < value.Length; ++i)
            {
                string eName = "_e" + i;
                dataConverter(eName, e, value[i]);
            }

            self.SetMap(name, e, overrideVals);
        }

        public static void SetEnum<T>(this DataElement self, string name, T value, bool overrideVals = true) where T : struct, Enum => self.SetInt(name, Convert.ToInt32(value), overrideVals);

        public static Color GetColor(this DataElement self, string name, Color defaultVal = default)
        {
            uint dClr = defaultVal.ToPixel<Rgba32>().PackedValue;
            uint clr = self.GetUnsignedInt(name, dClr);
            return new Color(new Rgba32(clr));
        }

        public static T[] GetArray<T>(this DataElement self, string name, Func<string, DataElement, T> dataConverter, T[] defaultVal = default)
        {
            if (self.Has(name, DataType.Map))
            {
                DataElement map = self.GetMap(name);
                int amt = map.GetInt("_amount");
                T[] ret = new T[amt];
                while (--amt >= 0)
                {
                    string eName = "_e" + amt;
                    ret[amt] = dataConverter(eName, map);
                }

                return ret;
            }

            return defaultVal;
        }

        public static T GetEnum<T>(this DataElement self, string name, T defaultVal = default) where T : struct, Enum
        {
            int dVal = Convert.ToInt32(defaultVal);
            int i = self.GetInt(name, dVal);
            return (T)Enum.ToObject(typeof(T), i);
        }

        public static DateTime GetDateTime(this DataElement self, string name, DateTime defaultVal = default)
        {
            long dVal = (long)(defaultVal.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds;
            long l = self.GetLong(name, dVal);
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(l);
        }

        public static void SetDateTime(this DataElement self, string name, DateTime val)
        {
            long dVal = (long)(val.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds;
            self.SetLong(name, dVal);
        }

        public static void SetObject<T>(this DataElement self, string name, T val) where T : ISerializable => self.SetMap(name, val.Serialize());
        public static void PopulateObject<T>(this DataElement self, string name, T val, DataElement defaultValue = default) where T : ISerializable => val.Deserialize(self.GetMap(name, defaultValue));
    }
}
