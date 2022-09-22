namespace VTT.GL
{
    using OpenTK.Graphics.OpenGL;
    using System;
    using System.Runtime.InteropServices;

    public class VertexArray
    {
        private readonly uint _glId;
        private int _stride;
        private int _currentLayoutIndex;
        private int _currentOffset;

        public static VertexArray Default { get; } = new VertexArray(0);

        private VertexArray(uint id) => this._glId = id;
        public VertexArray() => GL.GenVertexArrays(1, out this._glId);

        public static implicit operator uint(VertexArray self) => self._glId;

        public static implicit operator int(VertexArray self) => (int)self._glId;

        public static explicit operator VertexArray(uint id) => new VertexArray(id);

        public static explicit operator VertexArray(int id) => new VertexArray((uint)id);

        public void SetVertexSize(int size) => this._stride = size;

        public void SetVertexSize<T>(int size) where T : struct => this._stride = size * Marshal.SizeOf(typeof(T));

        public void Reset() => this._currentLayoutIndex = this._currentOffset = 0;

        public void PushElement(ElementType type, bool normalized = false)
        {
            for (int i = 0; i < type.Rows; ++i)
            {
                GL.EnableVertexAttribArray(this._currentLayoutIndex);
                if (type.Type == VertexAttribPointerType.Int || type.Type == VertexAttribPointerType.UnsignedInt || type.Type == VertexAttribPointerType.Byte || type.Type == VertexAttribPointerType.Short || type.Type == VertexAttribPointerType.UnsignedShort)
                {
                    VertexAttribIntegerType vait = (VertexAttribIntegerType)type.Type;
                    GL.VertexAttribIPointer(this._currentLayoutIndex, type.Size, vait, this._stride, ref this._currentOffset);
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

    public struct ElementType
    {
        private readonly int byteSize;

        public ElementType(int size, VertexAttribPointerType type, int byteSize)
        {
            this.Size = size;
            this.Type = type;
            this.byteSize = byteSize;
            this.Rows = 1;
        }

        public ElementType(int size, VertexAttribPointerType type, int byteSize, int rows)
        {
            this.Size = size;
            this.Type = type;
            this.byteSize = byteSize;
            this.Rows = rows;
        }

        public static ElementType SByte { get; } = new ElementType(1, VertexAttribPointerType.Byte, sizeof(byte));
        public static ElementType Byte { get; } = new ElementType(1, VertexAttribPointerType.UnsignedByte, sizeof(byte));
        public static ElementType Short { get; } = new ElementType(1, VertexAttribPointerType.Short, sizeof(short));
        public static ElementType UShort { get; } = new ElementType(1, VertexAttribPointerType.UnsignedShort, sizeof(ushort));
        public static ElementType Int { get; } = new ElementType(1, VertexAttribPointerType.Int, sizeof(int));
        public static ElementType UInt { get; } = new ElementType(1, VertexAttribPointerType.UnsignedInt, sizeof(uint));
        public static ElementType Float { get; } = new ElementType(1, VertexAttribPointerType.Float, sizeof(float));
        public static ElementType Double { get; } = new ElementType(1, VertexAttribPointerType.Double, sizeof(double));
        public static ElementType HalfFloat { get; } = new ElementType(1, VertexAttribPointerType.HalfFloat, 2);
        public static ElementType Int2101010Rev { get; } = new ElementType(1, VertexAttribPointerType.Int2101010Rev, 4);
        public static ElementType UInt2101010Rev { get; } = new ElementType(1, VertexAttribPointerType.UnsignedInt2101010Rev, 4);
        public static ElementType Vec2 { get; } = new ElementType(2, VertexAttribPointerType.Float, sizeof(float));
        public static ElementType Vec3 { get; } = new ElementType(3, VertexAttribPointerType.Float, sizeof(float));
        public static ElementType Vec4 { get; } = new ElementType(4, VertexAttribPointerType.Float, sizeof(float));
        public static ElementType Mat2 { get; } = new ElementType(2, VertexAttribPointerType.Float, sizeof(float), 2);
        public static ElementType Mat3 { get; } = new ElementType(3, VertexAttribPointerType.Float, sizeof(float), 3);
        public static ElementType Mat4 { get; } = new ElementType(4, VertexAttribPointerType.Float, sizeof(float), 4);
        public static ElementType Vec2d { get; } = new ElementType(2, VertexAttribPointerType.Double, sizeof(double));
        public static ElementType Vec3d { get; } = new ElementType(3, VertexAttribPointerType.Double, sizeof(double));
        public static ElementType Vec4d { get; } = new ElementType(4, VertexAttribPointerType.Double, sizeof(double));
        public static ElementType Mat2d { get; } = new ElementType(2, VertexAttribPointerType.Double, sizeof(double), 2);
        public static ElementType Mat3d { get; } = new ElementType(3, VertexAttribPointerType.Double, sizeof(double), 3);
        public static ElementType Mat4d { get; } = new ElementType(4, VertexAttribPointerType.Double, sizeof(double), 4);
        public static ElementType Vec2h { get; } = new ElementType(2, VertexAttribPointerType.HalfFloat, sizeof(ushort));
        public static ElementType Vec3h { get; } = new ElementType(3, VertexAttribPointerType.HalfFloat, sizeof(ushort));
        public static ElementType Vec4h { get; } = new ElementType(4, VertexAttribPointerType.HalfFloat, sizeof(ushort));

        public int Rows { get; }
        public int Size { get; }
        public int MarshalSize => this.byteSize * this.Size;
        public VertexAttribPointerType Type { get; }
    }
}
