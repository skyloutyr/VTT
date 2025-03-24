namespace VTT.Util
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    public sealed class NonClearingArrayList<T> : IList<T>
    {
        private T[] _array;
        private int _capacity;
        private int _index;

        public NonClearingArrayList(int nElements = 1)
        {
            this._array = new T[nElements];
            this._capacity = nElements;
            this._index = 0;
        }

        public T this[int index]
        {
            get => this._array[index];
            set => this._array[index] = value;
        }

        public int Count => this._index;
        public bool IsReadOnly => false;

        private void CheckCapacity(int needed)
        {
            bool needResize = false;
            if (this._capacity == 0)
            {
                this._capacity = 1;
                needResize = true;
            }

            while (needed > this._capacity)
            {
                needResize = true;
                this._capacity *= 2;
            }

            if (needResize)
            {
                Array.Resize(ref this._array, this._capacity);
            }
        }

        public void Add(T item)
        {
            this.CheckCapacity(this._index + 1);
            this._array[this._index++] = item;
        }

        public void Clear() => this._index = 0;
        public void FullClear()
        {
            this._index = 0;
            Array.Clear(this._array);
        }

        public bool Contains(T item) => this.IndexOf(item) != -1;

        public void CopyTo(T[] array, int arrayIndex)
        {
            for (int i = 0; i < this._index; ++i)
            {
                array[arrayIndex + i] = this._array[i];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < this._index; ++i)
            {
                yield return this._array[i];
            }

            yield break;
        }

        public int IndexOf(T item)
        {
            for (int i = 0; i < this._index; ++i)
            {
                if (Equals(this._array[i], item))
                {
                    return i;
                }
            }

            return -1;
        }

        public void Insert(int index, T item)
        {
            this.CheckCapacity(this._index + 1);
            if (index < this._index)
            {
                Array.Copy(this._array, index, this._array, index + 1, this._index - index);
            }
            else
            {
                this.Add(item);
            }

            this._array[index] = item;
            this._index += 1;
        }

        public bool Remove(T item)
        {
            int idx = this.IndexOf(item);
            if (idx == -1)
            {
                return false;
            }

            this.RemoveAt(idx);
            return true;
        }

        public void RemoveAt(int index)
        {
            for (int i = index + 1; i < this._index; ++i)
            {
                this._array[i - 1] = this._array[i];
            }

            this._index -= 1;
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
