namespace VTT.Network.Packet
{
    using SixLabors.ImageSharp;
    using System;
    using System.IO;
    using System.Numerics;
    using VTT.Util;

    public abstract class PacketBaseWithCodec : PacketBase
    {
        public abstract void LookupData(Codec c);

        public override void Encode(BinaryWriter bw) => this.LookupData(new Codec(bw));
        public override void Decode(BinaryReader br) => this.LookupData(new Codec(br)); public class Codec
        {
            private BinaryWriter _bw;
            private BinaryReader _br;
            private bool _isWrite;

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
        }
    }
}
