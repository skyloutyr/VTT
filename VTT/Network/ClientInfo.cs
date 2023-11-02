﻿namespace VTT.Network
{
    using Newtonsoft.Json;
    using SixLabors.ImageSharp;
    using System;
    using System.ComponentModel;
    using System.IO;
    using VTT.Util;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used for automatic json property parsing and conversion through C# property system (see impl)")]
    public class ClientInfo
    {
        [JsonProperty(PropertyName = "Color")]
        private uint ClrInt
        {
            get => this.Color.Argb();
            set => this.Color = Extensions.FromArgb(value);
        }

        [JsonProperty(PropertyName = "ID")]
        private string IdString
        {
            get => this.ID.ToString();
            set => this.ID = Guid.Parse(value);
        }

        [JsonProperty(PropertyName = "MapID")]
        private string MapIdString
        {
            get => this.MapID.ToString();
            set => this.MapID = Guid.Parse(value);
        }

        [JsonProperty(PropertyName = "Secret")]
        private string SecretBytes
        {
            get => this.Secret == null ? null : Convert.ToBase64String(this.Secret);
            set => this.Secret = string.IsNullOrEmpty(value) ? null : Convert.FromBase64String(value);
        }

        [DefaultValue(true)]
        [JsonProperty(PropertyName = "CanDraw", DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool CanDraw { get; set; } = true;

        public static ClientInfo Empty { get; } = new ClientInfo()
        {
            ID = Guid.Empty,
            MapID = Guid.Empty,
            Name = "All",
            Color = Color.White,
            IsAdmin = false,
            IsObserver = false,
            CanDraw = false
        };

        public ClientInfo()
        {
        }

        public ClientInfo(BinaryReader br) : this() => this.Read(br);

        [JsonIgnore]
        public Guid ID { get; set; }

        [JsonIgnore]
        public Guid MapID { get; set; }

        public string Name { get; set; } = string.Empty;

        [JsonIgnore]
        public Color Color { get; set; }

        public bool IsAdmin { get; set; }
        public bool IsObserver { get; set; }

        [JsonIgnore]
        public bool IsLoggedOn { get; set; }

        public bool IsBanned { get; set; }

        [JsonIgnore]
        public byte[] Secret { get; set; }

        public void Write(BinaryWriter bw)
        {
            bw.Write(this.ID.ToByteArray());
            bw.Write(this.MapID.ToByteArray());
            bw.Write(this.Name);
            bw.Write(this.Color.Argb());
            bw.Write(this.IsAdmin);
            bw.Write(this.IsObserver);
            bw.Write(this.IsLoggedOn);
            bw.Write(this.IsBanned);
            bw.Write(this.CanDraw);
        }

        public void Read(BinaryReader br)
        {
            this.ID = new Guid(br.ReadBytes(16));
            this.MapID = new Guid(br.ReadBytes(16));
            this.Name = br.ReadString();
            this.Color = Extensions.FromArgb(br.ReadUInt32());
            this.IsAdmin = br.ReadBoolean();
            this.IsObserver = br.ReadBoolean();
            this.IsLoggedOn = br.ReadBoolean();
            this.IsBanned = br.ReadBoolean();
            this.CanDraw = br.ReadBoolean();
        }
    }
}
