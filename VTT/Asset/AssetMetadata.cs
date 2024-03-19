namespace VTT.Asset
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System;
    using System.IO;
    using VTT.Util;

    public class AssetMetadata : ISerializable
    {
        [JsonIgnore]
        public static AssetMetadata Broken => new AssetMetadata() { Invalid = true, Name = string.Empty, Type = AssetType.Texture };

        [JsonConverter(typeof(StringEnumConverter))]
        public AssetType Type { get; set; }

        public string Name { get; set; }
        public int Version { get; set; }

        public TextureData.Metadata TextureInfo { get; set; }
        public ModelData.Metadata ModelInfo { get; set; }
        public SoundData.Metadata SoundInfo { get; set; }

        [JsonIgnore]
        public bool Invalid { get; set; }

        [JsonIgnore]
        public bool ConstructedFromOldBinaryEncoding { get; set; }

        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime UploadTime { get; set; } = DateTime.UnixEpoch;

        public AssetMetadata()
        {
        }

        public AssetMetadata(BinaryReader br)
        {
            DataElement d = new DataElement(br);
            this.Deserialize(d);
        }

        public void Deserialize(DataElement e)
        {
            this.Type = e.GetEnum<AssetType>("Type");
            this.Name = e.Get<string>("Name");
            this.Version = e.Get<int>("Version", 1);
            if (e.Has("TextureInfo", DataType.Map))
            {
                this.TextureInfo = new TextureData.Metadata();
                this.TextureInfo.Deserialize(e.Get<DataElement>("TextureInfo"));
            }

            if (e.Has("ModelInfo", DataType.Map))
            {
                this.ModelInfo = new ModelData.Metadata();
                this.ModelInfo.Deserialize(e.Get<DataElement>("ModelInfo"));
            }

            if (e.Has("SoundInfo", DataType.Map))
            {
                this.SoundInfo = new SoundData.Metadata();
                this.SoundInfo.Deserialize(e.Get<DataElement>("SoundInfo"));
            }

            this.UploadTime = e.GetDateTime("UploadTime", DateTime.UnixEpoch);
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.Set("Name", this.Name);
            ret.SetEnum("Type", this.Type);
            ret.Set("Version", this.Version);
            if (this.TextureInfo != null)
            {
                ret.Set("TextureInfo", this.TextureInfo.Serialize());
            }

            if (this.ModelInfo != null)
            {
                ret.Set("ModelInfo", this.ModelInfo.Serialize());
            }

            if (this.SoundInfo != null)
            {
                ret.Set("SoundInfo", this.SoundInfo.Serialize());
            }

            ret.SetDateTime("UploadTime", this.UploadTime);

            return ret;
        }
    }
}
