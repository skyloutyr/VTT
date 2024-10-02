namespace VTT.Control
{
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using VTT.Asset.Glb;
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
        public Color NameColor { get; set; } = Color.Transparent;

        public Matrix4x4 ClientCachedModelMatrix { get; set; } = Matrix4x4.Identity;

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
        public bool UseMarkdownForDescription { get; set; }

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

        public AABox ClientRaycastBox { get; set; }

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
        public AnimationContainer AnimationContainer { get; } = new AnimationContainer();
        public GlbScene LastRenderModel { get; set; }

        public bool IsRemoved { get; set; }
        #endregion

        public DataElement Serialize()
        {
            DataElement ret = new();
            ret.SetGuid("ID", this.ID);
            ret.SetString("Name", this.Name);
            ret.SetBool("IsNameVisible", this.IsNameVisible);
            ret.SetBool("IsCrossedOut", this.IsCrossedOut);
            ret.SetString("Desc", this.Description);
            ret.SetString("Notes", this.Notes);
            ret.SetGuid("AssetID", this.AssetID);
            ret.SetGuid("ShaderID", this.ShaderID);
            ret.SetGuid("OwnerID", this.OwnerID);
            ret.SetGuid("MapID", this.MapID);
            ret.SetInt("MapLayer", this.MapLayer);
            ret.SetVec3("Position", this.Position);
            ret.SetQuaternion("Rotation", this.Rotation);
            ret.SetVec3("Scale", this.Scale);
            ret.SetBool("LightsEnabled", this.LightsEnabled);
            ret.SetBool("LightsCastShadows", this.LightsCastShadows);
            ret.SetBool("SelfCastsShadow", this.LightsSelfCastsShadow);
            ret.SetBool("CastsShadow", this.CastsShadow);
            ret.SetBool("NoDraw", this.DoNotRender);
            ret.SetColor("TintColor", this.TintColor);
            ret.SetColor("NameColor", this.NameColor);
            ret.SetArray("Bars", this.Bars.ToArray(), (n, c, v) => c.SetMap(n, v.Serialize()));
            ret.SetArray("Auras", this.Auras.ToArray(), (n, c, v) =>
            {
                DataElement e = new DataElement();
                e.SetSingle("r", v.Item1);
                e.SetColor("c", v.Item2);
                c.SetMap(n, e);
            });

            ret.SetArray("FastLights", this.FastLights.ToArray(), (n, c, v) => c.SetMap(n, v.Serialize()));
            ret.SetArray("Statuses", this.StatusEffects.ToArray(), (n, c, v) =>
            {
                DataElement e = new DataElement();
                e.SetString("n", v.Key);
                e.SetSingle("s", v.Value.Item1);
                e.SetSingle("t", v.Value.Item2);
                c.SetMap(n, e);
            });

            ret.SetArray("Particles", this.ParticleContainers.Values.ToArray(), (n, c, v) => c.SetMap(n, v.Serialize()));
            ret.SetBool("HasCustomNameplate", this.HasCustomNameplate);
            ret.SetGuid("CustomNameplate", this.CustomNameplateID);
            ret.SetBool("IsInfoObject", this.IsInfoObject);
            ret.SetMap("Props", this.CustomProperties);
            ret.SetMap("AnimationData", this.AnimationContainer.Serialize());
            ret.SetBool("DescMarkdown", this.UseMarkdownForDescription);
            return ret;
        }


        public void Deserialize(DataElement e)
        {
            this.ID = e.GetGuid("ID");
            this.Name = e.GetString("Name");
            this.IsNameVisible = e.GetBool("IsNameVisible");
            this.IsCrossedOut = e.GetBool("IsCrossedOut");
            this.Description = e.GetString("Desc");
            this.Notes = e.GetString("Notes");
            this.AssetID = e.GetGuid("AssetID");
            this.ShaderID = e.GetGuid("ShaderID");
            this.OwnerID = e.GetGuid("OwnerID");
            this.MapID = e.GetGuid("MapID");
            this.MapLayer = e.GetInt("MapLayer");
            this.Position = e.GetVec3("Position", Vector3.Zero);
            this.Rotation = e.GetQuaternion("Rotation", Quaternion.Identity);
            this.Scale = e.GetVec3("Scale", Vector3.One);
            this.LightsEnabled = e.GetBool("LightsEnabled");
            this.LightsCastShadows = e.GetBool("LightsCastShadows");
            this.LightsSelfCastsShadow = e.GetBool("SelfCastsShadow");
            this.CastsShadow = e.GetBool("CastsShadow", true);
            this.DoNotRender = e.GetBool("NoDraw", false);
            this.TintColor = e.GetColor("TintColor", Color.White);
            this.NameColor = e.GetColor("NameColor", Color.Transparent);
            this.Bars.Clear();
            this.Bars.AddRange(e.GetArray("Bars", (n, e) => DisplayBar.FromData(e.GetMap(n)), Array.Empty<DisplayBar>()));
            this.FastLights.Clear();
            this.FastLights.AddRange(e.GetArray("FastLights", (n, e) => FastLight.FromData(e.GetMap(n)), Array.Empty<FastLight>()));
            this.Auras.Clear();
            this.Auras.AddRange(e.GetArray("Auras", (n, e) =>
            {
                DataElement d = e.GetMap(n);
                return (d.GetSingle("r"), d.GetColor("c"));
            }, Array.Empty<(float, Color)>()));

            this.StatusEffects.Clear();
            (string, float, float)[] stats = e.GetArray("Statuses", (n, e) =>
            {
                DataElement d = e.GetMap(n);
                return (d.GetString("n"), d.GetSingle("s"), d.GetSingle("t"));
            }, Array.Empty<(string, float, float)>());

            foreach ((string, float, float) s in stats)
            {
                this.StatusEffects[s.Item1] = (s.Item2, s.Item3);
            }

            this.ParticleContainers.Clear();
            ParticleContainer[] containers = e.GetArray("Particles", (n, e) =>
            {
                DataElement d = e.GetMap(n);
                ParticleContainer ret = new ParticleContainer(this);
                ret.Deserialize(d);
                return ret;
            }, Array.Empty<ParticleContainer>());

            foreach (ParticleContainer c in containers)
            {
                this.ParticleContainers[c.ID] = c;
            }

            this.HasCustomNameplate = e.GetBool("HasCustomNameplate", false);
            this.CustomNameplateID = e.GetGuid("CustomNameplate", Guid.Empty);
            this.IsInfoObject = e.GetBool("IsInfoObject");
            this.CustomProperties = e.GetMap("Props");
            this.AnimationContainer.Deserialize(e.GetMap("AnimationData", new DataElement()));
            this.UseMarkdownForDescription = e.GetBool("DescMarkdown", false);
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
            ret.NameColor = this.NameColor;
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
            ret.UseMarkdownForDescription = this.UseMarkdownForDescription;
            ret.CustomProperties = new DataElement();
            foreach (KeyValuePair<string, (float, float)> s in this.StatusEffects)
            {
                ret.StatusEffects.Add(s.Key, s.Value);
            }

            foreach (KeyValuePair<Guid, ParticleContainer> p in this.ParticleContainers)
            {
                Guid nID = Guid.NewGuid();
                ParticleContainer p1 = new ParticleContainer(ret) { AttachmentPoint = p.Value.AttachmentPoint, ContainerPositionOffset = p.Value.ContainerPositionOffset, IsActive = p.Value.IsActive, UseContainerOrientation = p.Value.UseContainerOrientation, RotateVelocityByOrientation = p.Value.RotateVelocityByOrientation, SystemID = p.Value.SystemID, ID = nID };
                ret.ParticleContainers[nID] = p1;
            }

            return ret;
        }

        public bool CanEdit(Guid playerID) => this.OwnerID.Equals(Guid.Empty) || playerID.Equals(this.OwnerID);

        public void RecalculateModelMatrix()
        {
            this.ClientCachedModelMatrix = Matrix4x4.CreateScale(this.scale) * Matrix4x4.CreateFromQuaternion(this.rotation) * Matrix4x4.CreateTranslation(this.position);
            this.CameraCullerBox = new BBBox(this.ClientBoundingBox, this.Rotation).Scale(this.Scale).GetBounds();
        }

        public void Update() // Client-only
        {
            if (this.ClientRenderedThisFrame) // TODO test if the animatio needs to be updated regardless of object's render visibility
            {
                this.AnimationContainer.Update(this.LastRenderModel);
            }

            if (this.ClientDragMoveServerInducedPositionChangeProgress > 0)
            {
                this.ClientDragMoveServerInducedPositionChangeProgress -= (1f / 60f);
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
                this.ClientDragMoveServerInducedScaleChangeProgress -= (1f / 60f);
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
                this.ClientDragMoveServerInducedRotationChangeProgress -= (1f / 60f);
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
            this.CurrentValue = e.GetSingle("Value");
            this.MaxValue = e.GetSingle("Max");
            this.DrawColor = e.GetColor("Color");
            this.Compact = e.GetBool("Compact");
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetSingle("Value", this.CurrentValue);
            ret.SetSingle("Max", this.MaxValue);
            ret.SetColor("Color", this.DrawColor);
            ret.SetBool("Compact", this.Compact);
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

    public interface IAnimationStorage
    {
        public class BoneData
        {
            public int Index { get; set; }
            public Matrix4x4 Transform { get; set; }

            public void CopyFrom(GlbBone bone) => this.Transform = bone.Transform;
        }

        public bool CheckAnimation(GlbAnimation anim, GlbArmature arm);
        public void LoadBonesFromAnimation(GlbArmature arm);
        public IEnumerable<BoneData> ProvideBones();
    }

    public class AnimationContainer : ISerializable, IAnimationStorage
    {
        private float _time;

        public string LoopingAnimationName { get; set; }
        public bool Paused { get; set; } = false;
        public float AnimationPlayRate { get; set; } = 1.0f;

        public GlbAnimation CurrentAnimation { get; set; }

        private GlbAnimation _lastStorageAnimation;
        public readonly object animationStorageLock = new object();
        public List<IAnimationStorage.BoneData> StoredBoneData { get; } = new List<IAnimationStorage.BoneData>();

        const float TimeScale = 1f / 60.0f;
        public float GetTime(double delta) => this.Paused ? this._time : this._time - (TimeScale * this.AnimationPlayRate) + ((float)delta * (TimeScale * this.AnimationPlayRate));

        public void SwitchNow(GlbScene model, string to)
        {
            GlbAnimation anim = model.Animations.Find(x => x.Name.Equals(to));
            if (anim != null)
            {
                this.CurrentAnimation = anim;
                this._time = this.AnimationPlayRate > 0 ? 0 : anim.Duration;
            }
        }

        public void Update(GlbScene model)
        {
            if (model == null)
            {
                this._time = 0;
                this.CurrentAnimation = null;
                return;
            }

            if (!model.IsAnimated)
            {
                return;
            }

            if (this.CurrentAnimation != null && this.CurrentAnimation.Container != model)
            {
                this._time = 0;
                this.CurrentAnimation = null;
                return;
            }

            if (this.CurrentAnimation != null)
            {
                if (!this.Paused)
                {
                    this._time += TimeScale * this.AnimationPlayRate;
                }

                if ((this.AnimationPlayRate > 0 && this._time > this.CurrentAnimation.Duration) || (this._time < 0))
                {
                    if (!this.CurrentAnimation.Name.Equals(this.LoopingAnimationName))
                    {
                        GlbAnimation anim = model.Animations.Find(x => x.Name.Equals(this.LoopingAnimationName));
                        this.CurrentAnimation = anim ?? (model.Animations.Count > 0 ? model.Animations[0] : null);
                        this._time = this.AnimationPlayRate > 0 ? 0 : this.CurrentAnimation.Duration;
                    }
                    else
                    {
                        this._time = this.AnimationPlayRate > 0 ? 0 : this.CurrentAnimation.Duration;
                    }
                }
            }
            else
            {
                if (model.IsAnimated)
                {
                    if (string.IsNullOrEmpty(this.LoopingAnimationName))
                    {
                        this.CurrentAnimation = model.Animations.Count > 0 ? model.Animations[0] : null;
                        this.LoopingAnimationName = this.CurrentAnimation?.Name ?? string.Empty;
                        this._time = this.CurrentAnimation == null ? 0 : this.AnimationPlayRate > 0 ? 0 : this.CurrentAnimation.Duration;
                    }
                    else
                    {
                        GlbAnimation anim = model.Animations.Find(x => x.Name.Equals(this.LoopingAnimationName));
                        this.CurrentAnimation = anim ?? (model.Animations.Count > 0 ? model.Animations[0] : null);
                        this._time = this.CurrentAnimation == null ? 0 : this.AnimationPlayRate > 0 ? 0 : this.CurrentAnimation.Duration;
                    }
                }
            }
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetString("Default", this.LoopingAnimationName);
            ret.SetBool("Paused", this.Paused);
            ret.SetSingle("PlayRate", this.AnimationPlayRate);
            return ret;
        }

        public void Deserialize(DataElement e)
        {
            this.LoopingAnimationName = e.GetString("Default", string.Empty);
            this.Paused = e.GetBool("Paused", false);
            this.AnimationPlayRate = e.GetSingle("PlayRate", 1.0f);
        }

        public bool CheckAnimation(GlbAnimation anim, GlbArmature arm)
        {
            if (anim != this._lastStorageAnimation)
            {
                this._lastStorageAnimation = anim;
                lock (this.animationStorageLock)
                {
                    this.StoredBoneData.Clear();
                    foreach (GlbBone bone in arm.UnsortedBones)
                    {
                        IAnimationStorage.BoneData bd = new IAnimationStorage.BoneData()
                        {
                            Index = bone.ModelIndex,
                            Transform = bone.Transform
                        };

                        this.StoredBoneData.Add(bd);
                    }

                }

                return false;
            }

            return true;
        }

        public void LoadBonesFromAnimation(GlbArmature arm)
        {
            lock (this.animationStorageLock)
            {
                for (int i = 0; i < this.StoredBoneData.Count; i++)
                {
                    IAnimationStorage.BoneData bd = this.StoredBoneData[i];
                    bd.Transform = arm.UnsortedBones[i].Transform;
                }
            }
        }

        public IEnumerable<IAnimationStorage.BoneData> ProvideBones()
        {
            return this.StoredBoneData;
        }
    }
}
