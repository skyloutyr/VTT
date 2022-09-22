namespace VTT.Util
{
    using OpenTK.Mathematics;
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
        public virtual Matrix4 View { get; set; }
        public virtual Matrix4 OriginView { get; set; }
        public virtual Matrix4 Projection { get; set; }
        public virtual Matrix4 ViewProj { get; set; }
        public virtual Matrix4 ProjView { get; set; }
        public virtual Matrix4 OriginViewProj { get; set; }
        public virtual Matrix4 OriginProjView { get; set; }
        public Plane[] Frustrum { get; set; } = new Plane[6];

        public virtual void RecalculateData(bool calculateDirection = true, bool calculateUp = true, Vector3 assumedUpAxis = default)
        {
            Vector3 direction = this.Direction;
            if (calculateDirection)
            {
                float c = (float)SysMath.Cos(MathHelper.DegreesToRadians(this.Pitch));
                direction = this.Direction = new Vector3(
                        c * (float)SysMath.Cos(MathHelper.DegreesToRadians(this.Yaw)),
                            (float)SysMath.Sin(MathHelper.DegreesToRadians(this.Pitch)),
                        c * (float)SysMath.Sin(MathHelper.DegreesToRadians(this.Yaw))).Normalized();
            }

            if (assumedUpAxis.Equals(default))
            {
                assumedUpAxis = Vector3.UnitY;
            }

            this.Right = Vector3.Normalize(Vector3.Cross(direction, assumedUpAxis));
            if (calculateUp)
            {
                this.Up = Vector3.Normalize(Vector3.Cross(this.Right, direction));
            }

            this.View = Matrix4.LookAt(this.Position, this.Position + direction, this.Up);
            this.OriginView = Matrix4.LookAt(Vector3.Zero, direction, this.Up);
            this.ViewProj = this.View * this.Projection;
            this.ProjView = this.Projection * this.View;
            this.OriginViewProj = this.OriginView * this.Projection;
            this.OriginProjView = this.Projection * this.OriginView;
            this.Frustrum[0] = new Plane(
                this.ViewProj.M14 + this.ViewProj.M11,
                this.ViewProj.M24 + this.ViewProj.M21,
                this.ViewProj.M34 + this.ViewProj.M31,
                this.ViewProj.M44 + this.ViewProj.M41).Normalized();
            this.Frustrum[1] = new Plane(
                this.ViewProj.M14 - this.ViewProj.M11,
                this.ViewProj.M24 - this.ViewProj.M21,
                this.ViewProj.M34 - this.ViewProj.M31,
                this.ViewProj.M44 - this.ViewProj.M41).Normalized();
            this.Frustrum[2] = new Plane(
                this.ViewProj.M14 - this.ViewProj.M12,
                this.ViewProj.M24 - this.ViewProj.M22,
                this.ViewProj.M34 - this.ViewProj.M32,
                this.ViewProj.M44 - this.ViewProj.M42).Normalized();
            this.Frustrum[3] = new Plane(
                this.ViewProj.M14 + this.ViewProj.M12,
                this.ViewProj.M24 + this.ViewProj.M22,
                this.ViewProj.M34 + this.ViewProj.M32,
                this.ViewProj.M44 + this.ViewProj.M42).Normalized();
            this.Frustrum[4] = new Plane(
                this.ViewProj.M13,
                this.ViewProj.M23,
                this.ViewProj.M33,
                this.ViewProj.M43).Normalized();
            this.Frustrum[5] = new Plane(
                this.ViewProj.M14 - this.ViewProj.M13,
                this.ViewProj.M24 - this.ViewProj.M23,
                this.ViewProj.M34 - this.ViewProj.M33,
                this.ViewProj.M44 - this.ViewProj.M43).Normalized();
        }

        public virtual Vector3 ToScreenspace(Vector3 worldSpace)
        {
            Vector4 worldVec = new Vector4(worldSpace, 1);
            Vector4 postProjectivePosition = Vector4.TransformRow(worldVec, this.ViewProj);
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
            Matrix4 mat = this.OriginViewProj.Inverted();
            Vector4 vec = new Vector4(x, y, 1, 1);
            vec *= mat;
            vec /= vec.W;
            vec = vec.Normalized();
            return new Ray(this.Position, vec.Xyz);
        }

        public virtual bool IsPointInFrustrum(Vector3 point)
        {
            foreach (Plane p in this.Frustrum)
            {
                if (p.DotProduct(point) <= 0)
                {
                    return false;
                }
            }

            return true;
        }

        public virtual bool IsSphereInFrustrum(Vector3 point, float radius)
        {
            foreach (Plane p in this.Frustrum)
            {
                if (p.DotProduct(point) + radius < 0)
                {
                    return false;
                }
            }

            return true;
        }

        public virtual bool IsAABoxInFrustrum(AABox box, Vector3 point = default)
        {
            box += point;
            if (box.Contains(this.Position))
            {
                return true;
            }

            foreach (Plane p in this.Frustrum)
            {
                Vector3 normal = p.Normal;
                Vector3 start = new Vector3(normal.X < 0 ? box.Start.X : box.End.X, normal.Y < 0 ? box.Start.Y : box.End.Y, normal.Z < 0 ? box.Start.Z : box.End.Z);
                Vector3 end = new Vector3(normal.X >= 0 ? box.Start.X : box.End.X, normal.Y >= 0 ? box.Start.Y : box.End.Y, normal.Z >= 0 ? box.Start.Z : box.End.Z);
                if (p.DotProduct(end) <= 0 && p.DotProduct(start) <= 0)
                {
                    return false;
                }
            }

            return true;
        }

        public virtual void MoveCamera(Vector3 newLocation, bool recalculateData = true)
        {
            this.Position = newLocation;
            if (recalculateData)
            {
                this.RecalculateData();
            }
        }
    }
}
