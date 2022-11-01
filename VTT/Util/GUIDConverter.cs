namespace VTT.Util
{
    using Newtonsoft.Json;
    using System;

    public class GUIDConverter : JsonConverter<Guid>
    {
        public override Guid ReadJson(JsonReader reader, Type objectType, Guid existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string s = serializer.Deserialize<string>(reader);
            return s != null ? Guid.Parse(s) : hasExistingValue ? existingValue : Guid.Empty;
        }

        public override void WriteJson(JsonWriter writer, Guid value, JsonSerializer serializer) => serializer.Serialize(writer, value.ToString());
    }
}
