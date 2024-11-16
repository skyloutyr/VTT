namespace VTT.GL
{
    using VTT.GL.Bindings;
    using GL = Bindings.GL;
    using System.Runtime.InteropServices;

    public class VertexArray
    {
        private readonly uint _glId;
        private int _stride;
        private uint _currentLayoutIndex;
        private int _currentOffset;

        public static VertexArray Default { get; } = new VertexArray(0);

        private VertexArray(uint id) => this._glId = id;
        public VertexArray() => this._glId = GL.GenVertexArray();

        public static implicit operator uint(VertexArray self) => self._glId;

        public static implicit operator int(VertexArray self) => (int)self._glId;

        public static explicit operator VertexArray(uint id) => new VertexArray(id);

        public static explicit operator VertexArray(int id) => new VertexArray((uint)id);

        public void SetVertexSize(int size) => this._stride = size;

        public void SetVertexSize<T>(int size) where T : struct => this._stride = size * Marshal.SizeOf(typeof(T));

        public void Reset()
        {
            this._currentLayoutIndex = 0;
            this._currentOffset = 0;
        }

        public void PushElement(ElementType type, bool normalized = false)
        {
            for (int i = 0; i < type.Rows; ++i)
            {
                GL.EnableVertexAttribArray(this._currentLayoutIndex);
                if (type.Type is VertexAttributeType.Int or VertexAttributeType.UnsignedInt or VertexAttributeType.Short or VertexAttributeType.UnsignedShort)
                {
                    VertexAttributeIntegerType vait = (VertexAttributeIntegerType)type.Type;
                    GL.VertexAttribIPointer(this._currentLayoutIndex, type.Size, vait, this._stride, this._currentOffset);
                }
                else
                {
                    GL.VertexAttribPointer(this._currentLayoutIndex, type.Size, type.Type, normalized, this._stride, this._currentOffset);
                }

                this._currentOffset += type.MarshalSize;
                ++this._currentLayoutIndex;
            }
        }

        public void Bind() => GL.BindVertexArray(this._glId);
        public void Dispose() => GL.DeleteVertexArray(this._glId);
    }

    public readonly struct ElementType
    {
        private readonly int byteSize;

        public ElementType(int size, int std140machineSize, VertexAttributeType type, int byteSize)
        {
            this.Size = size;
            this.MachineSize = std140machineSize;
            this.Type = type;
            this.byteSize = byteSize;
            this.Rows = 1;
        }

        public ElementType(int size, int std140machineSize, VertexAttributeType type, int byteSize, int rows)
        {
            this.Size = size;
            this.MachineSize = std140machineSize;
            this.Type = type;
            this.byteSize = byteSize;
            this.Rows = rows;
        }

        public static ElementType SByte { get; } = new ElementType(1, 4, VertexAttributeType.SignedByte, sizeof(byte));
        public static ElementType Byte { get; } = new ElementType(1, 4, VertexAttributeType.Byte, sizeof(byte));
        public static ElementType Short { get; } = new ElementType(1, 4, VertexAttributeType.Short, sizeof(short));
        public static ElementType UShort { get; } = new ElementType(1, 4, VertexAttributeType.UnsignedShort, sizeof(ushort));
        public static ElementType Int { get; } = new ElementType(1, 4, VertexAttributeType.Int, sizeof(int));
        public static ElementType UInt { get; } = new ElementType(1, 4, VertexAttributeType.UnsignedInt, sizeof(uint));
        public static ElementType Float { get; } = new ElementType(1, 4, VertexAttributeType.Float, sizeof(float));
        public static ElementType Double { get; } = new ElementType(1, 8, VertexAttributeType.Double, sizeof(double));
        public static ElementType HalfFloat { get; } = new ElementType(1, 4, VertexAttributeType.Half, 2);
        public static ElementType Int2101010Rev { get; } = new ElementType(1, 4, VertexAttributeType.Int2101010Rev, 4);
        public static ElementType UInt2101010Rev { get; } = new ElementType(1, 4, VertexAttributeType.UnsignedInt2101010Rev, 4);
        public static ElementType Vec2 { get; } = new ElementType(2, 8, VertexAttributeType.Float, sizeof(float));
        public static ElementType Vec3 { get; } = new ElementType(3, 16, VertexAttributeType.Float, sizeof(float));
        public static ElementType Vec4 { get; } = new ElementType(4, 16, VertexAttributeType.Float, sizeof(float));
        public static ElementType Mat2 { get; } = new ElementType(2, 32, VertexAttributeType.Float, sizeof(float), 2);
        public static ElementType Mat3 { get; } = new ElementType(3, 64, VertexAttributeType.Float, sizeof(float), 3);
        public static ElementType Mat4 { get; } = new ElementType(4, 64, VertexAttributeType.Float, sizeof(float), 4);
        public static ElementType Vec2d { get; } = new ElementType(2, 16, VertexAttributeType.Double, sizeof(double));
        public static ElementType Vec3d { get; } = new ElementType(3, 32, VertexAttributeType.Double, sizeof(double));
        public static ElementType Vec4d { get; } = new ElementType(4, 32, VertexAttributeType.Double, sizeof(double));
        public static ElementType Mat2d { get; } = new ElementType(2, 64, VertexAttributeType.Double, sizeof(double), 2);
        public static ElementType Mat3d { get; } = new ElementType(3, 128, VertexAttributeType.Double, sizeof(double), 3);
        public static ElementType Mat4d { get; } = new ElementType(4, 128, VertexAttributeType.Double, sizeof(double), 4);
        public static ElementType Vec2h { get; } = new ElementType(2, 8, VertexAttributeType.Half, sizeof(ushort));
        public static ElementType Vec3h { get; } = new ElementType(3, 16, VertexAttributeType.Half, sizeof(ushort));
        public static ElementType Vec4h { get; } = new ElementType(4, 16, VertexAttributeType.Half, sizeof(ushort));

        public int Rows { get; }
        public int Size { get; }
        public int MachineSize { get; }
        public int MarshalSize => this.byteSize * this.Size;
        public VertexAttributeType Type { get; }
    }
}
