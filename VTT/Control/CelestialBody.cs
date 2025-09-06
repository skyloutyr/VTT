namespace VTT.Control
{
    using System;
    using System.Numerics;
    using VTT.Util;

    public sealed class CelestialBody : ISerializable
    {
        public Guid OwnID { get; set; }
        public bool IsSun { get; set; } // Special case, the sun can't be deleted, is the source of DL

        // Sun specific properties
        public ShadowCastingPolicy ShadowPolicy { get; set; } = ShadowCastingPolicy.Normal; // A sun may cast no shadows, have the world be always shaded, or behave normally
        public MapSkyboxColors LightColor { get; set; } = new MapSkyboxColors(); // Color for the sunlight, interpolated over time of day

        // Generic properies
        public PositionPolicy PositionKind { get; set; } = PositionPolicy.Angular; // Either own explicit pitch/yaw, or they are offsets

        // Euler's angles
        public Vector3 Position { get; set; } = Vector3.Zero;
        public Vector3 Rotation { get; set; } = Vector3.Zero;
        public Vector3 Scale { get; set; } = Vector3.One;

        public bool Enabled { get; set; } = true;
        public RenderPolicy RenderKind { get; set; } = RenderPolicy.BuiltInSun;
        public bool Billboard { get; set; } = true;
        public bool UseOwnTime { get; set; } = true;
        public Guid AssetRef { get; set; } // For CustomImage/CustomModel
        public MapSkyboxColors OwnColor { get; set; } = new MapSkyboxColors(); // Own color mod, interpolated over own position

        public CelestialBody()
        {
        }

        public float SunYaw
        {
            get => this.Position.X;
            set => this.Position = new Vector3(value, this.Position.Y, this.Position.Z);
        }

        public float SunPitch
        {
            get => this.Position.Y;
            set => this.Position = new Vector3(this.Position.X, value, this.Position.Z);
        }

        public float SunRoll
        {
            get => this.Position.Z;
            set => this.Position = new Vector3(this.Position.X, this.Position.Y, value);
        }

        public CelestialBody(DataElement e) : this() => this.Deserialize(e);

        public void Deserialize(DataElement e)
        {
            this.OwnID = e.GetGuid("ID");
            this.IsSun = e.GetBool("IsSun");
            this.ShadowPolicy = e.GetEnum<ShadowCastingPolicy>("Shadows");
            this.LightColor.Deserialize(e.GetMap("Light"));
            this.PositionKind = e.GetEnum<PositionPolicy>("PositionKind");
            this.Position = e.GetVec3("Position");
            this.Rotation = e.GetVec3("Rotation");
            this.Scale = e.GetVec3("Scale");
            this.Enabled = e.GetBool("Enabled");
            this.Billboard = e.GetBool("Billboard");
            this.UseOwnTime = e.GetBool("OwnTime", true);
            this.RenderKind = e.GetEnum<RenderPolicy>("Render");
            this.AssetRef = e.GetGuid("Asset");
            this.OwnColor.Deserialize(e.GetMap("Color"));
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("ID", this.OwnID);
            ret.SetBool("IsSun", this.IsSun);
            ret.SetEnum("Shadows", this.ShadowPolicy);
            ret.SetMap("Light", this.LightColor.Serialize());
            ret.SetEnum("PositionKind", this.PositionKind);
            ret.SetVec3("Position", this.Position);
            ret.SetVec3("Rotation", this.Rotation);
            ret.SetVec3("Scale", this.Scale);
            ret.SetBool("Enabled", this.Enabled);
            ret.SetBool("Billboard", this.Billboard);
            ret.SetBool("OwnTime", this.UseOwnTime);
            ret.SetEnum("Render", this.RenderKind);
            ret.SetGuid("Asset", this.AssetRef);
            ret.SetMap("Color", this.OwnColor.Serialize());
            return ret;
        }

        public CelestialBody Clone()
        {
            CelestialBody ret = new CelestialBody();
            ret.OwnID = this.OwnID;
            ret.IsSun = this.IsSun;
            ret.ShadowPolicy = this.ShadowPolicy;
            ret.LightColor = this.LightColor.Clone();
            ret.PositionKind = this.PositionKind;
            ret.Position = this.Position;
            ret.Rotation = this.Rotation;
            ret.Scale = this.Scale;
            ret.Enabled = this.Enabled;
            ret.RenderKind = this.RenderKind;
            ret.Billboard = this.Billboard;
            ret.AssetRef = this.AssetRef;
            ret.OwnColor = this.OwnColor.Clone();
            return ret;
        }

        public enum ShadowCastingPolicy
        {
            Normal,
            Always,
            Never
        }

        public enum PositionPolicy
        {
            Angular,
            OpposesSun,
            FollowsSun,
            Static
        }

        public enum RenderPolicy
        {
            BuiltInSun,
            BuiltInMoon,
            BuiltInPlanetA,
            BuiltInPlanetB,
            BuiltInPlanetC,
            BuiltInPlanetD,
            BuiltInPlanetE,
            Custom
        }

        public static CelestialBody CreateSun(Guid id)
        {
            return new CelestialBody()
            {
                OwnID = id,
                IsSun = true,
                ShadowPolicy = ShadowCastingPolicy.Normal,
                LightColor = new MapSkyboxColors()
                {
                    OwnType = MapSkyboxColors.ColorsPointerType.DefaultSunlight,
                },

                PositionKind = PositionPolicy.Angular,
                Position = Vector3.Zero,
                Rotation = Vector3.Zero,
                Scale = new Vector3(8, 8, 8),
                Enabled = true,
                RenderKind = RenderPolicy.BuiltInSun,
                Billboard = true,
                AssetRef = Guid.Empty,
                OwnColor = new MapSkyboxColors()
                {
                    OwnType = MapSkyboxColors.ColorsPointerType.DefaultSun
                }
            };
        }
    }
}
