namespace VTT.Util
{
    using Newtonsoft.Json;
    using System;

    public class VersionConverter : JsonConverter<Version>
    {
        public override Version ReadJson(JsonReader reader, Type objectType, Version existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string s = serializer.Deserialize<string>(reader);
            return s != null ? Version.Parse(s) : hasExistingValue ? existingValue : null;
        }

        public override void WriteJson(JsonWriter writer, Version value, JsonSerializer serializer) => serializer.Serialize(writer, value.ToString());
    }
}
