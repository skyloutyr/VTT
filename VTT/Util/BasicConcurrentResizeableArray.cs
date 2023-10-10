namespace VTT.Util
{
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    public class BasicConcurrentResizeableArray<T> : IList<T>
    {
        private readonly ConcurrentDictionary<int, T> _kvs = new ConcurrentDictionary<int, T>();
        private int _index;
        public int Count => this._index;
        public T this[int i]
        {
            get => this._kvs[i];
            set => this._kvs[i] = value;
        }

        public bool IsReadOnly => false;

        public void Add(T value) => this._kvs.TryAdd(this._index++, value);
        public void RemoveAt(int index)
        {
            if (index == this._index - 1)
            {
                this._kvs.TryRemove(this._index - 1, out _);
                this._index -= 1;
                return;
            }

            int idxFrom = index;
            for (int i = idxFrom; i < this._index - 1; ++i)
            {
                this._kvs[i] = this._kvs[i + 1];
            }

            this._kvs.TryRemove(this._index - 1, out _);
            this._index -= 1;
        }

        public int IndexOf(T item)
        {
            foreach (KeyValuePair<int, T> t in this._kvs)
            {
                if (t.Value.Equals(item))
                {
                    return t.Key;
                }
            }

            return -1;
        }

        public void Insert(int index, T item)
        {
            for (int i = this._index; i > index; --i)
            {
                this._kvs[i] = this._kvs[i - 1];
            }

            this._kvs[index] = item;
            this._index += 1;
        }

        public void Clear() => this._kvs.Clear();
        bool ICollection<T>.Contains(T item) => this.IndexOf(item) != -1;

        public void CopyTo(T[] array, int arrayIndex) => throw new System.NotImplementedException();
        public bool Remove(T item)
        {
            int idx = this.IndexOf(item);
            if (idx != -1)
            {
                this.RemoveAt(idx);
            }

            return idx != -1;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < this._index; ++i)
            {
                yield return this._kvs[i];
            }

            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    }
}
