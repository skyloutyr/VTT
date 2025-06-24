namespace VTT.Util
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    public unsafe class UnsafeResizeableArray<T> : IEnumerable<T> where T : unmanaged
    {
        private T* _ptr;
        private int _allocatedSize;
        private int _nextElementIndex;

        public int Length => this._nextElementIndex;
        public int Capacity => this._allocatedSize;
        public T this[int i]
        {
            get => this._ptr[i];
            set => this._ptr[i] = value;
        }

        public UnsafeResizeableArray(int amt = 1)
        {
            this._nextElementIndex = 0;
            this._allocatedSize = amt;
            this._ptr = MemoryHelper.Allocate<T>((nuint)amt);
        }

        public UnsafeResizeableArray(T[] managed)
        {
            this._nextElementIndex = managed.Length - 1;
            this._allocatedSize = managed.Length;
            this._ptr = MemoryHelper.Allocate<T>((nuint)managed.Length);
            for (int i = managed.Length - 1; i >= 0; --i)
            {
                this._ptr[i] = managed[i];
            }
        }

        public UnsafeResizeableArray(IList<T> managed)
        {
            this._nextElementIndex = managed.Count;
            this._allocatedSize = managed.Count;
            this._ptr = MemoryHelper.Allocate<T>((nuint)managed.Count);
            for (int i = managed.Count - 1; i >= 0; --i)
            {
                this._ptr[i] = managed[i];
            }
        }

        private void CheckSize(int nSz)
        {
            if (this._allocatedSize <= nSz)
            {
                int movedTo = this._allocatedSize;
                while (movedTo <= nSz)
                {
                    movedTo *= 2;
                }

                this._ptr = MemoryHelper.Reallocate(this._ptr, (nuint)movedTo);
                this._allocatedSize = movedTo;
            }
        }

        public void Reset() => this._nextElementIndex = 0;

        public void Add(T val)
        {
            this.CheckSize(this._nextElementIndex + 1);
            this[this._nextElementIndex++] = val;
        }

        public void AddRange(T[] array, int offset, int amt)
        {
            this.CheckSize(this._nextElementIndex + amt);
            for (int i = offset; i < amt + offset; ++i)
            {
                this[this._nextElementIndex++] = array[i];
            }
        }

        public T[] ToManaged()
        {
            T[] ret = new T[this._nextElementIndex];
            for (int i = 0; i < this._nextElementIndex; ++i)
            {
                ret[i] = this[i];
            }

            return ret;
        }

        public byte[] ToBytes() => this.ToBytes(0, this._nextElementIndex);

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

        public void TrimToLength()
        {
            if (this._nextElementIndex < this._allocatedSize)
            {
                T* nptr = MemoryHelper.Allocate<T>((nuint)this._nextElementIndex);
                Buffer.MemoryCopy(this._ptr, nptr, _nextElementIndex * sizeof(T), this._nextElementIndex * sizeof(T));
                this._allocatedSize = _nextElementIndex;
                MemoryHelper.Free(this._ptr);
                this._ptr = nptr;
            }
        }

        public unsafe T* GetPointer(int element = 0) => this._ptr + element;

        public void Free() => MemoryHelper.Free(this._ptr);

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < this.Length; ++i)
            {
                yield return this[i];
            }

            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
