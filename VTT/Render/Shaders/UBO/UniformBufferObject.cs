namespace VTT.Render.Shaders.UBO
{
    using System;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Util;

    public unsafe class UniformBufferObject<T> where T : unmanaged
    {
        protected readonly T* _cpuMemory;
        protected readonly GPUBuffer _buffer;

        public uint GLID => this._buffer;

        public uint BindingIndex { get; }
        public nuint BufferPhysicalSize { get; }
        public uint BufferElementCapacity { get; }

        public UniformBufferObject(uint binding, nuint elementCount = 1, BufferUsage usageHint = BufferUsage.StreamDraw)
        {
            this.BufferElementCapacity = (uint)elementCount;
            this._cpuMemory = MemoryHelper.Allocate<T>(elementCount);
            this.BufferPhysicalSize = (nuint)sizeof(T) * elementCount;
            for (nuint i = 0; i < elementCount; ++i)
            {
                *(this._cpuMemory + i) = new T();
            }

            this.BindingIndex = binding;
            this._buffer = new GPUBuffer(BufferTarget.Uniform, usageHint);
            this._buffer.Bind();
            this._buffer.SetData((IntPtr)this._cpuMemory, (int)this.BufferPhysicalSize);
            GL.BindBuffer(BufferTarget.Uniform, 0);
        }

        public void Dispose()
        {
            MemoryHelper.Free(this._cpuMemory);
            this._buffer.Dispose();
        }

        public void Upload()
        {
            this._buffer.Bind();
            this._buffer.SetSubData((IntPtr)this._cpuMemory, (int)this.BufferPhysicalSize, 0);
            GL.BindBuffer(BufferTarget.Uniform, 0);
        }

        public void BindAsUniformBuffer() => GL.BindBufferBase(BaseBufferTarget.UniformBuffer, this.BindingIndex, this._buffer);
        public void BindAsGLObject() => this._buffer.Bind();
    }
}
