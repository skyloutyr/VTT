namespace VTT.Util
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    public unsafe class UnsafeResizeableArray<T> where T : unmanaged
    {
        private T* _ptr;
        private int _count;
        private int _index;

        public int Length => this._index;
        public T this[int i]
        {
            get => this._ptr[i];
            set => this._ptr[i] = value;
        }

        public UnsafeResizeableArray(int amt = 1)
        {
            this._index = 0;
            this._count = amt;
            this._ptr = (T*)Marshal.AllocHGlobal(amt * sizeof(T));
        }

        public UnsafeResizeableArray(T[] managed)
        {
            this._index = managed.Length - 1;
            this._count = managed.Length;
            this._ptr = (T*)Marshal.AllocHGlobal((int)managed.Length * sizeof(T));
            for (int i = managed.Length - 1; i >= 0; --i)
            {
                this._ptr[i] = managed[i];
            }
        }

        public UnsafeResizeableArray(IList<T> managed)
        {
            this._index = managed.Count - 1;
            this._count = managed.Count;
            this._ptr = (T*)Marshal.AllocHGlobal((int)managed.Count * sizeof(T));
            for (int i = managed.Count - 1; i >= 0; --i)
            {
                this._ptr[i] = managed[i];
            }
        }

        private void CheckSize(int nSz)
        {
            if (this._count <= nSz)
            {
                int movedTo = this._count;
                while (true)
                {
                    movedTo *= 2;
                    if (movedTo > nSz)
                    {
                        T* nptr = (T*)Marshal.AllocHGlobal(movedTo * sizeof(T));
                        Buffer.MemoryCopy(this._ptr, nptr, movedTo * sizeof(T), this._index * sizeof(T));
                        this._count = movedTo;
                        Marshal.FreeHGlobal((IntPtr)this._ptr);
                        this._ptr = nptr;
                        break;
                    }
                }
            }
        }

        public void Reset() => this._index = 0;

        public void Add(T val)
        {
            this.CheckSize(this._index + 1);
            this[this._index++] = val;
        }

        public void AddRange(T[] array, int offset, int amt)
        {
            this.CheckSize(this._index + amt);
            for (int i = offset; i < amt + offset; ++i)
            {
                this[this._index++] = array[i];
            }
        }

        public T[] ToManaged()
        {
            T[] ret = new T[this._index];
            for (int i = 0; i < this._index; ++i)
            {
                ret[i] = this[i];
            }

            return ret;
        }

        public byte[] ToBytes() => this.ToBytes(0, this._index);

        public byte[] ToBytes(int from, int length)
        {
            byte[] ret = new byte[length * sizeof(T)];
            byte* sptr = (byte*)this._ptr;
            for (int i = 0; i < length; ++i)
            {
                ret[i] = sptr[from + i];
            }

            return ret;
        }

        public unsafe T* GetPointer(int element = 0) => this._ptr + element;

        public void Free() => Marshal.FreeHGlobal((IntPtr)this._ptr);
    }
}
