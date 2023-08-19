namespace VTT.GL
{
    using OpenTK.Mathematics;
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;

    public unsafe class UBOHelper
    {
        private int _cntr;
        private readonly int _dLen;
        private readonly byte* _dataView;
        private readonly GPUBuffer _buf;

        public UBOHelper(OpenTK.Graphics.OpenGL.BufferUsageHint usage, params ElementType[] elements)
        {
            int bSize = elements.Select(e => e.MachineSize).Sum();
            this._dataView = (byte*)Marshal.AllocHGlobal(bSize);
            this._dLen = bSize;
            this._buf = new GPUBuffer(OpenTK.Graphics.OpenGL.BufferTarget.ArrayBuffer, usage);
            this._buf.Bind();
            this._buf.SetData(IntPtr.Zero, bSize);
        }

        ~UBOHelper()
        {
            Marshal.FreeHGlobal((IntPtr)this._dataView);
            this._buf.Dispose();
        }

        public void Reset() => this._cntr = 0;

        private void Push(void* data, int bSize)
        {
            byte* dB = (byte*)data;
            for (int i = 0; i < bSize; ++i)
            {
                this._dataView[this._cntr++] = dB[i];
            }
        }

        public void Push(float f) => this.Push(&f, sizeof(float)); // No pad
        public void Push(int f) => this.Push(&f, sizeof(float)); // No pad
        public void Push(uint f) => this.Push(&f, sizeof(float)); // No pad
        public void Push(byte f) => this.Push((uint)f); // N base size is 4
        public void Push(bool f) => this.Push(f ? 1 : 0); // N base size is 4, don't care signed/unsigned
        public void Push(Vector2 f) // OpenTK does this internally
=> this.Push(&f.X, sizeof(Vector2));

        public void Push(Vector3 f) // OpenTK does this internally
        {
            this.Push(&f.X, sizeof(Vector3));
            this.Push(IntPtr.Zero.ToPointer(), sizeof(float)); // Pad
        }

        public void Push(Vector4 f) => this.Push(&f.X, sizeof(Vector4));

        public void Push(Matrix4 f) => this.Push(&f.Row0.X, sizeof(Matrix4));

        public void Bind(int bindingPoint)
        {
            this.Reset();
            OpenTK.Graphics.OpenGL.GL.BindBufferBase(OpenTK.Graphics.OpenGL.BufferRangeTarget.UniformBuffer, bindingPoint, this._buf);
        }

        public void Upload() => this._buf.SetSubData((IntPtr)this._dataView, this._cntr, 0);
    }
}
