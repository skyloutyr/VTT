﻿namespace VTT.Util
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Numerics;

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
            Matrix4x4 m = Matrix4x4.CreateFromQuaternion(this.Rotation.Normalized());

            float minX = 0, minY = 0, minZ = 0;
            float maxX = 0, maxY = 0, maxZ = 0;

            Vector3 sStart = this.Start;
            Vector3 sEnd = this.End;

            float a = m.M11 * sStart.X;
            float b = m.M11 * this.End.X;
            bool ab = a < b;
            minX += ab ? a : b;
            maxX += ab ? b : a;
            a = m.M21 * sStart.X;
            b = m.M21 * sEnd.X;
            ab = a < b;
            minX += ab ? a : b;
            maxX += ab ? b : a;
            a = m.M31 * sStart.X;
            b = m.M31 * sEnd.X;
            ab = a < b;
            minX += ab ? a : b;
            maxX += ab ? b : a;

            a = m.M12 * sStart.Y;
            b = m.M12 * sEnd.Y;
            ab = a < b;
            minY += ab ? a : b;
            maxY += ab ? b : a;
            a = m.M22 * sStart.Y;
            b = m.M22 * sEnd.Y;
            ab = a < b;
            minY += ab ? a : b;
            maxY += ab ? b : a;
            a = m.M32 * sStart.Y;
            b = m.M32 * sEnd.Y;
            ab = a < b;
            minY += ab ? a : b;
            maxY += ab ? b : a;

            a = m.M13 * sStart.Z;
            b = m.M13 * sEnd.Z;
            ab = a < b;
            minZ += ab ? a : b;
            maxZ += ab ? b : a;
            a = m.M23 * sStart.Z;
            b = m.M23 * sEnd.Z;
            ab = a < b;
            minZ += ab ? a : b;
            maxZ += ab ? b : a;
            a = m.M33 * sStart.Z;
            b = m.M33 * sEnd.Z;
            ab = a < b;
            minZ += ab ? a : b;
            maxZ += ab ? b : a;

            return new AABox(minX, minY, minZ, maxX, maxY, maxZ);
        }

        public readonly Vector3? Intersects(Ray ray, Vector3 offset = default)
        {
            Matrix4x4 modelMatrix = Matrix4x4.CreateFromQuaternion(this.Rotation) * Matrix4x4.CreateTranslation(offset);

            float tMin = 0.0f;
            float tMax = 100000.0f;

            Vector3 worldSpace = new Vector3(modelMatrix.M41, modelMatrix.M42, modelMatrix.M43);
            Vector3 delta = worldSpace - ray.Origin;

            {
                Vector3 xaxis = new Vector3(modelMatrix.M11, modelMatrix.M12, modelMatrix.M13);
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
                Vector3 yaxis = new Vector3(modelMatrix.M21, modelMatrix.M22, modelMatrix.M23);
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
                Vector3 zaxis = new Vector3(modelMatrix.M31, modelMatrix.M32, modelMatrix.M33);
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

            Vector3 p0 = Vector4.Transform(new Vector3(s.X, s.Y, s.Z), q).Xyz(); // -X, -Y, -Z
            Vector3 p1 = Vector4.Transform(new Vector3(e.X, s.Y, s.Z), q).Xyz(); // +X, -Y, -Z
            Vector3 p2 = Vector4.Transform(new Vector3(s.X, e.Y, s.Z), q).Xyz(); // -X, +Y, -Z
            Vector3 p3 = Vector4.Transform(new Vector3(e.X, e.Y, s.Z), q).Xyz(); // +X, +Y, -Z
            Vector3 p4 = Vector4.Transform(new Vector3(s.X, s.Y, e.Z), q).Xyz(); // -X, -Y, +Z
            Vector3 p5 = Vector4.Transform(new Vector3(e.X, s.Y, e.Z), q).Xyz(); // +X, -Y, +Z
            Vector3 p6 = Vector4.Transform(new Vector3(s.X, e.Y, e.Z), q).Xyz(); // -X, +Y, +Z
            Vector3 p7 = Vector4.Transform(new Vector3(e.X, e.Y, e.Z), q).Xyz(); // +X, +Y, +Z
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
