namespace VTT.Util
{
    using System;
    using System.Numerics;

    public readonly struct Ray
    {
        private readonly Vector3 _origin;
        private readonly Vector3 _direction;
        private readonly Vector3 _invDir;

        public readonly Vector3 Origin => this._origin;
        public readonly Vector3 Direction => this._direction;
        public readonly Vector3 InverseDirection => this._invDir;

        public Ray(Vector3 origin, Vector3 direction)
        {
            this._origin = origin;
            this._direction = direction.Normalized();
            this._invDir = Vector3.One / this._direction;
        }

        public readonly Vector3? IntersectsSphere(Vector3 position, float radius)
        {
            Vector3 o_minus_c = this.Origin - position;
            float p = Vector3.Dot(this.Direction, o_minus_c);
            float q = Vector3.Dot(o_minus_c, o_minus_c) - (radius * radius);

            float discriminant = (p * p) - q;
            if (discriminant < 0.0f)
            {
                return null;
            }

            float dRoot = MathF.Sqrt(discriminant);
            float dist1 = -p - dRoot;
            float dist2 = -p + dRoot;
            return discriminant > 1e-7 ? this.Origin + (this.Direction * dist2) : this.Origin + (this.Direction * dist1);
        }

        public readonly (Vector3, Vector3)? IntersectsSphereBoth(Vector3 position, float radius)
        {
            Vector3 o_minus_c = this.Origin - position;
            float p = Vector3.Dot(this.Direction, o_minus_c);
            float q = Vector3.Dot(o_minus_c, o_minus_c) - (radius * radius);

            float discriminant = (p * p) - q;
            if (discriminant < 0.0f)
            {
                return null;
            }

            float dRoot = MathF.Sqrt(discriminant);
            float dist1 = -p - dRoot;
            float dist2 = -p + dRoot;
            return (this.Origin + (this.Direction * dist2), this.Origin + (this.Direction * dist1));
        }

        public readonly Vector3? IntersectsSphereInverse(Vector3 position, float radius)
        {
            Vector3 o_minus_c = this.Origin - position;
            float p = Vector3.Dot(this.Direction, o_minus_c);
            float q = Vector3.Dot(o_minus_c, o_minus_c) - (radius * radius);

            float discriminant = (p * p) - q;
            if (discriminant < 0.0f)
            {
                return null;
            }

            float dRoot = MathF.Sqrt(discriminant);
            float dist1 = -p - dRoot;
            float dist2 = -p + dRoot;
            return discriminant <= 1e-7 ? this.Origin + (this.Direction * dist2) : this.Origin + (this.Direction * dist1);
        }

        public readonly Ray Offset(Vector3 by) => new Ray(this.Origin + by, this.Direction);

        public readonly Vector3? IntersectsAABB(AABox box) => box.Intersects(this);

        public override readonly string ToString() => this.Origin.ToString() + ", " + this.Direction.ToString();
    }
}
