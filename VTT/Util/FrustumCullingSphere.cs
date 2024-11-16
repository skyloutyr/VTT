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
    }
}
