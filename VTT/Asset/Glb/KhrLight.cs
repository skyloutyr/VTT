namespace VTT.Asset.Glb
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System;
    using System.ComponentModel;
    using System.Runtime.Serialization;

    public class KhrLight
    {
        [DefaultValue("")]
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }


        [DefaultValue(new[] { 1.0f, 1.0f, 1.0f })]
        [JsonProperty(PropertyName = "color", DefaultValueHandling = DefaultValueHandling.Populate)]
        public float[] Color { get; set; }

        [DefaultValue(1.0f)]
        [JsonProperty(PropertyName = "intensity", DefaultValueHandling = DefaultValueHandling.Populate)]
        public float Intensity { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = "type")]
        public LightTypeEnum Type { get; set; }

        [JsonProperty(PropertyName = "range")]
        public float? Range { get; set; }

        [JsonProperty(PropertyName = "spot")]
        public SpotLightProperties Spot { get; set; }

        public enum LightTypeEnum
        {
            [EnumMember(Value = "directional")]
            Directional,

            [EnumMember(Value = "point")]
            Point,

            [EnumMember(Value = "spot")]
            Spot
        }

        public class SpotLightProperties
        {
            [DefaultValue(0.0f)]
            [JsonProperty(PropertyName = "innerConeAngle", DefaultValueHandling = DefaultValueHandling.Populate)]
            public float InnerConeAngle { get; set; }

            [DefaultValue(MathF.PI / 4.0f)]
            [JsonProperty(PropertyName = "outerConeAngle", DefaultValueHandling = DefaultValueHandling.Populate)]
            public float OuterConeAngle { get; set; }
        }
    }
}
