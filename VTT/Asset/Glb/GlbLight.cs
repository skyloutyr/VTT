namespace VTT.Asset.Glb
{
    using System.Numerics;

    public readonly struct GlbLight
    {
        public Vector4 Color { get; }
        public float Intensity { get; }
        public KhrLight.LightTypeEnum LightType { get; }

        public GlbLight(Vector4 color, float intensity, KhrLight.LightTypeEnum lightType)
        {
            this.Color = color;
            this.Intensity = intensity;
            this.LightType = lightType;
        }
    }
}
