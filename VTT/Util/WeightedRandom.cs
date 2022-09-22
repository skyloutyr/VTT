namespace VTT.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class WeightedRandom
    {
        private static readonly Random _rand = new Random();

        public static WeightedItem<T> GetWeightedItem<T>(IEnumerable<WeightedItem<T>> collection, Random rand = null)
        {
            rand = rand ?? _rand;
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
