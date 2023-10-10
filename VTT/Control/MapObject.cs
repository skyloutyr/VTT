namespace VTT.Control
{
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using VTT.Util;

    public class MapObject : ISerializable
    {
        private Vector3 position = Vector3.Zero;
        private Quaternion rotation = Quaternion.Identity;
        private Vector3 scale = Vector3.One;
        private AABox clientBoundingBox = new AABox(-0.5f, -0.5f, -0.5f, 0.5f, 0.5f, 0.5f);

        public Guid ID { get; set; }

        public bool IsNameVisible { get; set; }
        public bool IsCrossedOut { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public string Notes { get; set; }
        public bool IsServer { get; set; }

        public Guid AssetID { get; set; }
        public Guid ShaderID { get; set; }

        public Guid OwnerID { get; set; }
        public Guid MapID { get; set; }

        public int MapLayer { get; set; }
        public Vector3 Position
        {
            get => this.position;
            set
            {
                this.position = value;
                this.RecalculateModelMatrix();
            }
        }

        public Quaternion Rotation
        {
            get => this.rotation;
            set
            {
                this.rotation = value;
                this.RecalculateModelMatrix();
            }
        }

        public Vector3 Scale
        {
            get => this.scale;
            set
            {
                this.scale = value;
                this.RecalculateModelMatrix();
            }
        }

        public Color TintColor { get; set; } = Color.White;

        public Matrix4 ClientCachedModelMatrix { get; set; } = Matrix4.Identity;

        public DataElement CustomProperties { get; set; } = new DataElement();

        public Map Container { get; set; }

        public List<DisplayBar> Bars { get; set; } = new List<DisplayBar>();
        public List<FastLight> FastLights { get; set; } = new List<FastLight>();
        public List<(float, Color)> Auras { get; set; } = new List<(float, Color)>();

        public object Lock = new object();
        public object FastLightsLock = new object();
        public Dictionary<string, (float, float)> StatusEffects { get; } = new Dictionary<string, (float, float)>();
        public Dictionary<Guid, ParticleContainer> ParticleContainers { get; } = new Dictionary<Guid, ParticleContainer>();

        public bool LightsEnabled { get; set; }
        public bool LightsCastShadows { get; set; }
        public bool LightsSelfCastsShadow { get; set; }
        public bool CastsShadow { get; set; } = true;

        public bool HasCustomNameplate { get; set; }
        public Guid CustomNameplateID { get; set; }
        public bool IsInfoObject { get; set; }
        public bool DoNotRender { get; set; }

        #region Client Data
        public AABox ClientBoundingBox
        {
            get => this.clientBoundingBox;
            set
            {
                this.clientBoundingBox = value;
                this.RecalculateModelMatrix();
            }
        }
        public AABox CameraCullerBox { get; set; }
        public bool ClientAssignedModelBounds { get; set; }

        public bool ClientRenderedThisFrame { get; set; }
        public bool ClientGuiOverlayDrawnThisFrame { get; set; }
        public bool ClientDeferredRejectThisFrame { get; set; }
        public Vector3 ClientDragMoveResetInitialPosition { get; set; }
        public Vector3 ClientDragMoveAccumulatedPosition { get; set; }

        public Quaternion ClientDragRotaateInitialRotation { get; set; }

        public Vector3 ClientDragMoveInitialPosition { get; set; }
        public Vector3 ClientDragMoveServerInducedNewPosition { get; set; }
        public float ClientDragMoveServerInducedPositionChangeProgress { get; set; } = -1;

        public Vector3 ClientDragMoveInitialScale { get; set; }
        public Vector3 ClientDragMoveServerInducedNewScale { get; set; }
        public float ClientDragMoveServerInducedScaleChangeProgress { get; set; } = -1;

        public Quaternion ClientDragMoveInitialRotation { get; set; }
        public Quaternion ClientDragMoveServerInducedNewRotation { get; set; }
        public float ClientDragMoveServerInducedRotationChangeProgress { get; set; } = -1;

        public int ClientRulerRendererAccumData { get; set; } = 0;

        public bool IsRemoved { get; set; }
        #endregion

        public DataElement Serialize()
        {
            DataElement ret = new();
            ret.SetGuid("ID", this.ID);
            ret.Set("Name", this.Name);
            ret.Set("IsNameVisible", this.IsNameVisible);
            ret.Set("IsCrossedOut", this.IsCrossedOut);
            ret.Set("Desc", this.Description);
            ret.Set("Notes", this.Notes);
            ret.SetGuid("AssetID", this.AssetID);
            ret.SetGuid("ShaderID", this.ShaderID);
            ret.SetGuid("OwnerID", this.OwnerID);
            ret.SetGuid("MapID", this.MapID);
            ret.Set("MapLayer", this.MapLayer);
            ret.SetVec3("Position", this.Position);
            ret.SetQuaternion("Rotation", this.Rotation);
            ret.SetVec3("Scale", this.Scale);
            ret.Set("LightsEnabled", this.LightsEnabled);
            ret.Set("LightsCastShadows", this.LightsCastShadows);
            ret.Set("SelfCastsShadow", this.LightsSelfCastsShadow);
            ret.Set("CastsShadow", this.CastsShadow);
            ret.Set("NoDraw", this.DoNotRender);
            ret.SetColor("TintColor", this.TintColor);
            ret.SetArray("Bars", this.Bars.ToArray(), (n, c, v) => c.Set(n, v.Serialize()));
            ret.SetArray("Auras", this.Auras.ToArray(), (n, c, v) =>
            {
                DataElement e = new DataElement();
                e.Set("r", v.Item1);
                e.SetColor("c", v.Item2);
                c.Set(n, e);
            });

            ret.SetArray("FastLights", this.FastLights.ToArray(), (n, c, v) => c.Set(n, v.Serialize()));
            ret.SetArray("Statuses", this.StatusEffects.ToArray(), (n, c, v) =>
            {
                DataElement e = new DataElement();
                e.Set("n", v.Key);
                e.Set("s", v.Value.Item1);
                e.Set("t", v.Value.Item2);
                c.Set(n, e);
            });

            ret.SetArray("Particles", this.ParticleContainers.Values.ToArray(), (n, c, v) => c.Set(n, v.Serialize()));
            ret.Set("HasCustomNameplate", this.HasCustomNameplate);
            ret.SetGuid("CustomNameplate", this.CustomNameplateID);
            ret.Set("IsInfoObject", this.IsInfoObject);
            ret.Set("Props", this.CustomProperties);
            return ret;
        }


        public void Deserialize(DataElement e)
        {
            this.ID = e.GetGuid("ID");
            this.Name = e.Get<string>("Name");
            this.IsNameVisible = e.Get<bool>("IsNameVisible");
            this.IsCrossedOut = e.Get<bool>("IsCrossedOut");
            this.Description = e.Get<string>("Desc");
            this.Notes = e.Get<string>("Notes");
            this.AssetID = e.GetGuid("AssetID");
            this.ShaderID = e.GetGuid("ShaderID");
            this.OwnerID = e.GetGuid("OwnerID");
            this.MapID = e.GetGuid("MapID");
            this.MapLayer = e.Get<int>("MapLayer");
            this.Position = e.GetVec3("Position", Vector3.Zero);
            this.Rotation = e.GetQuaternion("Rotation", Quaternion.Identity);
            this.Scale = e.GetVec3("Scale", Vector3.One);
            this.LightsEnabled = e.Get<bool>("LightsEnabled");
            this.LightsCastShadows = e.Get<bool>("LightsCastShadows");
            this.LightsSelfCastsShadow = e.Get<bool>("SelfCastsShadow");
            this.CastsShadow = e.Get("CastsShadow", true);
            this.DoNotRender = e.Get("NoDraw", false);
            this.TintColor = e.GetColor("TintColor", Color.White);
            this.Bars.Clear();
            this.Bars.AddRange(e.GetArray("Bars", (n, e) => DisplayBar.FromData(e.Get<DataElement>(n)), Array.Empty<DisplayBar>()));
            this.FastLights.Clear();
            this.FastLights.AddRange(e.GetArray("FastLights", (n, e) => FastLight.FromData(e.Get<DataElement>(n)), Array.Empty<FastLight>()));
            this.Auras.Clear();
            this.Auras.AddRange(e.GetArray("Auras", (n, e) =>
            {
                DataElement d = e.Get<DataElement>(n);
                return (d.Get<float>("r"), d.GetColor("c"));
            }, Array.Empty<(float, Color)>()));

            this.StatusEffects.Clear();
            (string, float, float)[] stats = e.GetArray("Statuses", (n, e) =>
            {
                DataElement d = e.Get<DataElement>(n);
                return (d.Get<string>("n"), d.Get<float>("s"), d.Get<float>("t"));
            }, Array.Empty<(string, float, float)>());

            foreach ((string, float, float) s in stats)
            {
                this.StatusEffects[s.Item1] = (s.Item2, s.Item3);
            }

            this.ParticleContainers.Clear();
            ParticleContainer[] containers = e.GetArray("Particles", (n, e) =>
            {
                DataElement d = e.Get<DataElement>(n);
                ParticleContainer ret = new ParticleContainer(this);
                ret.Deserialize(d);
                return ret;
            }, Array.Empty<ParticleContainer>());

            foreach (ParticleContainer c in containers)
            {
                this.ParticleContainers[c.ID] = c;
            }

            this.HasCustomNameplate = e.Get<bool>("HasCustomNameplate", false);
            this.CustomNameplateID = e.GetGuid("CustomNameplate", Guid.Empty);
            this.IsInfoObject = e.Get<bool>("IsInfoObject");
            this.CustomProperties = e.Get<DataElement>("Props");
        }

        public MapObject Clone()
        {
            MapObject ret = new MapObject();
            ret.ID = Guid.NewGuid();
            ret.Name = this.Name;
            ret.IsNameVisible = this.IsNameVisible;
            ret.Description = this.Description;
            ret.Notes = this.Notes;
            ret.AssetID = this.AssetID;
            ret.ShaderID = this.ShaderID;
            ret.OwnerID = this.OwnerID;
            ret.MapID = this.MapID;
            ret.MapLayer = this.MapLayer;
            ret.Position = this.Position;
            ret.Rotation = this.Rotation;
            ret.Scale = this.Scale;
            ret.TintColor = this.TintColor;
            ret.LightsEnabled = this.LightsEnabled;
            ret.LightsCastShadows = this.LightsCastShadows;
            ret.LightsSelfCastsShadow = this.LightsSelfCastsShadow;
            ret.CastsShadow = this.CastsShadow;
            ret.Bars.AddRange(this.Bars.Select(x => x.Clone()));
            ret.Auras.AddRange(this.Auras.Select(x => (x.Item1, x.Item2)));
            ret.FastLights.AddRange(this.FastLights.Select(x => x.Clone()));
            ret.IsCrossedOut = this.IsCrossedOut;
            ret.HasCustomNameplate = this.HasCustomNameplate;
            ret.CustomNameplateID = this.CustomNameplateID;
            ret.IsInfoObject = this.IsInfoObject;
            ret.DoNotRender = this.DoNotRender;
            ret.CustomProperties = new DataElement();
            foreach (KeyValuePair<string, (float, float)> s in this.StatusEffects)
            {
                ret.StatusEffects.Add(s.Key, s.Value);
            }

            foreach (KeyValuePair<Guid, ParticleContainer> p in this.ParticleContainers)
            {
                Guid nID = Guid.NewGuid();
                ParticleContainer p1 = new ParticleContainer(ret) { AttachmentPoint = p.Value.AttachmentPoint, ContainerPositionOffset = p.Value.ContainerPositionOffset, IsActive = p.Value.IsActive, UseContainerOrientation = p.Value.UseContainerOrientation, SystemID = p.Value.SystemID, ID = nID };
                ret.ParticleContainers[nID] = p1;
            }

            return ret;
        }

        public bool CanEdit(Guid playerID) => this.OwnerID.Equals(Guid.Empty) || playerID.Equals(this.OwnerID);

        public void RecalculateModelMatrix()
        {
            this.ClientCachedModelMatrix = Matrix4.CreateScale(this.scale) * Matrix4.CreateFromQuaternion(this.rotation) * Matrix4.CreateTranslation(this.position);
            this.CameraCullerBox = new BBBox(this.ClientBoundingBox, this.Rotation).Scale(this.Scale).GetBounds();
        }

        public void Update(double delta) // Client-only
        {
            if (this.ClientDragMoveServerInducedPositionChangeProgress > 0)
            {
                this.ClientDragMoveServerInducedPositionChangeProgress -= (float)delta;
                Vector3 nPos = Vector3.Lerp(this.ClientDragMoveServerInducedNewPosition, this.ClientDragMoveInitialPosition, this.ClientDragMoveServerInducedPositionChangeProgress);
                this.Position = nPos;
                if (this.ClientDragMoveServerInducedPositionChangeProgress <= 0)
                {
                    this.Position = this.ClientDragMoveServerInducedNewPosition;
                    this.ClientDragMoveServerInducedPositionChangeProgress = -1;
                }
            }

            if (this.ClientDragMoveServerInducedScaleChangeProgress > 0)
            {
                this.ClientDragMoveServerInducedScaleChangeProgress -= (float)delta;
                Vector3 nPos = Vector3.Lerp(this.ClientDragMoveServerInducedNewScale, this.ClientDragMoveInitialScale, this.ClientDragMoveServerInducedScaleChangeProgress);
                this.Scale = nPos;
                if (this.ClientDragMoveServerInducedScaleChangeProgress <= 0)
                {
                    this.Scale = this.ClientDragMoveServerInducedNewScale;
                    this.ClientDragMoveServerInducedScaleChangeProgress = -1;
                }
            }

            if (this.ClientDragMoveServerInducedRotationChangeProgress > 0)
            {
                this.ClientDragMoveServerInducedRotationChangeProgress -= (float)delta;
                Quaternion nPos = Quaternion.Slerp(this.ClientDragMoveServerInducedNewRotation, this.ClientDragMoveInitialRotation, this.ClientDragMoveServerInducedRotationChangeProgress);
                this.Rotation = nPos;
                if (this.ClientDragMoveServerInducedRotationChangeProgress <= 0)
                {
                    this.Rotation = this.ClientDragMoveServerInducedNewRotation;
                    this.ClientDragMoveServerInducedRotationChangeProgress = -1;
                }
            }
        }
    }

    public class DisplayBar : ISerializable
    {
        public float CurrentValue { get; set; }
        public float MaxValue { get; set; }
        public Color DrawColor { get; set; }
        public bool Compact { get; set; }

        public void Deserialize(DataElement e)
        {
            this.CurrentValue = e.Get<float>("Value");
            this.MaxValue = e.Get<float>("Max");
            this.DrawColor = e.GetColor("Color");
            this.Compact = e.Get<bool>("Compact");
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.Set("Value", this.CurrentValue);
            ret.Set("Max", this.MaxValue);
            ret.SetColor("Color", this.DrawColor);
            ret.Set("Compact", this.Compact);
            return ret;
        }

        public DisplayBar Clone()
        {
            return new DisplayBar()
            {
                CurrentValue = this.CurrentValue,
                MaxValue = this.MaxValue,
                DrawColor = this.DrawColor,
                Compact = this.Compact
            };
        }

        public static DisplayBar FromData(DataElement de)
        {
            DisplayBar ret = new DisplayBar();
            ret.Deserialize(de);
            return ret;
        }
    }
}
