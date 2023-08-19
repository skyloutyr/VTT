namespace VTT.GL
{
    using OpenTK.Graphics.OpenGL;
    using System;
    using System.Runtime.InteropServices;

    public readonly struct GPUBuffer
    {
        private readonly BufferUsageHint _usageHint;
        private readonly BufferTarget _target;
        private readonly uint _glID;

        public GPUBuffer(BufferTarget target, BufferUsageHint usageHint)
        {
            this._target = target;
            this._usageHint = usageHint;
            GL.GenBuffers(1, out this._glID);
        }

        public GPUBuffer(BufferTarget target)
        {
            this._target = target;
            this._usageHint = BufferUsageHint.StaticDraw;
            GL.GenBuffers(1, out this._glID);
        }

        public GPUBuffer(BufferTarget target, uint glID)
        {
            this._target = target;
            this._usageHint = BufferUsageHint.StaticDraw;
            this._glID = glID;
        }

        public GPUBuffer(BufferTarget target, BufferUsageHint usageHint, uint glID)
        {
            this._target = target;
            this._usageHint = usageHint;
            this._glID = glID;
        }

        public static implicit operator uint(GPUBuffer self) => self._glID;
        public static implicit operator int(GPUBuffer self) => (int)self._glID;

        public void Bind() => GL.BindBuffer(this._target, this._glID);
        public void SetData<T>(T[] dat) where T : struct => GL.BufferData(this._target, Marshal.SizeOf(typeof(T)) * dat.Length, dat, this._usageHint);
        public void SetData<T>(IntPtr dat, int size) where T : struct => GL.BufferData(this._target, Marshal.SizeOf(typeof(T)) * size, dat, this._usageHint);
        public void SetData(IntPtr dat, int size) => GL.BufferData(this._target, size, dat, this._usageHint);

        public void SetSubData<T>(T[] dat, int offset) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            GL.BufferSubData(this._target, (IntPtr)(size * offset), size * dat.Length, dat);
        }

        public void SetSubData<T>(T[] dat, int length, int offset) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            GL.BufferSubData(this._target, (IntPtr)(size * offset), size * length, dat);
        }

        public void SetSubData(IntPtr dat, int length, int offset) => GL.BufferSubData(this._target, (IntPtr)offset, length, dat);

        public void GetSubData<T>(T[] dat, int offset) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            GL.GetBufferSubData(this._target, (IntPtr)(offset * size), dat.Length * size, dat);
        }

        public void GetSubData<T>(T[] dat, int length, int offset) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            GL.GetBufferSubData(this._target, (IntPtr)(offset * size), length * size, dat);
        }

        public void Dispose() => GL.DeleteBuffer(this._glID);
    }
}
