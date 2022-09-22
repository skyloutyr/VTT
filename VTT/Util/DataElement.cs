namespace VTT.Util
{
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class DataElement
    {
        private sbyte _sbVal;
        private byte _bVal;
        private ushort _usVal;
        private short _sVal;
        private uint _uiVal;
        private int _iVal;
        private ulong _ulVal;
        private long _lVal;
        private float _fVal;
        private double _dVal;
        private string _stVal;
        private byte[] _bArrVal;
        private readonly Dictionary<string, DataElement> _dataMap = new Dictionary<string, DataElement>();

        public DataType Type { get; set; }

        public DataElement() => this.Type = DataType.Map;

        public DataElement(BinaryReader br) : this() => this.Read(br);

        public bool Has(string name, DataType type) => this._dataMap.ContainsKey(name) && this._dataMap[name].Type == type;

        public void Remove(string name) => this._dataMap.Remove(name);

        public T Get<T>(string name, T defaultVal = default)
        {
            if (typeof(T) == typeof(byte) || typeof(T) == typeof(bool)) // Specific bool handling
            {
                byte b = this.Has(name, DataType.Byte) ? this._dataMap[name]._bVal : ((bool)(object)defaultVal ? (byte)1 : (byte)0);
                return typeof(T) == typeof(bool) ? (T)(object)(b == 1) : (T)(object)b;
            }

            DataType dt =
                typeof(T) == typeof(sbyte) ? DataType.SByte :
                typeof(T) == typeof(short) ? DataType.Short :
                typeof(T) == typeof(ushort) ? DataType.UShort : 
                typeof(T) == typeof(int) ? DataType.Int :
                typeof(T) == typeof(uint) ? DataType.UInt :
                typeof(T) == typeof(long) ? DataType.Long :
                typeof(T) == typeof(ulong) ? DataType.ULong :
                typeof(T) == typeof(float) ? DataType.Float :
                typeof(T) == typeof(double) ? DataType.Double :
                typeof(T) == typeof(string) ? DataType.String :
                typeof(T) == typeof(byte[]) ? DataType.ByteArray :
                DataType.Map;

            if (this.Has(name, dt))
            {
                switch (dt)
                {
                    case DataType.SByte:
                    {
                        return (T)(object)this._dataMap[name]._sbVal;
                    }

                    case DataType.Short:
                    {
                        return (T)(object)this._dataMap[name]._sVal;
                    }

                    case DataType.UShort:
                    {
                        return (T)(object)this._dataMap[name]._usVal;
                    }

                    case DataType.Int:
                    {
                        return (T)(object)this._dataMap[name]._iVal;
                    }

                    case DataType.UInt:
                    {
                        return (T)(object)this._dataMap[name]._uiVal;
                    }

                    case DataType.Long:
                    {
                        return (T)(object)this._dataMap[name]._lVal;
                    }

                    case DataType.ULong:
                    {
                        return (T)(object)this._dataMap[name]._ulVal;
                    }

                    case DataType.Float:
                    {
                        return (T)(object)this._dataMap[name]._fVal;
                    }

                    case DataType.Double:
                    {
                        return (T)(object)this._dataMap[name]._dVal;
                    }

                    case DataType.String:
                    {
                        return (T)(object)this._dataMap[name]._stVal;
                    }

                    case DataType.ByteArray:
                    {
                        return (T)(object)this._dataMap[name]._bArrVal;
                    }

                    default:
                    {
                        return (T)(object)this._dataMap[name];
                    }
                }
            }

            return defaultVal;
        }

        public unsafe void Set<T>(string name, T value, bool overrideVals = true)
        {
            DataType dt =
                typeof(T) == typeof(byte) || typeof(T) == typeof(bool) ? DataType.Byte :
                typeof(T) == typeof(sbyte) ? DataType.SByte :
                typeof(T) == typeof(short) ? DataType.Short :
                typeof(T) == typeof(ushort) ? DataType.UShort :
                typeof(T) == typeof(int) ? DataType.Int :
                typeof(T) == typeof(uint) ? DataType.UInt :
                typeof(T) == typeof(long) ? DataType.Long :
                typeof(T) == typeof(ulong) ? DataType.ULong :
                typeof(T) == typeof(float) ? DataType.Float :
                typeof(T) == typeof(double) ? DataType.Double :
                typeof(T) == typeof(string) ? DataType.String :
                typeof(T) == typeof(byte[]) ? DataType.ByteArray :
                DataType.Map;

            if (this._dataMap.ContainsKey(name))
            {
                DataType r = this._dataMap[name].Type;
                if (r != dt && !overrideVals)
                {
                    return; // Data type doesn't match and override is disabled
                }
            }

            DataElement ret = new() { Type = dt };
            switch (dt)
            {
                case DataType.Byte:
                {
                    ret._bVal = typeof(T) == typeof(bool) ? ((bool)(object)value ? (byte)1 : (byte)0) : (byte)(object)value;
                    break;
                }

                case DataType.SByte:
                {
                    ret._sbVal = (sbyte)(object)value;
                    break;
                }

                case DataType.Short:
                {
                    ret._sVal = (short)(object)value;
                    break;
                }

                case DataType.UShort:
                {
                    ret._usVal = (ushort)(object)value;
                    break;
                }

                case DataType.Int:
                {
                    ret._iVal = (int)(object)value;
                    break;
                }

                case DataType.UInt:
                {
                    ret._uiVal = (uint)(object)value;
                    break;
                }

                case DataType.Long:
                {
                    ret._lVal = (long)(object)value;
                    break;
                }

                case DataType.ULong:
                {
                    ret._ulVal = (ulong)(object)value;
                    break;
                }

                case DataType.Float:
                {
                    ret._fVal = (float)(object)value;
                    break;
                }

                case DataType.Double:
                {
                    ret._dVal = (double)(object)value;
                    break;
                }

                case DataType.String:
                {
                    ret._stVal = (string)(object)value;
                    break;
                }

                case DataType.ByteArray:
                {
                    ret._bArrVal = (byte[])(object)value;
                    break;
                }

                case DataType.Map:
                {
                    ret = (DataElement)(object)value;
                    break;
                }

                default:
                {
                    break;
                }
            }

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
                    bw.Write(this._bVal);
                    break;
                }

                case DataType.SByte:
                {
                    bw.Write(this._sbVal);
                    break;
                }

                case DataType.Short:
                {
                    bw.Write(this._sVal);
                    break;
                }

                case DataType.UShort:
                {
                    bw.Write(this._usVal);
                    break;
                }

                case DataType.Int:
                {
                    bw.Write(this._iVal);
                    break;
                }

                case DataType.UInt:
                {
                    bw.Write(this._uiVal);
                    break;
                }

                case DataType.Long:
                {
                    bw.Write(this._lVal);
                    break;
                }

                case DataType.ULong:
                {
                    bw.Write(this._ulVal);
                    break;
                }

                case DataType.Float:
                {
                    bw.Write(this._fVal);
                    break;
                }

                case DataType.Double:
                {
                    bw.Write(this._dVal);
                    break;
                }

                case DataType.String:
                {
                    bw.Write(this._stVal ?? string.Empty);
                    break;
                }

                case DataType.ByteArray:
                {
                    bw.Write(this._bArrVal.Length);
                    bw.Write(this._bArrVal);
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
                    this._bVal = br.ReadByte();
                    break;
                }

                case DataType.SByte:
                {
                    this._sbVal = br.ReadSByte();
                    break;
                }

                case DataType.Short:
                {
                    this._sVal = br.ReadInt16();
                    break;
                }

                case DataType.UShort:
                {
                    this._usVal = br.ReadUInt16();
                    break;
                }

                case DataType.Int:
                {
                    this._iVal = br.ReadInt32();
                    break;
                }

                case DataType.UInt:
                {
                    this._uiVal = br.ReadUInt32();
                    break;
                }

                case DataType.Long:
                {
                    this._lVal = br.ReadInt64();
                    break;
                }

                case DataType.ULong:
                {
                    this._ulVal = br.ReadUInt64();
                    break;
                }

                case DataType.Float:
                {
                    this._fVal = br.ReadSingle();
                    break;
                }

                case DataType.Double:
                {
                    this._dVal = br.ReadDouble();
                    break;
                }

                case DataType.String:
                {
                    this._stVal = br.ReadString();
                    break;
                }

                case DataType.ByteArray:
                {
                    this._bArrVal = br.ReadBytes(br.ReadInt32());
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
        ByteArray,
        Map
    }

    public interface ISerializable
    {
        DataElement Serialize();
        void Deserialize(DataElement e);
    }

    public static class DataElementExt
    {
        public static void SetGuid(this DataElement self, string name, Guid value, bool overrideVals = true)
        {
            byte[] valB = value.ToByteArray();
            self.Set(name, valB, overrideVals);
        }

        public static void SetVec2(this DataElement self, string name, Vector2 value, bool overrideVals = true)
        {
            DataElement e = new DataElement();
            e.Set("x", value.X);
            e.Set("y", value.Y);
            self.Set(name, e, overrideVals);
        }

        public static void SetVec3(this DataElement self, string name, Vector3 value, bool overrideVals = true)
        {
            DataElement e = new DataElement();
            e.Set("x", value.X);
            e.Set("y", value.Y);
            e.Set("z", value.Z);
            self.Set(name, e, overrideVals);
        }

        public static void SetVec4(this DataElement self, string name, Vector4 value, bool overrideVals = true)
        {
            DataElement e = new DataElement();
            e.Set("x", value.X);
            e.Set("y", value.Y);
            e.Set("z", value.Z);
            e.Set("w", value.W);
            self.Set(name, e, overrideVals);
        }

        public static void SetColor(this DataElement self, string name, Color value, bool overrideVals = true)
        {
            Rgba32 pixel = value.ToPixel<Rgba32>();
            self.Set(name, pixel.PackedValue, overrideVals);
        }

        public static void SetArray<T>(this DataElement self, string name, T[] value, Action<string, DataElement, T> dataConverter, bool overrideVals = true)
        {
            DataElement e = new DataElement();
            e.Set("_amount", value.Length);
            for (int i = 0; i < value.Length; ++i)
            {
                string eName = "_e" + i;
                dataConverter(eName, e, value[i]);
            }

            self.Set(name, e, overrideVals);
        }

        public static void SetQuaternion(this DataElement self, string name, Quaternion value, bool overrideVals = true)
        {
            DataElement e = new DataElement();
            e.Set("x", value.X);
            e.Set("y", value.Y);
            e.Set("z", value.Z);
            e.Set("w", value.W);
            self.Set(name, e, overrideVals);
        }

        public static void SetEnum<T>(this DataElement self, string name, T value, bool overrideVals = true) where T : struct, System.Enum => self.Set<int>(name, Convert.ToInt32(value), overrideVals);

        public static Guid GetGuid(this DataElement self, string name, Guid defaultVal = default)
        {
            byte[] rDat = self.Get(name, defaultVal.ToByteArray());
            return new Guid(rDat);
        }

        public static Vector2 GetVec2(this DataElement self, string name, Vector2 defaultVal = default)
        {
            if (self.Has(name, DataType.Map))
            {
                DataElement e = self.Get<DataElement>(name);
                return new Vector2(e.Get<float>("x"), e.Get<float>("y"));
            }

            return defaultVal;
        }

        public static Vector3 GetVec3(this DataElement self, string name, Vector3 defaultVal = default)
        {
            if (self.Has(name, DataType.Map))
            {
                DataElement e = self.Get<DataElement>(name);
                return new Vector3(e.Get<float>("x"), e.Get<float>("y"), e.Get<float>("z"));
            }

            return defaultVal;
        }

        public static Vector4 GetVec4(this DataElement self, string name, Vector4 defaultVal = default)
        {
            if (self.Has(name, DataType.Map))
            {
                DataElement e = self.Get<DataElement>(name);
                return new Vector4(e.Get<float>("x"), e.Get<float>("y"), e.Get<float>("z"), e.Get<float>("w"));
            }

            return defaultVal;
        }

        public static Quaternion GetQuaternion(this DataElement self, string name, Quaternion defaultVal = default)
        {
            if (self.Has(name, DataType.Map))
            {
                DataElement e = self.Get<DataElement>(name);
                return new Quaternion(e.Get<float>("x"), e.Get<float>("y"), e.Get<float>("z"), e.Get<float>("w"));
            }

            return defaultVal;
        }

        public static Color GetColor(this DataElement self, string name, Color defaultVal = default)
        {
            uint dClr = unchecked((uint)defaultVal.ToPixel<Rgba32>().PackedValue);
            uint clr = unchecked((uint)self.Get(name, dClr));
            return new Color(new Rgba32(clr));
        }

        public static T[] GetArray<T>(this DataElement self, string name, Func<string, DataElement, T> dataConverter, T[] defaultVal = default)
        {
            if (self.Has(name, DataType.Map))
            {
                DataElement map = self.Get<DataElement>(name);
                int amt = map.Get<int>("_amount");
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

        public static T GetEnum<T>(this DataElement self, string name, T defaultVal = default) where T : struct, System.Enum
        {
            int dVal = Convert.ToInt32(defaultVal);
            int i = self.Get<int>(name, dVal);
            return (T)Enum.ToObject(typeof(T), i);
        }
    }
}
