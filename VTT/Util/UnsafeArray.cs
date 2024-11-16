namespace VTT.Util
{
    using System.Collections;
    using System.Collections.Generic;

    public unsafe class UnsafeArray<T> : IEnumerable<T> where T : unmanaged
    {
        private readonly T* _ptr;
        private readonly int _amt;

        public int Length => this._amt;
        public T this[int i]
        {
            get => this._ptr[i];
            set => this._ptr[i] = value;
        }

        public UnsafeArray(int amt = 1)
        {
            this._amt = amt;
            this._ptr = MemoryHelper.Allocate<T>((nuint)amt);
        }

        public UnsafeArray(T[] managed)
        {
            this._amt = managed.Length;
            this._ptr = MemoryHelper.Allocate<T>((nuint)managed.Length);
            for (int i = managed.Length - 1; i >= 0; --i)
            {
                this._ptr[i] = managed[i];
            }
        }

        public UnsafeArray(IList<T> managed)
        {
            this._amt = managed.Count;
            this._ptr = MemoryHelper.Allocate<T>((nuint)managed.Count);
            for (int i = managed.Count - 1; i >= 0; --i)
            {
                this._ptr[i] = managed[i];
            }
        }


        public T[] ToManaged()
        {
            T[] ret = new T[this._amt];
            for (int i = 0; i < this._amt; ++i)
            {
                ret[i] = this[i];
            }

            return ret;
        }

        public byte[] ToBytes() => this.ToBytes(0, this._amt);

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
