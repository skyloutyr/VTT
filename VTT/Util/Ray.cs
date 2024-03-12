namespace VTT.Util
{
    using System;
    using System.Numerics;

    public struct Ray
    {
        public Vector3 Origin { get; set; }
        public Vector3 Direction { get; set; }

        public Ray(Vector3 origin, Vector3 direction)
        {
            this.Origin = origin;
            this.Direction = direction.Normalized();
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


        public override readonly string ToString() => this.Origin.ToString() + ", " + this.Direction.ToString();
    }
}
