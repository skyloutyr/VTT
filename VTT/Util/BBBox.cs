namespace VTT.Util
{
    using OpenTK.Mathematics;
    using System;
    using System.Collections;
    using System.Collections.Generic;

    public struct BBBox : IEquatable<BBBox>, IEnumerable<Vector3>
    {
        public Vector3 Start { get; set; }
        public Vector3 End { get; set; }
        public Quaternion Rotation { get; set; }

        public BBBox(Vector3 start, Vector3 end, Quaternion quat) : this()
        {
            this.Start = new Vector3(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Min(start.Z, end.Z));
            this.End = new Vector3(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y), Math.Max(start.Z, end.Z));
            this.Rotation = quat;
        }

        public BBBox(AABox box, Quaternion quat) : this(box.Start, box.End, quat)
        {
        }

        public static BBBox FromPositionAndSize(Vector3 pos, Vector3 size) => new BBBox(pos, pos + size, Quaternion.Identity);

        public readonly BBBox Offset(Vector3 by) => new BBBox(this.Start + by, this.End + by, this.Rotation);

        public readonly BBBox Scale(Vector3 by) => new BBBox(this.Start * by, this.End * by, this.Rotation);
        public readonly BBBox Scale(float by) => new BBBox(this.Start * by, this.End * by, this.Rotation);

        public readonly BBBox Rotate(Quaternion by) => new BBBox(this.Start, this.End, this.Rotation * by);

        public readonly BBBox SetRotation(Quaternion to) => new BBBox(this.Start, this.End, to);

        public readonly AABox GetBounds()
        {
            Matrix3 m = Matrix3.CreateFromQuaternion(this.Rotation.Normalized());

            float minX = 0, minY = 0, minZ = 0;
            float maxX = 0, maxY = 0, maxZ = 0;

            Vector3 sStart = this.Start;
            Vector3 sEnd = this.End;

            float a = m.Row0.X * sStart.X;
            float b = m.Row0.X * this.End.X;
            bool ab = a < b;
            minX += ab ? a : b;
            maxX += ab ? b : a;
            a = m.Row1.X * sStart.X;
            b = m.Row1.X * sEnd.X;
            ab = a < b;
            minX += ab ? a : b;
            maxX += ab ? b : a;
            a = m.Row2.X * sStart.X;
            b = m.Row2.X * sEnd.X;
            ab = a < b;
            minX += ab ? a : b;
            maxX += ab ? b : a;

            a = m.Row0.Y * sStart.Y;
            b = m.Row0.Y * sEnd.Y;
            ab = a < b;
            minY += ab ? a : b;
            maxY += ab ? b : a;
            a = m.Row1.Y * sStart.Y;
            b = m.Row1.Y * sEnd.Y;
            ab = a < b;
            minY += ab ? a : b;
            maxY += ab ? b : a;
            a = m.Row2.Y * sStart.Y;
            b = m.Row2.Y * sEnd.Y;
            ab = a < b;
            minY += ab ? a : b;
            maxY += ab ? b : a;

            a = m.Row0.Z * sStart.Z;
            b = m.Row0.Z * sEnd.Z;
            ab = a < b;
            minZ += ab ? a : b;
            maxZ += ab ? b : a;
            a = m.Row1.Z * sStart.Z;
            b = m.Row1.Z * sEnd.Z;
            ab = a < b;
            minZ += ab ? a : b;
            maxZ += ab ? b : a;
            a = m.Row2.Z * sStart.Z;
            b = m.Row2.Z * sEnd.Z;
            ab = a < b;
            minZ += ab ? a : b;
            maxZ += ab ? b : a;

            return new AABox(minX, minY, minZ, maxX, maxY, maxZ);
        }

        public readonly Vector3? Intersects(Ray ray, Vector3 offset = default)
        {
            Matrix4 modelMatrix = Matrix4.CreateFromQuaternion(this.Rotation) * Matrix4.CreateTranslation(offset);

            float tMin = 0.0f;
            float tMax = 100000.0f;

            Vector3 worldSpace = (modelMatrix[3, 0], modelMatrix[3, 1], modelMatrix[3, 2]);
            Vector3 delta = worldSpace - ray.Origin;

            {
                Vector3 xaxis = (modelMatrix[0, 0], modelMatrix[0, 1], modelMatrix[0, 2]);
                float e = Vector3.Dot(xaxis, delta);
                float f = Vector3.Dot(ray.Direction, xaxis);

                if (MathF.Abs(f) > 0.001f)
                {
                    float t1 = (e + this.Start.X) / f;
                    float t2 = (e + this.End.X) / f;

                    if (t1 > t2)
                    {
                        (t2, t1) = (t1, t2);
                    }

                    if (t2 < tMax) tMax = t2;
                    if (t1 > tMin) tMin = t1;
                    if (tMin > tMax) return null;

                }
                else
                {
                    if (-e + this.Start.X > 0.0f || -e + this.End.X < 0.0f) return null;
                }
            }


            {
                Vector3 yaxis = (modelMatrix[1, 0], modelMatrix[1, 1], modelMatrix[1, 2]);
                float e = Vector3.Dot(yaxis, delta);
                float f = Vector3.Dot(ray.Direction, yaxis);

                if (MathF.Abs(f) > 0.001f)
                {

                    float t1 = (e + this.Start.Y) / f;
                    float t2 = (e + this.End.Y) / f;

                    if (t1 > t2)
                    {
                        (t2, t1) = (t1, t2);
                    }

                    if (t2 < tMax) tMax = t2;
                    if (t1 > tMin) tMin = t1;
                    if (tMin > tMax) return null;

                }
                else
                {
                    if (-e + this.Start.Y > 0.0f || -e + this.End.Y < 0.0f) return null;
                }
            }


            {
                Vector3 zaxis = (modelMatrix[2, 0], modelMatrix[2, 1], modelMatrix[2, 2]);
                float e = Vector3.Dot(zaxis, delta);
                float f = Vector3.Dot(ray.Direction, zaxis);

                if (MathF.Abs(f) > 0.001f)
                {

                    float t1 = (e + this.Start.Z) / f;
                    float t2 = (e + this.End.Z) / f;

                    if (t1 > t2)
                    {
                        (t2, t1) = (t1, t2);
                    }

                    if (t2 < tMax) tMax = t2;
                    if (t1 > tMin) tMin = t1;
                    if (tMin > tMax) return null;

                }
                else
                {
                    if (-e + this.Start.Z > 0.0f || -e + this.End.Z < 0.0f) return null;
                }
            }

            return ray.Origin + (ray.Direction * tMin);

            // return new AABox(this.Start, this.End).Intersects(new Ray(ori.Xyz, dir.Xyz));
        }

        public readonly bool Equals(BBBox other) => other.Start.Equals(this.Start) && other.End.Equals(this.End);

        public override readonly int GetHashCode() => HashCode.Combine(this.Start, this.End);

        public override readonly bool Equals(object obj) => base.Equals(obj);

        public override readonly string ToString() => base.ToString() + $"[{this.Start}, {this.End}]";

        public readonly IEnumerator<Vector3> GetEnumerator()
        {
            Vector3 s = this.Start;
            Vector3 e = this.End;

            Quaternion q = this.Rotation.Normalized();

            Vector3 p0 = q * new Vector3(s.X, s.Y, s.Z); // -X, -Y, -Z
            Vector3 p1 = q * new Vector3(e.X, s.Y, s.Z); // +X, -Y, -Z
            Vector3 p2 = q * new Vector3(s.X, e.Y, s.Z); // -X, +Y, -Z
            Vector3 p3 = q * new Vector3(e.X, e.Y, s.Z); // +X, +Y, -Z
            Vector3 p4 = q * new Vector3(s.X, s.Y, e.Z); // -X, -Y, +Z
            Vector3 p5 = q * new Vector3(e.X, s.Y, e.Z); // +X, -Y, +Z
            Vector3 p6 = q * new Vector3(s.X, e.Y, e.Z); // -X, +Y, +Z
            Vector3 p7 = q * new Vector3(e.X, e.Y, e.Z); // +X, +Y, +Z
            yield return p0;
            yield return p1;
            yield return p2;
            yield return p3;
            yield return p4;
            yield return p5;
            yield return p6;
            yield return p7;
        }

        readonly IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public static BBBox operator *(BBBox self, Vector3 by) => self.Scale(by);

        public static BBBox operator /(BBBox self, Vector3 by) => self.Scale(new Vector3(1 / by.X, 1 / by.Y, 1 / by.Z));

        public static BBBox operator +(BBBox self, Vector3 by) => self.Offset(by);

        public static BBBox operator -(BBBox self, Vector3 by) => self.Offset(-by);

        public static bool operator ==(BBBox left, BBBox right) => left.Equals(right);

        public static bool operator !=(BBBox left, BBBox right) => !(left == right);
    }
}
