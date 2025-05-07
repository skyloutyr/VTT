
namespace VTT.Control
{
    using System.Numerics;
    using VTT.Util;

    public class FastLight : ISerializable
    {
        public Vector4 Offset { get; set; }
        public bool UseObjectTransform { get; set; }
        public Vector4 Color { get; set; }
        public bool Enabled { get; set; }
        public float Intensity { get; set; }
        public float Radius
        {
            get => this.Color.W;
            set => this.Color = new Vector4(this.Color.Xyz(), value);
        }

        public Vector3 LightColor
        {
            get => this.Color.Xyz();
            set => this.Color = new Vector4(value, this.Color.W);
        }

        public Vector3 Translation
        {
            get => this.Offset.Xyz();
            set => this.Offset = new Vector4(value, this.Offset.W);
        }

        public FastLight Clone()
        {
            FastLight ret = new FastLight()
            {
                Offset = this.Offset,
                UseObjectTransform = this.UseObjectTransform,
                Color = this.Color,
                Enabled = this.Enabled,
                Intensity = this.Intensity
            };

            return ret;
        }

        public void Deserialize(DataElement e)
        {
            this.Offset = e.GetVec4Legacy("Offset");
            this.UseObjectTransform = e.GetBool("UCO");
            this.Color = e.GetVec4Legacy("Color");
            this.Enabled = e.GetBool("Enabled");
            this.Intensity = e.GetSingle("Intensity", 1.0f);
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetVec4("Offset", this.Offset);
            ret.SetBool("UCO", this.UseObjectTransform);
            ret.SetVec4("Color", this.Color);
            ret.SetBool("Enabled", this.Enabled);
            ret.SetSingle("Intensity", this.Intensity);
            return ret;
        }

        public static FastLight FromData(DataElement de)
        {
            FastLight ret = new FastLight();
            ret.Deserialize(de);
            return ret;
        }
    }
}