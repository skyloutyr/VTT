namespace VTT.Util
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    public class LinearGradient<T> : IEnumerable<LinearGradient<T>.LinearGradientPoint>
    {
        public delegate T LerpFunc(T left, T right, float a);

        private readonly LerpFunc _lerpProc;
        private readonly List<LinearGradientPoint> _pts = new List<LinearGradientPoint>();
        public int Count => this._pts.Count;

        public T this[float f]
        {
            get 
            {
                this.GetClosestPoints(f, out LinearGradientPoint left, out _);
                return left._value;
            }

            set => this.Add(f, value);
        }

        public LinearGradient(LerpFunc lerpProc) => this._lerpProc = lerpProc;
        public LinearGradient(LinearGradient<T> copy)
        {
            this._lerpProc = copy._lerpProc;
            foreach (LinearGradientPoint lgp in copy._pts)
            {
                this._pts.Add(lgp.Clone());
            }
        }

        public void FromEnumerable(IEnumerable<KeyValuePair<float, T>> enumerable)
        {
            this.Clear();
            LinearGradientPoint prev = null;
            foreach (var kv in enumerable)
            {
                this._pts.Add(prev = new LinearGradientPoint(kv.Value, kv.Key, prev == null ? kv.Key : prev.Key));
            }
        }

        public T Interpolate(float value)
        {
            this.GetClosestPoints(value, out LinearGradientPoint left, out LinearGradientPoint right);
            float a = (value - right._keyLeft) / (right._keySelf - right._keyLeft);
            if (float.IsNaN(a) || float.IsInfinity(a))
            {
                a = 0;
            }

            return this._lerpProc(left._value, right._value, a);
        }

        public bool RemoveAtKey(float key)
        {
            int i = this._pts.FindIndex(x => x._keySelf == key);
            if (i != -1)
            {
                this.RemoveInternalPointAt(i);
                return true;
            }

            return false;
        }

        public void GetClosestPoints(float value, out LinearGradientPoint left, out LinearGradientPoint right)
        {
            left = null;
            right = null;
            if (this._pts.Count < 16) // Arbitrarily use normal non-binary search if count is small
            {
                bool foundPoints = false;
                for (int i = 0; i < this._pts.Count; ++i)
                {
                    left = right;
                    right = this._pts[i];
                    if (right._keySelf < value)
                    {
                        continue;
                    }

                    if (right._keyLeft <= value)
                    {
                        left ??= (i == 0 ? right : this._pts[i - 1]);
                        foundPoints = true;
                        break;
                    }
                }

                if (!foundPoints)
                {
                    right = this._pts[^1];
                    left ??= this._pts.Count == 1 ? right : this._pts[^2];
                }
            }
            else
            {
                int low = 0;
                int high = this._pts.Count - 1;
                while (low <= high)
                {
                    int mid = low + ((high - low) / 2);
                    right = this._pts[mid];
                    if (value > right._keySelf)
                    {
                        low = mid + 1;
                        continue;
                    }

                    if (value < right._keyLeft)
                    {
                        high = mid - 1;
                        continue;
                    }

                    left = mid == 0 ? this._pts[^1] : this._pts[mid - 1];
                    break;
                }
            }
        }

        public LinearGradientPoint Add(float key, T value)
        {
            bool hadInsertion = false;
            LinearGradientPoint ret = null;
            int i;
            for (i = 0; i < this._pts.Count; ++i)
            {
                LinearGradientPoint self = this._pts[i];
                if (self._keySelf == key)
                {
                    self._value = value;
                    return self;
                }

                if (key > self._keyLeft && key < self._keySelf) // Inserted inbetween here and prev
                {
                    this._pts.Insert(i, ret = new LinearGradientPoint(value, key, self._keyLeft));
                    hadInsertion = true;
                    i += 1;
                    break;
                }
            }

            if (!hadInsertion)
            {
                this._pts.Add(ret = new LinearGradientPoint(value, key, this._pts.Count == 0 ? key : this._pts[^1]._keySelf));
            }
            else
            {
                for (;i < this._pts.Count; ++i)
                {
                    this._pts[i]._keyLeft = this._pts[i - 1]._keySelf;
                }
            }

            return ret;
        }

        public LinearGradientPoint GetInternalPointAt(int i) => this._pts[i];
        public void RemoveInternalPointAt(int i)
        {
            this._pts.RemoveAt(i);
            for (;i < this._pts.Count; ++i)
            {
                this._pts[i]._keyLeft = i == 0 ? this._pts[i]._keySelf : this._pts[i - 1]._keySelf;
            }
        }

        public void RemoveInternalPoint(LinearGradientPoint pt) => this.RemoveInternalPointAt(this._pts.IndexOf(pt));

        public void Clear() => this._pts.Clear();

        public IEnumerator<LinearGradientPoint> GetEnumerator() => this._pts.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this._pts.GetEnumerator();

        public class LinearGradientPoint
        {
            internal T _value;
            internal float _keySelf;
            internal float _keyLeft;

            public float Key => this._keySelf;
            public T Value => this._value;

            public LinearGradientPoint(T value, float keySelf, float keyLeft)
            {
                this._value = value;
                this._keySelf = keySelf;
                this._keyLeft = keyLeft;
            }

            public LinearGradientPoint Clone() => new LinearGradientPoint(this._value, this._keySelf, this._keyLeft);

            public static explicit operator KeyValuePair<float, T>(LinearGradientPoint self) => new KeyValuePair<float, T>(self._keySelf, self._value);
        }
    }
}
