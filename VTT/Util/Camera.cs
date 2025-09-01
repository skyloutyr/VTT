namespace VTT.Util
{
    using System;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using VTT.Network;
    using SysMath = System.Math;

    public class Camera
    {
        public Camera()
        {
        }

        public Camera(Vector3 pos, float yaw, float pitch)
        {
            this.Position = pos;
            this.Yaw = yaw;
            this.Pitch = pitch;
        }

        public virtual Vector3 Direction { get; set; }
        public virtual Vector3 Position { get; set; }
        public virtual Vector3 Right { get; set; }
        public virtual Vector3 Up { get; set; }
        public virtual float Yaw { get; set; }
        public virtual float Pitch { get; set; }
        public virtual Matrix4x4 View { get; set; }
        public virtual Matrix4x4 OriginView { get; set; }
        public virtual Matrix4x4 Projection { get; set; }
        public virtual Matrix4x4 ViewProj { get; set; }
        public virtual Matrix4x4 ProjView { get; set; }
        public virtual Matrix4x4 OriginViewProj { get; set; }
        public virtual Matrix4x4 OriginProjView { get; set; }
        public Frustum frustum;

        public virtual void RecalculateData(bool calculateDirection = true, bool calculateUp = true, Vector3 assumedUpAxis = default)
        {
            Vector3 direction = this.Direction;
            if (calculateDirection)
            {
                float c = (float)SysMath.Cos(this.Pitch * MathF.PI / 180);
                direction = this.Direction = new Vector3(
                        c * (float)SysMath.Cos(this.Yaw * MathF.PI / 180),
                            (float)SysMath.Sin(this.Pitch * MathF.PI / 180),
                        c * (float)SysMath.Sin(this.Yaw * MathF.PI / 180)).Normalized();
            }

            if (assumedUpAxis.Equals(default))
            {
                assumedUpAxis = this.Up.Equals(default) ? Vector3.UnitY : this.Up;
            }

            this.Right = Vector3.Normalize(Vector3.Cross(direction, assumedUpAxis));
            if (calculateUp)
            {
                this.Up = Vector3.Normalize(Vector3.Cross(this.Right, direction));
            }

            this.View = Matrix4x4.CreateLookAt(this.Position, this.Position + direction, this.Up);
            this.OriginView = Matrix4x4.CreateLookAt(Vector3.Zero, direction, this.Up);
            this.ViewProj = this.View * this.Projection;
            this.ProjView = this.Projection * this.View;
            this.OriginViewProj = this.OriginView * this.Projection;
            this.OriginProjView = this.Projection * this.OriginView;
            this.frustum = new Frustum(this.ViewProj);
        }

        public virtual Vector3 ToScreenspace(Vector3 worldSpace)
        {
            Vector4 worldVec = new Vector4(worldSpace, 1);
            Vector4 postProjectivePosition = Vector4.Transform(worldVec, this.ViewProj);
            float clipSpaceX = postProjectivePosition.X / postProjectivePosition.W;
            float clipSpaceY = postProjectivePosition.Y / postProjectivePosition.W;
            float clipSpaceZ = postProjectivePosition.Z;
            return (Vector3.One + new Vector3(clipSpaceX, -clipSpaceY, clipSpaceZ)) / 2 * new Vector3(Client.Instance.Frontend.Width, Client.Instance.Frontend.Height, 1);
        }

        public virtual Ray RayFromCursor() => this.RayFromCursor(Client.Instance.Frontend.MouseX, Client.Instance.Frontend.MouseY, Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);

        public virtual Ray RayFromCursor(float mouseX, float mouseY, float width, float height)
        {
            float x = (2.0f * mouseX / width) - 1.0f;
            float y = 1.0f - (2.0f * mouseY / height);
            Matrix4x4.Invert(this.OriginViewProj, out Matrix4x4 mat);
            Vector4 vec = new Vector4(x, y, 1, 1);
            vec = Vector4.Transform(vec, mat);
            vec /= vec.W;
            vec = vec.Normalized();
            return new Ray(this.Position, vec.Xyz());
        }

        public virtual void MoveCamera(Vector3 newLocation, bool recalculateData = true)
        {
            this.Position = newLocation;
            if (recalculateData)
            {
                this.RecalculateData(false, false, this.Up);
            }
        }

        public virtual bool IsPointInFrustum(Vector3 point) => this.frustum.IsPointInFrustum(point);

        public virtual bool IsSphereInFrustum(Vector3 point, float radius) => this.frustum.IsSphereInFrustum(point, radius);

        public virtual bool IsSphereInFrustumCached(ref FrustumCullingSphere sphere) => this.frustum.IsSphereInFrustumCached(ref sphere);

        public virtual bool IsAABoxInFrustum(AABox box, Vector3 point = default)
        {
            box += point;
            return box.Contains(this.Position) || this.frustum.IsAABoxInFrustum(box);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = sizeof(float) * 4 * 6, Pack = 1)]
    public unsafe readonly struct Frustum
    {
        [FieldOffset(0)]
        public readonly Plane p0;

        [FieldOffset(sizeof(float) * 4 * 1)]
        public readonly Plane p1;

        [FieldOffset(sizeof(float) * 4 * 2)]
        public readonly Plane p2;

        [FieldOffset(sizeof(float) * 4 * 3)]
        public readonly Plane p3;

        [FieldOffset(sizeof(float) * 4 * 4)]
        public readonly Plane p4;

        [FieldOffset(sizeof(float) * 4 * 5)]
        public readonly Plane p5;

        private readonly Plane this[int i]
        {
            get
            {
                fixed (Plane* p = &this.p0)
                {
                    return p[i];
                }
            }
        }

        public Frustum(Matrix4x4 viewProj)
        {
            this.p0 = new Plane(
                viewProj.M14 + viewProj.M11,
                viewProj.M24 + viewProj.M21,
                viewProj.M34 + viewProj.M31,
                viewProj.M44 + viewProj.M41).Normalized();
            this.p1 = new Plane(
                viewProj.M14 - viewProj.M11,
                viewProj.M24 - viewProj.M21,
                viewProj.M34 - viewProj.M31,
                viewProj.M44 - viewProj.M41).Normalized();
            this.p2 = new Plane(
                viewProj.M14 - viewProj.M12,
                viewProj.M24 - viewProj.M22,
                viewProj.M34 - viewProj.M32,
                viewProj.M44 - viewProj.M42).Normalized();
            this.p3 = new Plane(
                viewProj.M14 + viewProj.M12,
                viewProj.M24 + viewProj.M22,
                viewProj.M34 + viewProj.M32,
                viewProj.M44 + viewProj.M42).Normalized();
            this.p4 = new Plane(
                viewProj.M13,
                viewProj.M23,
                viewProj.M33,
                viewProj.M43).Normalized();
            this.p5 = new Plane(
                viewProj.M14 - viewProj.M13,
                viewProj.M24 - viewProj.M23,
                viewProj.M34 - viewProj.M33,
                viewProj.M44 - viewProj.M43).Normalized();
        }

        // Loop here was manually unrolled bc of a very significant performance gain
        public readonly bool IsPointInFrustum(Vector3 point) =>
            this.p0.DotProduct(point) >= 0 &&
            this.p1.DotProduct(point) >= 0 &&
            this.p2.DotProduct(point) >= 0 &&
            this.p3.DotProduct(point) >= 0 && 
            this.p4.DotProduct(point) >= 0 &&
            this.p5.DotProduct(point) >= 0;

        // Loop here was manually unrolled bc of a very significant performance gain
        public readonly bool IsSphereInFrustum(Vector3 point, float radius) => 
            this.p0.DotProduct(point) + radius >= 0 &&
            this.p1.DotProduct(point) + radius >= 0 &&
            this.p2.DotProduct(point) + radius >= 0 &&
            this.p3.DotProduct(point) + radius >= 0 &&
            this.p4.DotProduct(point) + radius >= 0 &&
            this.p5.DotProduct(point) + radius >= 0;

        public readonly bool IsSphereInFrustumCached(ref FrustumCullingSphere sphere)
        {
            fixed (Plane* planes = &this.p0)
            {
                if (sphere.cachedPlane != -1)
                {
                    Plane p = planes[sphere.cachedPlane];
                    if (p.DotProduct(sphere.position) + sphere.radius < 0)
                    {
                        return false;
                    }
                }

                for (int i = 5; i >= 0; --i)
                {
                    if (i == sphere.cachedPlane)
                    {
                        continue;
                    }

                    Plane p = planes[i];
                    if (p.DotProduct(sphere.position) + sphere.radius < 0)
                    {
                        sphere.cachedPlane = i;
                        return false;
                    }
                }
            }

            return true;
        }

        // This section is a mess of cpp-like code bc it is a performance-critical hot path
        // Struct ptr bc data locality is guaranteed either way, no need to copy value to stack
        private static bool TestBoxAgainstPlane(Plane* p, in AABox box)
        {
            Vector3 normal = p->Normal;
            Vector3 start = new Vector3(normal.X < 0 ? box.Start.X : box.End.X, normal.Y < 0 ? box.Start.Y : box.End.Y, normal.Z < 0 ? box.Start.Z : box.End.Z);
            Vector3 end = new Vector3(normal.X >= 0 ? box.Start.X : box.End.X, normal.Y >= 0 ? box.Start.Y : box.End.Y, normal.Z >= 0 ? box.Start.Z : box.End.Z);
            return p->DotProduct(end) > 0 || p->DotProduct(start) > 0;
        }

        public readonly bool IsAABoxInFrustum(AABox box)
        {
            fixed (Plane* planes = &this.p0)
            {
                if (!TestBoxAgainstPlane(planes, in box))
                {
                    return false;
                }

                if (!TestBoxAgainstPlane(planes + 1, in box))
                {
                    return false;
                }

                if (!TestBoxAgainstPlane(planes + 2, in box))
                {
                    return false;
                }

                if (!TestBoxAgainstPlane(planes + 3, in box))
                {
                    return false;
                }

                if (!TestBoxAgainstPlane(planes + 4, in box))
                {
                    return false;
                }

                if (!TestBoxAgainstPlane(planes + 5, in box))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
