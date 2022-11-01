namespace VTT.Util
{
    using OpenTK.Mathematics;
    using System;

    public struct AABox : IEquatable<AABox>
    {
        public Vector3 Start { get; set; }
        public Vector3 End { get; set; }

        public Vector3 Center => this.Start + ((this.End - this.Start) * 0.5f);

        public Vector3 Size => this.End - this.Start;
        public Vector3[] Mesh => new Vector3[] {
                    new Vector3(this.Start.X, this.Start.Y, this.Start.Z),
                    new Vector3(this.End.X, this.Start.Y, this.Start.Z),
                    new Vector3(this.End.X, this.End.Y, this.Start.Z),
                    new Vector3(this.Start.X, this.End.Y, this.Start.Z),
                    new Vector3(this.Start.X, this.Start.Y, this.End.Z),
                    new Vector3(this.End.X, this.Start.Y, this.End.Z),
                    new Vector3(this.End.X, this.End.Y, this.End.Z),
                    new Vector3(this.Start.X, this.End.Y, this.End.Z)
                };


        public AABox(float sx, float sy, float sz, float ex, float ey, float ez) : this(new Vector3(sx, sy, sz), new Vector3(ex, ey, ez))
        {
        }

        public AABox(Vector3 start, Vector3 end) : this()
        {
            this.Start = new Vector3(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Min(start.Z, end.Z));
            this.End = new Vector3(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y), Math.Max(start.Z, end.Z));
        }

        public static AABox FromPositionAndSize(Vector3 pos, Vector3 size) => new AABox(pos, pos + size);

        public AABox Offset(Vector3 by) => new AABox(this.Start + by, this.End + by);

        public AABox Scale(float by) => this.Scale(new Vector3(by));
        public AABox Scale(Vector3 by) => new AABox(this.Start * by, this.End * by);
        public AABox Inflate(Vector3 by) => new AABox(this.Start - (by / 2), this.End + (by / 2));
        public AABox Union(AABox other) => new AABox(
            MathF.Min(this.Start.X, other.Start.X),
            MathF.Min(this.Start.Y, other.Start.Y),
            MathF.Min(this.Start.Z, other.Start.Z),
            MathF.Max(this.End.X, other.End.X),
            MathF.Max(this.End.Y, other.End.Y),
            MathF.Max(this.End.Z, other.End.Z)
        );

        public AABox Transform(Matrix3 by) => new AABox(this.Start * by, this.End * by);

        public AABox Transform(Matrix4 by)
        {
            Vector4 start = new Vector4(this.Start, 1);
            Vector4 end = new Vector4(this.End, 1);
            start *= by;
            end *= by;
            return new AABox(start.Xyz, end.Xyz);
        }

        public bool IntersectsSphere(Vector3 center, float radius)
        {
            static float Sqr(float f) => f * f;

            Vector3 s2c = center - this.Start;
            Vector3 e2c = center - this.End;
            float dmin = 0;

            dmin += Sqr(center.X < this.Start.X ? s2c.X : e2c.X);
            dmin += Sqr(center.Y < this.Start.Y ? s2c.Y : e2c.Y);
            dmin += Sqr(center.Z < this.Start.Z ? s2c.Z : e2c.Z);
            return dmin <= Sqr(radius);
        }

        public bool Intersects(AABox other)
        {
            Vector3 distances1 = other.Start - this.End;
            Vector3 distances2 = this.Start - other.End;
            Vector3 distances = Vector3.ComponentMax(distances1, distances2);
            float maxDistance = MathF.Max(distances.X, MathF.Max(distances.Y, distances.Z));
            return maxDistance < 0;
        }

        public Vector3? Intersects(Ray ray)
        {
            Vector3 dirFract = new Vector3(1 / ray.Direction.X, 1 / ray.Direction.Y, 1 / ray.Direction.Z);
            float t1 = (this.Start.X - ray.Origin.X) * dirFract.X;
            float t2 = (this.End.X - ray.Origin.X) * dirFract.X;
            float t3 = (this.Start.Y - ray.Origin.Y) * dirFract.Y;
            float t4 = (this.End.Y - ray.Origin.Y) * dirFract.Y;
            float t5 = (this.Start.Z - ray.Origin.Z) * dirFract.Z;
            float t6 = (this.End.Z - ray.Origin.Z) * dirFract.Z;
            float tmin = MathF.Max(MathF.Max(MathF.Min(t1, t2), MathF.Min(t3, t4)), MathF.Min(t5, t6));
            float tmax = MathF.Min(MathF.Min(MathF.Max(t1, t2), MathF.Max(t3, t4)), MathF.Max(t5, t6));
            if (tmax < 0)
            {
                return null;
            }

            // if tmin > tmax, ray doesn't intersect AABB
            if (tmin > tmax)
            {
                return null;
            }

            float t = tmin;
            return ray.Origin + (ray.Direction * t);
        }

        public bool Contains(Vector3 point)
        {
            return 
                point.X >= this.Start.X && point.Y >= this.Start.Y && point.Z >= this.Start.Z &&
                point.X <= this.End.X && point.Y <= this.End.Y && point.Z <= this.End.Z;
        }

        public bool Equals(AABox other) => other.Start.Equals(this.Start) && other.End.Equals(this.End);

        public override int GetHashCode() => HashCode.Combine(this.Start, this.End);

        public override bool Equals(object obj) => base.Equals(obj);

        public override string ToString() => base.ToString() + $"[{ this.Start }, { this.End }]";

        public static AABox operator *(AABox self, Vector3 by) => self.Scale(by);

        public static AABox operator /(AABox self, Vector3 by) => self.Scale(new Vector3(1 / by.X, 1 / by.Y, 1 / by.Z));

        public static AABox operator +(AABox self, Vector3 by) => self.Offset(by);

        public static AABox operator -(AABox self, Vector3 by) => self.Offset(-by);

        public static bool operator ==(AABox left, AABox right) => left.Equals(right);

        public static bool operator !=(AABox left, AABox right) => !(left == right);
    }
}
