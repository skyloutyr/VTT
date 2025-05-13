namespace VTT.Network.Packet
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Numerics;
    using VTT.Util;

    public abstract class PacketBaseWithCodec : PacketBase
    {
        public abstract void LookupData(Codec c);

        public override void Encode(BinaryWriter bw) => this.LookupData(new Codec(bw));
        public override void Decode(BinaryReader br) => this.LookupData(new Codec(br)); 
        
        public class Codec
        {
            private readonly BinaryWriter _bw;
            private readonly BinaryReader _br;
            private readonly bool _isWrite;

            public Codec(BinaryReader br)
            {
                this._br = br;
                this._isWrite = false;
            }

            public Codec(BinaryWriter bw)
            {
                this._bw = bw;
                this._isWrite = true;
            }

            public void Lookup(ref bool value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadBoolean();
                }
            }

            public bool Lookup(bool value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref sbyte value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadSByte();
                }
            }

            public sbyte Lookup(sbyte value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref byte value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadByte();
                }
            }

            public byte Lookup(byte value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup<T>(ref T[] array) where T : unmanaged
            {
                int amt = this.Lookup(array?.Length ?? 0);
                if (amt > 0)
                {
                    if (this._isWrite)
                    {
                        unsafe
                        {
                            fixed (T* tptr = array)
                            {
                                Span<byte> bspan = new Span<byte>(tptr, amt * sizeof(T));
                                this._bw.Write(bspan);
                            }
                        }
                    }
                    else
                    {
                        if ((array?.Length ?? 0) != amt)
                        {
                            array = new T[amt];
                        }

                        unsafe
                        {
                            fixed (T* tptr = array)
                            {
                                int nRead = 0;
                                int needed = amt * sizeof(T);
                                Span<byte> bspan = new Span<byte>(tptr, needed); // This being a regular int is iffy but a Span<T> only accepts an int
                                while (true)
                                {
                                    nRead += this._br.Read(bspan[nRead..]);
                                    if (nRead >= needed)
                                    {
                                        break;
                                    }
                                }

                                if (nRead > needed) // Sanity check
                                {
                                    throw new OverflowException("Read more bytes than expected!");
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (!this._isWrite)
                    {
                        array = Array.Empty<T>();
                    }
                }
            }

            public T[] Lookup<T>(T[] array) where T : unmanaged
            {
                this.Lookup(ref array);
                return array;
            }

            public void Lookup(ref short value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadInt16();
                }
            }

            public short Lookup(short value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref ushort value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadUInt16();
                }
            }

            public ushort Lookup(ushort value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref int value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadInt32();
                }
            }

            public int Lookup(int value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref uint value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadUInt32();
                }
            }

            public uint Lookup(uint value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref long value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadInt64();
                }
            }

            public long Lookup(long value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref ulong value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadUInt64();
                }
            }

            public ulong Lookup(ulong value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref float value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadSingle();
                }
            }

            public float Lookup(float value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref double value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadDouble();
                }
            }

            public double Lookup(double value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref string value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadString();
                }
            }

            public string Lookup(string value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref Guid value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadGuid();
                }
            }

            public Guid Lookup(Guid value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref Guid[] array)
            {
                int amt = this.Lookup(array?.Length ?? 0);
                if (amt > 0)
                {
                    if (this._isWrite)
                    {
                        for (int i = 0; i < amt; ++i)
                        {
                            this._bw.Write(array[i]);
                        }
                    }
                    else
                    {
                        array = array?.Length == amt ? array : new Guid[amt];
                        for (int i = 0; i < amt; ++i)
                        {
                            array[i] = this._br.ReadGuid();
                        }
                    }
                }
                else
                {
                    if (!this._isWrite)
                    {
                        array = Array.Empty<Guid>();
                    }
                }
            }

            public Guid[] Lookup(Guid[] array)
            {
                this.Lookup(ref array);
                return array;
            }

            public void Lookup<T>(ref T value) where T : struct, Enum
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadEnum<T>();
                }
            }

            public T Lookup<T>(T value) where T : struct, Enum
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref Vector2 value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadVec2();
                }
            }

            public Vector2 Lookup(Vector2 value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref Vector3 value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadVec3();
                }
            }

            public Vector3 Lookup(Vector3 value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref Vector4 value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadVec4();
                }
            }

            public Vector4 Lookup(Vector4 value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref Color value)
            {
                if (this._isWrite)
                {
                    this._bw.Write(value);
                }
                else
                {
                    value = this._br.ReadColor();
                }
            }

            public Color Lookup(Color value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ref DataElement value)
            {
                if (this._isWrite)
                {
                    value.Write(this._bw);
                }
                else
                {
                    value = new DataElement(this._br);
                }
            }

            public DataElement Lookup(DataElement value)
            {
                this.Lookup(ref value);
                return value;
            }

            public void Lookup(ISerializable serializable)
            {
                if (this._isWrite)
                {
                    this.Lookup(serializable.Serialize());
                }
                else
                {
                    serializable.Deserialize(this.Lookup(new DataElement()));
                }
            }

            public void Lookup(ICustomNetworkHandler icnh)
            {
                if (this._isWrite)
                {
                    icnh.Write(this._bw);
                }
                else
                {
                    icnh.Read(this._br);
                }
            }

            public void Lookup<T>(ref List<T> list, Func<T, T> serializer)
            {
                int cnt = this.Lookup(list?.Count ?? 0);
                if (this._isWrite)
                {
                    if (cnt != 0)
                    {
                        foreach (T item in list)
                        {
                            serializer(item);
                        }
                    }
                }
                else
                {
                    if (list != null)
                    {
                        list.Clear();
                    }
                    else
                    {
                        list = new List<T>();
                    }

                    for (int i = 0; i < cnt; ++i)
                    {
                        list.Add(serializer(default));
                    }
                }
            }

            public List<T> Lookup<T>(List<T> list, Func<T, T> serializer)
            {
                this.Lookup(ref list, serializer); 
                return list;
            }

            public void Lookup<T>(ref T[] array, Func<T, T> serializer)
            {
                int cnt = this.Lookup(array?.Length ?? 0);
                if (this._isWrite)
                {
                    if (cnt != 0)
                    {
                        for (int i = 0; i < array.Length; i++)
                        {
                            T item = array[i];
                            serializer(item);
                        }
                    }
                }
                else
                {
                    if (array?.Length != cnt)
                    {
                        array = new T[cnt];
                    }

                    for (int i = 0; i < cnt; ++i)
                    {
                        array[i] = serializer(default);
                    }
                }
            }

            public T[] Lookup<T>(T[] array, Func<T, T> serializer)
            {
                this.Lookup(ref array, serializer);
                return array;
            }

            public void LookupBox<P>(ref object box, Func<P, P> boxer) where P : notnull
            {
                P p = box != null ? (P)box : default;
                p = boxer(p);
                box = p;
            }

            public object LookupBox<P>(object box, Func<P, P> boxer) where P : notnull
            {
                this.LookupBox(ref box, boxer);
                return box;
            }

            public void Lookup(ref Image img)
            {
                if (this._isWrite)
                {
                    using MemoryStream ms = new MemoryStream();
                    img.SaveAsPng(ms);
                    this._bw.Write((int)ms.Length);
                    this._bw.Write(ms.ToArray());
                }
                else
                {
                    byte[] arr = this._br.ReadBytes(this._br.ReadInt32());
                    using MemoryStream ms = new MemoryStream(arr);
                    img = Image.Load(ms);
                }
            }

            public Image Lookup(Image img)
            {
                this.Lookup(ref img);
                return img;
            }

            public void Lookup<TPixel>(ref Image<TPixel> img) where TPixel : unmanaged, IPixel<TPixel>
            {
                if (this._isWrite)
                {
                    if (img == null)
                    {
                        this._bw.Write(0);
                    }
                    else
                    {
                        using MemoryStream ms = new MemoryStream();
                        img.SaveAsPng(ms);
                        this._bw.Write((int)ms.Length);
                        this._bw.Write(ms.ToArray());
                    }
                }
                else
                {
                    int l = this._br.ReadInt32();
                    if (l == 0)
                    {
                        img = null;
                    }
                    else
                    {
                        byte[] arr = this._br.ReadBytes(l);
                        using MemoryStream ms = new MemoryStream(arr);
                        img = Image.Load<TPixel>(ms);
                    }
                }
            }

            public Image<TPixel> Lookup<TPixel>(Image<TPixel> img) where TPixel : unmanaged, IPixel<TPixel>
            {
                this.Lookup(ref img);
                return img;
            }
        }
    }
}
