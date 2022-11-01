namespace VTT.Util
{
    using OpenTK.Mathematics;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public class Gradient<T> : IDictionary<float, T>
    {
        private List<GradientPoint<T>> Keys { get; } = new List<GradientPoint<T>>();

        public delegate T GradientFunc(Gradient<T> grad, IList<GradientPoint<T>> collection, float a);

        ICollection<float> IDictionary<float, T>.Keys => this.Keys.Select(e => e.Key).ToArray();
        public ICollection<T> Values => this.Keys.Select(e => e.Color).ToArray();
        public int Count => this.Keys.Count;
        public bool IsReadOnly => false;

        public T this[float key]
        {
            get => this.Keys[this.GetClosest(key)].Color;
            set => this.Add(key, value);
        }

        public Gradient()
        {
        }
        public List<GradientPoint<T>> InternalList => this.Keys;

        public Gradient(Gradient<T> copyFrom) : this()
        {
            foreach (GradientPoint<T> key in copyFrom.Keys)
            {
                this.Keys.Add(new GradientPoint<T>(key.Key, key.Color));
            }
        }

        public T Interpolate(float value, GradientFunc func) => func(this, this.Keys.AsReadOnly(), value);

        public int GetClosest(float value)
        {
            for (int i = 0; i < this.Keys.Count; ++i)
            {
                GradientPoint<T> current = this.Keys[i];
                if (i == this.Keys.Count - 1 && i == 0)
                {
                    return i;
                }

                GradientPoint<T> next = this.Keys[(i + 1) % this.Keys.Count];
                if (value >= current.Key && value <= next.Key)
                {
                    return i;
                }
            }

            return this.Keys.Count - 1;
        }

        public void Add(float key, T val)
        {
            GradientPoint<T> point = new GradientPoint<T>(key, val);
            this.Keys.Add(point);
            this.Keys.Sort();
        }

        public void Add(GradientPoint<T> val)
        {
            this.Keys.Add(val);
            this.Keys.Sort();
        }

        public bool ContainsKey(float key) => this.Keys.Any(p => p.Key == key);

        public bool Remove(float key) => this.Keys.RemoveAll(p => p.Key == key) > 0;

        public bool TryGetValue(float key, out T value)
        {
            GradientPoint<T>? point = this.Keys.Cast<GradientPoint<T>?>().FirstOrDefault(p => p.HasValue && p.Value.Key == key);
            if (point != null)
            {
                value = point.Value.Color;
                return true;
            }

            value = default;
            return false;
        }

        public void Add(KeyValuePair<float, T> item) => this.Add(item.Key, item.Value);

        public void Clear() => this.Keys.Clear();

        public bool Contains(KeyValuePair<float, T> item) => this.Keys.Any(p => p.Key == item.Key && p.Color.Equals(item.Value));

        public void CopyTo(KeyValuePair<float, T>[] array, int arrayIndex)
        {
            foreach (GradientPoint<T> gp in this.Keys)
            {
                array[arrayIndex++] = new KeyValuePair<float, T>(gp.Key, gp.Color);
            }
        }

        public bool Remove(KeyValuePair<float, T> item) => this.Keys.RemoveAll(p => p.Key == item.Key && p.Color.Equals(item.Value)) > 0;

        public IEnumerator<KeyValuePair<float, T>> GetEnumerator() => this.Keys.Select(p => new KeyValuePair<float, T>(p.Key, p.Color)).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.Keys.Select(p => new KeyValuePair<float, T>(p.Key, p.Color)).GetEnumerator();

        
    }

    public static class GradientInterpolators
    {
        private static float CubicInterpolation1(float[] colors, int start, int end, float a)
        {
            float clr_start = colors[start];
            float clr_end = colors[end];
            float clr_before_start = colors[start == 0 ? 3 : start - 1];
            float clr_after_end = colors[(end + 1) % 4];
            float aSq = a * a;
            float a0 = (-0.5F * clr_before_start) + (1.5F * clr_start) - (1.5F * clr_end) + (0.5F * clr_after_end);
            float a1 = clr_before_start - (2.5F * clr_start) + (2 * clr_end) - (0.5F * clr_after_end);
            float a2 = (-0.5F * clr_before_start) + (0.5F * clr_end);
            float a3 = clr_start;
            return (a0 * a * aSq) + (a1 * aSq) + (a2 * a) + a3;
        }

        private static Vector2 CubicInterpolation2(Vector2[] colors, int start, int end, float a)
        {
            Vector2 clr_start = colors[start];
            Vector2 clr_end = colors[end];
            Vector2 clr_before_start = colors[start == 0 ? 3 : start - 1];
            Vector2 clr_after_end = colors[(end + 1) % 4];
            float aSq = a * a;
            Vector2 a0 = (-0.5F * clr_before_start) + (1.5F * clr_start) - (1.5F * clr_end) + (0.5F * clr_after_end);
            Vector2 a1 = clr_before_start - (2.5F * clr_start) + (2 * clr_end) - (0.5F * clr_after_end);
            Vector2 a2 = (-0.5F * clr_before_start) + (0.5F * clr_end);
            Vector2 a3 = clr_start;
            return (a0 * a * aSq) + (a1 * aSq) + (a2 * a) + a3;
        }

        private static Vector3 CubicInterpolation3(Vector3[] colors, int start, int end, float a)
        {
            Vector3 clr_start = colors[start];
            Vector3 clr_end = colors[end];
            Vector3 clr_before_start = colors[start == 0 ? 3 : start - 1];
            Vector3 clr_after_end = colors[(end + 1) % 4];
            float aSq = a * a;
            Vector3 a0 = (-0.5F * clr_before_start) + (1.5F * clr_start) - (1.5F * clr_end) + (0.5F * clr_after_end);
            Vector3 a1 = clr_before_start - (2.5F * clr_start) + (2 * clr_end) - (0.5F * clr_after_end);
            Vector3 a2 = (-0.5F * clr_before_start) + (0.5F * clr_end);
            Vector3 a3 = clr_start;
            return (a0 * a * aSq) + (a1 * aSq) + (a2 * a) + a3;
        }

        private static Vector4 CubicInterpolation4(Vector4[] colors, int start, int end, float a)
        {
            Vector4 clr_start = colors[start];
            Vector4 clr_end = colors[end];
            Vector4 clr_before_start = colors[start == 0 ? 3 : start - 1];
            Vector4 clr_after_end = colors[(end + 1) % 4];
            float aSq = a * a;
            Vector4 a0 = (-0.5F * clr_before_start) + (1.5F * clr_start) - (1.5F * clr_end) + (0.5F * clr_after_end);
            Vector4 a1 = clr_before_start - (2.5F * clr_start) + (2 * clr_end) - (0.5F * clr_after_end);
            Vector4 a2 = (-0.5F * clr_before_start) + (0.5F * clr_end);
            Vector4 a3 = clr_start;
            return (a0 * a * aSq) + (a1 * aSq) + (a2 * a) + a3;
        }

        public static float Lerp(Gradient<float> grad, IList<GradientPoint<float>> collection, float a)
        {
            int closestStart = grad.GetClosest(a);
            GradientPoint<float> point = collection[closestStart];
            GradientPoint<float> next = collection[(closestStart + 1) % collection.Count];
            float aRel = Math.Abs((a - point.Key) / (next.Key - point.Key));
            if (float.IsInfinity(aRel) || float.IsNaN(aRel))
            {
                aRel = 0;
            }

            return (point.Color * (1 - aRel)) + (next.Color * aRel);
        }

        public static float Cos(Gradient<float> grad, IList<GradientPoint<float>> collection, float a)
        {
            int closestStart = grad.GetClosest(a);
            GradientPoint<float> point = collection[closestStart];
            GradientPoint<float> next = collection[(closestStart + 1) % collection.Count];
            float aRelC = (float)(1 - Math.Cos(Math.Abs((a - point.Key) / (next.Key - point.Key)) * Math.PI)) / 2;
            return (point.Color * (1 - aRelC)) + (next.Color * aRelC);
        }

        public static float Cubic(Gradient<float> grad, IList<GradientPoint<float>> collection, float a)
        {
            int closestStart = grad.GetClosest(a);
            int preStart = closestStart == 0 ? collection.Count - 1 : closestStart - 1;
            int nextStart = (closestStart + 1) % collection.Count;
            int lastStart = (nextStart + 1) % collection.Count;
            GradientPoint<float> point = collection[closestStart];
            GradientPoint<float> next = collection[nextStart];
            float aRel = Math.Abs((a - point.Key) / (next.Key - point.Key));
            return CubicInterpolation1(new float[] { collection[preStart].Color, point.Color, next.Color, collection[lastStart].Color }, 1, 2, aRel);
        }

        public static Vector2 LerpVec2(Gradient<Vector2> grad, IList<GradientPoint<Vector2>> collection, float a)
        {
            int closestStart = grad.GetClosest(a);
            GradientPoint<Vector2> point = collection[closestStart];
            GradientPoint<Vector2> next = collection[(closestStart + 1) % collection.Count];
            float aRel = Math.Abs((a - point.Key) / (next.Key - point.Key));
            return (point.Color * (1 - aRel)) + (next.Color * aRel);
        }

        public static Vector2 CosVec2(Gradient<Vector2> grad, IList<GradientPoint<Vector2>> collection, float a)
        {
            int closestStart = grad.GetClosest(a);
            GradientPoint<Vector2> point = collection[closestStart];
            GradientPoint<Vector2> next = collection[(closestStart + 1) % collection.Count];
            float aRelC = (float)(1 - Math.Cos(Math.Abs((a - point.Key) / (next.Key - point.Key)) * Math.PI)) / 2;
            return (point.Color * (1 - aRelC)) + (next.Color * aRelC);
        }

        public static Vector2 CubVec2(Gradient<Vector2> grad, IList<GradientPoint<Vector2>> collection, float a)
        {
            int closestStart = grad.GetClosest(a);
            int preStart = closestStart == 0 ? collection.Count - 1 : closestStart - 1;
            int nextStart = (closestStart + 1) % collection.Count;
            int lastStart = (nextStart + 1) % collection.Count;
            GradientPoint<Vector2> point = collection[closestStart];
            GradientPoint<Vector2> next = collection[nextStart];
            float aRel = Math.Abs((a - point.Key) / (next.Key - point.Key));
            return CubicInterpolation2(new Vector2[] { collection[preStart].Color, point.Color, next.Color, collection[lastStart].Color }, 1, 2, aRel);
        }

        public static Vector3 LerpVec3(Gradient<Vector3> grad, IList<GradientPoint<Vector3>> collection, float a)
        {
            int closestStart = grad.GetClosest(a);
            GradientPoint<Vector3> point = collection[closestStart];
            GradientPoint<Vector3> next = collection[(closestStart + 1) % collection.Count];
            float aRel = Math.Abs((a - point.Key) / (next.Key - point.Key));
            return (point.Color * (1 - aRel)) + (next.Color * aRel);
        }

        public static Vector3 CosVec3(Gradient<Vector3> grad, IList<GradientPoint<Vector3>> collection, float a)
        {
            int closestStart = grad.GetClosest(a);
            GradientPoint<Vector3> point = collection[closestStart];
            GradientPoint<Vector3> next = collection[(closestStart + 1) % collection.Count];
            float aRelC = (float)(1 - Math.Cos(Math.Abs((a - point.Key) / (next.Key - point.Key)) * Math.PI)) / 2;
            return (point.Color * (1 - aRelC)) + (next.Color * aRelC);
        }

        public static Vector3 CubVec3(Gradient<Vector3> grad, IList<GradientPoint<Vector3>> collection, float a)
        {
            int closestStart = grad.GetClosest(a);
            int preStart = closestStart == 0 ? collection.Count - 1 : closestStart - 1;
            int nextStart = (closestStart + 1) % collection.Count;
            int lastStart = (nextStart + 1) % collection.Count;
            GradientPoint<Vector3> point = collection[closestStart];
            GradientPoint<Vector3> next = collection[nextStart];
            float aRel = Math.Abs((a - point.Key) / (next.Key - point.Key));
            return CubicInterpolation3(new Vector3[] { collection[preStart].Color, point.Color, next.Color, collection[lastStart].Color }, 1, 2, aRel);
        }

        public static Vector4 LerpVec4(Gradient<Vector4> grad, IList<GradientPoint<Vector4>> collection, float a)
        {
            int closestStart = grad.GetClosest(a);
            GradientPoint<Vector4> point = collection[closestStart];
            GradientPoint<Vector4> next = collection[(closestStart + 1) % collection.Count];
            float aRel = Math.Abs((a - point.Key) / (next.Key - point.Key));
            return (point.Color * (1 - aRel)) + (next.Color * aRel);
        }

        public static Vector4 CosVec4(Gradient<Vector4> grad, IList<GradientPoint<Vector4>> collection, float a)
        {
            int closestStart = grad.GetClosest(a);
            GradientPoint<Vector4> point = collection[closestStart];
            GradientPoint<Vector4> next = collection[(closestStart + 1) % collection.Count];
            float aRelC = (float)(1 - Math.Cos(Math.Abs((a - point.Key) / (next.Key - point.Key)) * Math.PI)) / 2;
            return (point.Color * (1 - aRelC)) + (next.Color * aRelC);
        }

        public static Vector4 CubVec4(Gradient<Vector4> grad, IList<GradientPoint<Vector4>> collection, float a)
        {
            int closestStart = grad.GetClosest(a);
            int preStart = closestStart == 0 ? collection.Count - 1 : closestStart - 1;
            int nextStart = (closestStart + 1) % collection.Count;
            int lastStart = (nextStart + 1) % collection.Count;
            GradientPoint<Vector4> point = collection[closestStart];
            GradientPoint<Vector4> next = collection[nextStart];
            float aRel = Math.Abs((a - point.Key) / (next.Key - point.Key));
            return CubicInterpolation4(new Vector4[] { collection[preStart].Color, point.Color, next.Color, collection[lastStart].Color }, 1, 2, aRel);
        }
    }

    public struct GradientPoint<T> : IComparable<GradientPoint<T>>
    {
        public GradientPoint(float key, T color)
        {
            this.Key = key;
            this.Color = color;
        }

        public T Color { get; }
        public float Key { get; }

        public int CompareTo(GradientPoint<T> other)
        {
            float diff = this.Key - other.Key;
            return diff < 0 ? -1 : diff > 0 ? 1 : 0;
        }
    }
}
