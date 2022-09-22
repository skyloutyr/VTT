namespace VTT.Asset.Glb
{
    using OpenTK.Mathematics;

    public class GlbLight
    {
        public Vector4 Color { get; set; }
        public float Intensity { get; set; }
        public KhrLight.LightTypeEnum LightType { get; set; }
    }
}
