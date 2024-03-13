namespace VTT.Util
{
    using Newtonsoft.Json;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.IO;

    public class ImageBase64Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(Image<Rgba32>);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            string val = (string)reader.Value;
            byte[] data = Convert.FromBase64String(val);
            return Image.Load<Rgba32>(data);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            Image<Rgba32> img = (Image<Rgba32>)value;
            using MemoryStream ms = new MemoryStream();
            img.SaveAsPng(ms);
            string s = Convert.ToBase64String(ms.ToArray());
            writer.WriteValue(s);
        }
    }
}
