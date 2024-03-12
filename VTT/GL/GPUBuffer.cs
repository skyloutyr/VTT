namespace VTT.GL
{
    using System;
    using System.Runtime.InteropServices;
    using VTT.GL.Bindings;

    public readonly struct GPUBuffer
    {
        private readonly BufferUsage _usageHint;
        private readonly BufferTarget _target;
        private readonly uint _glID;

        public GPUBuffer(BufferTarget target, BufferUsage usageHint)
        {
            this._target = target;
            this._usageHint = usageHint;
            this._glID = GL.GenBuffer();
        }

        public GPUBuffer(BufferTarget target)
        {
            this._target = target;
            this._usageHint = BufferUsage.StaticDraw;
            this._glID = GL.GenBuffer();
        }

        public GPUBuffer(BufferTarget target, uint glID)
        {
            this._target = target;
            this._usageHint = BufferUsage.StaticDraw;
            this._glID = glID;
        }

        public GPUBuffer(BufferTarget target, BufferUsage usageHint, uint glID)
        {
            this._target = target;
            this._usageHint = usageHint;
            this._glID = glID;
        }

        public static implicit operator uint(GPUBuffer self) => self._glID;
        public static implicit operator int(GPUBuffer self) => (int)self._glID;

        public void Bind() => GL.BindBuffer(this._target, this._glID);
        public void SetData<T>(T[] dat) where T : unmanaged => GL.BufferData(this._target, dat.AsSpan(), this._usageHint);
        public void SetData<T>(IntPtr dat, int size) where T : unmanaged => GL.BufferData(this._target, Marshal.SizeOf(typeof(T)) * size, dat, this._usageHint);
        public void SetData(IntPtr dat, int size) => GL.BufferData(this._target, size, dat, this._usageHint);

        public void SetSubData<T>(T[] dat, int offset) where T : unmanaged
        {
            int size = Marshal.SizeOf(typeof(T));
            GL.BufferSubData<T>(this._target, (IntPtr)(size * offset), dat.AsSpan());
        }

        public void SetSubData<T>(T[] dat, int length, int offset) where T : unmanaged
        {
            int size = Marshal.SizeOf(typeof(T));
            GCHandle hnd = GCHandle.Alloc(dat, GCHandleType.Pinned);
            GL.BufferSubData(this._target, (IntPtr)(size * offset), size * length, Marshal.UnsafeAddrOfPinnedArrayElement(dat, 0));
            hnd.Free();
        }

        public void SetSubData(IntPtr dat, int length, int offset) => GL.BufferSubData(this._target, (IntPtr)offset, length, dat);

        public void Dispose() => GL.DeleteBuffer(this._glID);
    }
}
