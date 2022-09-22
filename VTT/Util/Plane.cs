namespace VTT.Util
{
    using OpenTK.Mathematics;
    using System;

    public readonly struct Plane
    {
        public Vector3 Normal { get; }
        public float Distance { get; }

        public Plane(float x, float y, float z, float d)
        {
            this.Normal = new Vector3(x, y, z);
            this.Distance = d;
        }

        public Plane(Vector3 normal, float d)
        {
            this.Normal = normal;
            this.Distance = d;
        }

        public Plane Normalized() => new Plane(this.Normal.Normalized(), this.Distance / this.Normal.Length);
        public float DotProduct(Vector3 vec) => Vector3.Dot(this.Normal, vec) + this.Distance;

        public Vector3? Intersect(Ray r, Vector3 planeCenter)
        {
            float denom = Vector3.Dot(this.Normal, r.Direction);
            if (MathF.Abs(denom) <= 1e-7)
            {
                return null;
            }

            float t = Vector3.Dot(planeCenter - r.Origin, Normal) / denom;
            if (t <= float.Epsilon)
            {
                return null;
            }

            return r.Origin + t * r.Direction;
        }
    }
}
