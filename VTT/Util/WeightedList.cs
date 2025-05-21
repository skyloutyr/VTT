namespace VTT.Util
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    public class WeightedList<T> : IList<WeightedItem<T>>
    {
        internal static readonly Random rand = new Random();

        private readonly List<WeightedItem<T>> _list = new List<WeightedItem<T>>();
        private int _totalWeight;

        public int TotalWeight => this._totalWeight;

        public int Count => this._list.Count;

        public bool IsReadOnly => false;

        public WeightedItem<T> this[int index] 
        { 
            get => this._list[index]; 
            set
            {
                WeightedItem<T> now = this._list[index];
                int wDelta = now.Weight - value.Weight;
                if (wDelta != 0)
                {
                    this.UpdateRightElements(index + 1, wDelta);
                }

                this._totalWeight += wDelta;
                this._list[index] = value;
            }
        }

        public WeightedList()
        {
        }

        public WeightedList(WeightedList<T> copyFrom) => this.FullCopyFrom(copyFrom);

        public void FullCopyFrom(WeightedList<T> copyFrom)
        {
            this.Clear();
            foreach (WeightedItem<T> itm in copyFrom)
            {
                this._list.Add(itm.Clone());
            }
        }

        public void Add(WeightedItem<T> item)
        {
            item.localCumulativeWeight = this._totalWeight + item.Weight;
            this._list.Add(item);
            this._totalWeight += item.Weight;
        }

        public bool Remove(WeightedItem<T> item)
        {
            int i = this._list.IndexOf(item);
            if (i != -1)
            {
                this.UpdateRightElements(i + 1, -item.Weight);
                this._list.Remove(item);
                this._totalWeight -= item.Weight;
                return true;
            }

            return false;
        }

        public WeightedItem<T> GetRandomItem(Random rand = null)
        {
            rand ??= WeightedList<T>.rand;
            int weight = rand.Next(this.TotalWeight) + 1;
            if (this.Count < 16) // Arbitrary, for small collections a quick check may be faster
            {
                for (int i = 0; i < this.Count; ++i)
                {
                    WeightedItem<T> itm = this._list[i];
                    if (weight <= itm.localCumulativeWeight)
                    {
                        return itm;
                    }
                }

                return this[^1];
            }
            else // Use binary search
            {
                int low = 0;
                int high = this._list.Count - 1;
                while (low <= high)
                {
                    int mid = low + ((high - low) / 2);
                    WeightedItem<T> itm = this._list[mid];
                    int wEnd = itm.localCumulativeWeight;
                    if (weight > wEnd)
                    {
                        low = mid + 1;
                        continue;
                    }

                    if (weight < wEnd - itm.Weight)
                    {
                        high = mid - 1;
                        continue;
                    }

                    return itm;
                }

                return this[^1];
            }
        }

        public int IndexOf(WeightedItem<T> item) => this._list.IndexOf(item);
        public void Insert(int index, WeightedItem<T> item)
        {
            if (index < this.Count - 1)
            {
                WeightedItem<T> itmHere = this[index];
                item.localCumulativeWeight = itmHere.localCumulativeWeight - itmHere.Weight + item.Weight;
                this.UpdateRightElements(index + 1, item.Weight);
            }
            else
            {
                item.localCumulativeWeight = this._totalWeight + item.Weight;
            }

            this._list.Insert(index, item);
            this._totalWeight += item.Weight;
        }

        public void RemoveAt(int index)
        {
            if (index >= 0 && index < this.Count)
            {
                WeightedItem<T> itm = this._list[index];
                this.UpdateRightElements(index, -itm.Weight);
                this._list.RemoveAt(index);
                this._totalWeight -= itm.Weight;
            }
        }

        public void Clear()
        {
            this._list.Clear();
            this._totalWeight = 0;
        }

        public bool Contains(WeightedItem<T> item) => this._list.Contains(item);
        public void CopyTo(WeightedItem<T>[] array, int arrayIndex) => this._list.CopyTo(array, arrayIndex);
        public IEnumerator<WeightedItem<T>> GetEnumerator() => this._list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        private void UpdateRightElements(int at, int added)
        {
            if (at >= this.Count - 1)
            {
                return;
            }

            Span<WeightedItem<T>> items = CollectionsMarshal.AsSpan(this._list);
            for (int i = at + 1; i < this.Count; ++i)
            {
                items[i].localCumulativeWeight += added;
            }
        }
    }

    public struct WeightedItem<T>
    {
        public T Item { get; }
        public int Weight { get; }
        internal int localCumulativeWeight;

        public WeightedItem(T t, int weight)
        {
            this.Item = t;
            this.Weight = weight;
            this.localCumulativeWeight = weight;
        }

        public readonly WeightedItem<T> Clone() => new WeightedItem<T>(this.Item, this.Weight) { localCumulativeWeight = this.localCumulativeWeight };
    }
}
