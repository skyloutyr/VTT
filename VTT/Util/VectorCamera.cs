namespace VTT.Util
{
    using System.Numerics;

    /// <summary>
    /// This version of <see cref="Camera"/> type omits the pitch and yaw system in favour of the position and look vectors.
    /// </summary>
    public class VectorCamera : Camera
    {
        public VectorCamera(Vector3 pos, Vector3 look) : base()
        {
            this.Position = pos;
            this.Direction = look.Normalized();
        }

        public override void RecalculateData(bool calculateDirection = true, bool calculateUp = true, Vector3 assumedUpVector = default) => base.RecalculateData(false, calculateUp, assumedUpVector);

        public virtual void SetDirection(Vector3 look)
        {
            this.Direction = look.Normalized();
            this.RecalculateData();
        }

        public virtual void SetPositionDirection(Vector3 pos, Vector3 look)
        {
            this.Position = pos;
            this.Direction = look.Normalized();
            this.RecalculateData();
        }

        public virtual void LookAt(Vector3 point) => this.SetDirection(point - this.Position);

        public virtual void SetPositionAndLookAt(Vector3 pos, Vector3 point) => this.SetPositionDirection(pos, point - this.Position);
    }
}
