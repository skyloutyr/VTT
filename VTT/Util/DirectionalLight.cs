namespace VTT.Util
{
    using System.Numerics;

    public struct DirectionalLight
    {
        public DirectionalLight(Vector3 dir, Vector3 clr)
        {
            this.Direction = dir;
            this.Color = clr;
        }

        public Vector3 Direction { get; set; }
        public Vector3 Color { get; set; }
    }
}
