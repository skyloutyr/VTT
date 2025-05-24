namespace VTT.Util
{
    using System.Numerics;

    public struct FrustumCullingSphere
    {
        public Vector3 position;
        public float radius;
        public int cachedPlane;

        public FrustumCullingSphere(Vector3 position, float radius)
        {
            this.position = position;
            this.radius = radius;
            this.cachedPlane = -1;
        }

        public readonly bool Intersects(Ray r)
        {
            Vector3 oc = r.Origin - this.position;
            float a = Vector3.Dot(r.Direction, r.Direction);
            float b = 2 * Vector3.Dot(oc, r.Direction);
            float c = Vector3.Dot(oc, oc) - (this.radius * this.radius);
            float d = (b * b) - (4 * a * c);
            return d > 0;
        }
    }
}
