namespace VTT.Util
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public class WeightedRandom
    {
        internal static readonly Random rand = new Random();

        public static WeightedItem<T> GetWeightedItem<T>(IEnumerable<WeightedItem<T>> collection, Random rand = null)
        {
            rand = rand ?? WeightedRandom.rand;
            int weight = rand.Next(collection.Sum(w => w.Weight)) + 1;
            Queue<WeightedItem<T>> q = new Queue<WeightedItem<T>>(collection);
            WeightedItem<T> wi = q.Dequeue();
            while (true)
            {
                weight -= wi.Weight;
                if (weight > 0)
                {
                    wi = q.Dequeue();
                }
                else
                {
                    break;
                }
            }

            return wi;
        }

        public static WeightedItem<T> GetWeightedItem<T>(WeightedList<T> collection, Random rand)
        {
            rand = rand ?? WeightedRandom.rand;
            int weight = rand.Next(collection.TotalWeight) + 1;
            int idx = 0;
            WeightedItem<T> wi = collection[idx];
            while (true)
            {
                weight -= wi.Weight;
                if (weight > 0)
                {
                    wi = collection[++idx];
                }
                else
                {
                    break;
                }
            }

            return wi;
        }
    }

    public class WeightedList<T> : IList<WeightedItem<T>>
    {
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
                this._totalWeight -= this._list[index].Weight;
                this._totalWeight += this._list[index].Weight;
                this._list[index] = value;
            }
        }

        public void Add(WeightedItem<T> item)
        {
            this._list.Add(item);
            this._totalWeight += item.Weight;
        }

        public bool Remove(WeightedItem<T> item)
        {
            if (this._list.Remove(item))
            {
                this._totalWeight -= item.Weight;
                return true;
            }

            return false;
        }

        public WeightedItem<T> GetRandomItem(Random rand = null)
        {
            rand ??= WeightedRandom.rand;
            int weight = rand.Next(this.TotalWeight) + 1;
            for (int i = 0; i < this.Count; ++i)
            {
                if (weight <= this[i].Weight)
                {
                    return this[i];
                }

                weight -= this[i].Weight;
            }

            return this[^1];
        }

        public int IndexOf(WeightedItem<T> item) => this._list.IndexOf(item);
        public void Insert(int index, WeightedItem<T> item)
        {
            this._list.Insert(index, item);
            this._totalWeight += item.Weight;
        }

        public void RemoveAt(int index)
        {
            this._totalWeight -= this._list[index].Weight;
            this._list.RemoveAt(index);
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
    }

    public readonly struct WeightedItem<T>
    {
        public T Item { get; }
        public int Weight { get; }

        public WeightedItem(T t, int weight)
        {
            this.Item = t;
            this.Weight = weight;
        }
    }
}
