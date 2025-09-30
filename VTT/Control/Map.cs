namespace VTT.Control
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using VTT.Asset;
    using VTT.Network;
    using VTT.Util;

    public class Map : ISerializable
    {
        public readonly object Lock = new object();

        public Guid ID { get; set; }
        public bool IsServer { get; set; }
        public bool IsDeleted { get; set; } = false;
        public string Name { get; set; }
        public string Folder { get; set; } = string.Empty;
        public bool GridEnabled { get; set; } = true;
        public bool GridDrawn { get; set; } = true;
        public float GridSize { get; set; } = 1;
        public float GridUnit { get; set; } = 5;
        public Color GridColor { get; set; }
        public MapGridType GridType { get; set; } = MapGridType.Square;
        public Color AmbientColor { get; set; }
        public Color SunColor { get; set; }

        public float SunIntensity { get; set; } = 1.0f;
        public float AmbientIntensity { get; set; } = 1.0f;

        public bool EnablePointShadows { get; set; } = false;
        public bool EnableDarkvision { get; set; }
        public bool EnableDrawing { get; set; } = true;
        public bool Has2DShadows { get; set; } = false;
        public bool Shadows2DObjectVisionStrict { get; set; } = false;

        public bool Is2D { get; set; }
        public float Camera2DHeight { get; set; } = 5.0f;
        public Guid AmbientSoundID { get; set; } = Guid.Empty;
        public float AmbientSoundVolume { get; set; } = 1.0f;

        public Vector3 DefaultCameraPosition { get; set; } = new Vector3(5, 5, 5);
        public Vector3 DefaultCameraRotation { get; set; } = new Vector3(-1, -1, -1).Normalized();

        // Server-only
        public FOWCanvas FOW { get; set; }

        public TurnTracker TurnTracker { get; set; }
        public Map2DShadowLayer ShadowLayer2D { get; } = new Map2DShadowLayer();

        public List<RulerInfo> PermanentMarks { get; } = new List<RulerInfo>();
        public List<DrawingPointContainer> Drawings { get; } = new List<DrawingPointContainer>();

        private readonly List<MapObject> Objects = new List<MapObject>();
        private readonly Dictionary<Guid, MapObject> ObjectsByID = new Dictionary<Guid, MapObject>();

        public Dictionary<Guid, (Guid, float)> DarkvisionData { get; } = new Dictionary<Guid, (Guid, float)>();

        public Guid DaySkyboxAssetID { get; set; } = Guid.Empty;
        public MapSkyboxColors DaySkyboxColors { get; set; } = new MapSkyboxColors();
        public Guid NightSkyboxAssetID { get; set; } = Guid.Empty;
        public MapSkyboxColors NightSkyboxColors { get; set; } = new MapSkyboxColors();
        public MapCelestialBodies CelestialBodies { get; set; } = new MapCelestialBodies();

        public bool NeedsSave { get; set; }

        public Map() => this.TurnTracker = new TurnTracker(this);

        public void AddObject(MapObject obj)
        {
            lock (this.Lock)
            {
                this.Objects.Add(obj);
                this.Objects.Sort((l, r) => l.AssetID.CompareTo(r.AssetID));
                obj.Container = this;
                obj.MapID = this.ID;
                obj.IsServer = this.IsServer;
                this.ObjectsByID[obj.ID] = obj;
                this.NeedsSave = true;
                obj.IsRemoved = false;
                if (!this.IsServer)
                {
                    obj.Particles.UploadAllConainers();
                }
            }
        }

        public void RemoveObject(MapObject obj)
        {
            lock (this.Lock)
            {
                obj.Particles.MarkContainersForDestructionWithoutClearing();
                this.Objects.Remove(obj);
                obj.Container = null;
                obj.MapID = Guid.Empty;
                obj.IsRemoved = true;
                this.ObjectsByID.Remove(obj.ID);
                this.NeedsSave = true;
            }
        }

        public bool GetObject(Guid id, out MapObject mo)
        {
            lock (this.Lock)
            {
                return this.ObjectsByID.TryGetValue(id, out mo);
            }
        }

        public IEnumerable<MapObject> IterateObjects(int? layer)
        {
            lock (this.Lock)
            {
                for (int i = this.Objects.Count - 1; i >= 0; i--)
                {
                    MapObject mo = this.Objects[i];
                    if (!layer.HasValue || mo.MapLayer == layer.Value)
                    {
                        yield return mo;
                    }
                }
            }

            yield break;
        }

        public void MapObjects(int? layer, Action<int, MapObject> action)
        {
            lock (this.Lock)
            {
                for (int i = this.Objects.Count - 1; i >= 0; i--)
                {
                    MapObject mo = this.Objects[i];
                    if (!layer.HasValue || mo.MapLayer == layer.Value)
                    {
                        action(i, mo);
                    }
                }
            }
        }

        public int ObjectCountUnsafe => this.Objects.Count;

        private int _turnTrackerPulseCounter;
        public void Update()
        {
            this.ShadowLayer2D.Update(this);
            if (++this._turnTrackerPulseCounter >= 60)
            {
                this._turnTrackerPulseCounter = 0;
                this.TurnTracker.Pulse();
            }

            lock (this.Lock)
            {
                for (int i = this.Objects.Count - 1; i >= 0; i--)
                {
                    MapObject mo = this.Objects[i];
                    if (!mo.IsRemoved)
                    {
                        mo.Update();
                    }
                    else
                    {
                        
                        continue;
                    }
                }
            }
        }

        public void Deserialize(DataElement e)
        {
            this.ID = e.GetGuidLegacy("ID");
            this.Name = e.GetString("Name");
            this.Folder = e.GetString("Folder", string.Empty);
            this.GridEnabled = e.GetBool("GridEnabled");
            this.GridDrawn = e.GetBool("GridDrawn");
            this.GridSize = e.GetSingle("GridSize");
            this.GridUnit = e.GetSingle("GridUnit", 5.0f);
            this.GridColor = e.GetColor("GridColor");
            this.GridType = e.GetEnum("GridType", MapGridType.Square);
            this.SunColor = e.GetColor("SunColor", new Color(new Rgba32(0.2f, 0.2f, 0.2f, 1.0f)));
            this.AmbientColor = e.GetColor("AmbientColor", new Color(new Rgba32(0.03f, 0.03f, 0.03f, 0.03f)));
            this.SunIntensity = e.GetSingle("SunIntensity");
            this.AmbientIntensity = e.GetSingle("AmbietIntensity", 1.0f);
            this.TurnTracker.Deserialize(e.GetMap("TurnTracker", new DataElement()));
            this.EnablePointShadows = e.GetBool("EnableDirectionalShadows");
            this.EnableDarkvision = e.GetBool("EnableDarkvision");
            this.DefaultCameraPosition = e.GetVec3Legacy("DefaultCameraPosition", this.DefaultCameraPosition);
            this.DefaultCameraRotation = e.GetVec3Legacy("DefaultCameraRotation", this.DefaultCameraRotation);
            if (this.DefaultCameraPosition.HasAnyNans())
            {
                this.DefaultCameraPosition = new Vector3(5, 5, 5);
            }

            if (this.DefaultCameraRotation.HasAnyNans())
            {
                this.DefaultCameraRotation = new Vector3(-1, -1, -1).Normalized();
            }

            this.EnableDrawing = e.GetBool("EnableDrawing", true);
            this.Has2DShadows = e.GetBool("Has2DShadows", false);
            this.Shadows2DObjectVisionStrict = e.GetBool("Strict2DShadows", false);
            (Guid, Guid, float)[] dvData = e.GetPrimitiveArrayWithLegacySupport("DarkvisionData", (n, c) =>
            {
                DataElement de = c.GetMap(n);
                return (de.GetGuidLegacy("k"), de.GetGuidLegacy("o"), de.GetSingle("v"));
            }, Array.Empty<(Guid, Guid, float)>());
            this.DarkvisionData.Clear();
            foreach ((Guid, Guid, float) kv in dvData)
            {
                this.DarkvisionData[kv.Item1] = (kv.Item2, kv.Item3);
            }

            this.PermanentMarks.Clear();
            this.PermanentMarks.AddRange(e.GetArray("PermanentMarks", (n, c) =>
            {
                DataElement de = c.GetMap(n);
                RulerInfo ri = new RulerInfo();
                ri.Deserialize(de);
                return ri;
            }, Array.Empty<RulerInfo>()));

            this.Drawings.Clear();
            this.Drawings.AddRange(e.GetArray("Drawings", (n, c) =>
            {
                DataElement de = c.GetMap(n);
                DrawingPointContainer dpc = new DrawingPointContainer(Guid.Empty, Guid.Empty, 0, Vector4.Zero);
                dpc.Deserialize(de);
                return dpc;
            }, Array.Empty<DrawingPointContainer>()));

            this.Is2D = e.GetBool("Is2D", false);
            this.Camera2DHeight = e.GetSingle("Camera2DHeight", 5.0f);
            this.AmbientSoundID = e.GetGuidLegacy("AmbientSoundID", Guid.Empty);
            this.AmbientSoundVolume = e.GetSingle("AmbientVolume", 1.0f);
            this.ShadowLayer2D.Deserialize(e.GetMap("ShadowLayer2D", new DataElement()));
            this.DaySkyboxAssetID = e.GetGuidLegacy("DaySkyboxAssetID", Guid.Empty);
            this.DaySkyboxColors.Deserialize(e.GetMap("DaySkyboxColors", new DataElement()));
            this.NightSkyboxAssetID = e.GetGuidLegacy("NightSkyboxAssetID", Guid.Empty);
            this.NightSkyboxColors.Deserialize(e.GetMap("NightSkyboxColors", new DataElement()));
            this.CelestialBodies.Deserialize(e.GetMap("CelestialBodies", new DataElement()));

            if (this.IsServer)
            {
                this.Objects.AddRange(e.GetArray("Objects", (name, elem) =>
                {
                    MapObject r = new MapObject() { Container = this };
                    r.Deserialize(elem.GetMap(name));
                    return r;
                }, Array.Empty<MapObject>()));

                foreach (MapObject mo in this.Objects)
                {
                    this.ObjectsByID[mo.ID] = mo;
                }
            }
        }

        public DataElement Serialize()
        {
            DataElement ret = this.SerializeWithoutObjects();
            if (this.IsServer)
            {
                ret.SetArray("Objects", this.Objects.ToArray(), (name, container, obj) =>
                {
                    DataElement e = obj.Serialize();
                    container.SetMap(name, e);
                });
            }

            return ret;
        }

        public DataElement SerializeWithoutObjects()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("ID", this.ID);
            ret.SetString("Name", this.Name);
            ret.SetString("Folder", this.Folder);
            ret.SetBool("GridEnabled", this.GridEnabled);
            ret.SetBool("GridDrawn", this.GridDrawn);
            ret.SetSingle("GridSize", this.GridSize);
            ret.SetSingle("GridUnit", this.GridUnit);
            ret.SetColor("GridColor", this.GridColor);
            ret.SetEnum("GridType", this.GridType);
            ret.SetColor("AmbientColor", this.AmbientColor);
            ret.SetColor("SunColor", this.SunColor);
            ret.SetSingle("SunIntensity", this.SunIntensity);
            ret.SetSingle("AmbietIntensity", this.AmbientIntensity);
            ret.SetMap("TurnTracker", this.TurnTracker.Serialize());
            ret.SetBool("EnableDirectionalShadows", this.EnablePointShadows);
            ret.SetBool("EnableDarkvision", this.EnableDarkvision);
            ret.SetVec3("DefaultCameraPosition", this.DefaultCameraPosition);
            ret.SetVec3("DefaultCameraRotation", this.DefaultCameraRotation);
            ret.SetBool("EnableDrawing", this.EnableDrawing);
            ret.SetBool("Has2DShadows", this.Has2DShadows);
            ret.SetBool("Strict2DShadows", this.Shadows2DObjectVisionStrict);
            ret.SetPrimitiveArray("DarkvisionData", this.DarkvisionData.Select(kv => (kv.Key, kv.Value.Item1, kv.Value.Item2)).ToArray());
            ret.SetArray("PermanentMarks", this.PermanentMarks.ToArray(), (n, c, e) => c.SetMap(n, e.Serialize()));
            ret.SetArray("Drawings", this.Drawings.ToArray(), (n, c, e) => c.SetMap(n, e.Serialize()));
            ret.SetBool("Is2D", this.Is2D);
            ret.SetSingle("Camera2DHeight", this.Camera2DHeight);
            ret.SetGuid("AmbientSoundID", this.AmbientSoundID);
            ret.SetSingle("AmbientVolume", this.AmbientSoundVolume);
            ret.SetMap("ShadowLayer2D", this.ShadowLayer2D.Serialize());
            ret.SetGuid("DaySkyboxAssetID", this.DaySkyboxAssetID);
            ret.SetMap("DaySkyboxColors", this.DaySkyboxColors.Serialize());
            ret.SetGuid("NightSkyboxAssetID", this.NightSkyboxAssetID);
            ret.SetMap("NightSkyboxColors", this.NightSkyboxColors.Serialize());
            ret.SetMap("CelestialBodies", this.CelestialBodies.Serialize());
            return ret;
        }

        public void Save(string file)
        {
            if (this.IsServer) // Only server maps can be saved
            {
                DataElement data = this.Serialize();
                using Stream s = File.OpenWrite(file);
                using BinaryWriter bw = new BinaryWriter(s);
                data.Write(bw);
            }
        }

        public Map Clone()
        {
            Map ret = new Map() 
            { 
                ID = Guid.NewGuid(),
                IsServer = this.IsServer,
                IsDeleted = this.IsDeleted,
                Name = $"New {this.Name}",
                Folder = this.Folder,
                GridEnabled = this.GridEnabled,
                GridDrawn = this.GridDrawn,
                GridSize = this.GridSize,
                GridUnit = this.GridUnit,
                GridColor = this.GridColor,
                GridType = this.GridType,
                AmbientColor = this.AmbientColor,
                SunColor = this.SunColor,
                SunIntensity = this.SunIntensity,
                AmbientIntensity = this.AmbientIntensity,
                EnablePointShadows = this.EnablePointShadows,
                EnableDarkvision = this.EnableDarkvision,
                EnableDrawing = this.EnableDrawing,
                Has2DShadows = this.Has2DShadows,
                Shadows2DObjectVisionStrict = this.Shadows2DObjectVisionStrict,
                Is2D = this.Is2D,
                Camera2DHeight = this.Camera2DHeight,
                AmbientSoundID = this.AmbientSoundID,
                AmbientSoundVolume = this.AmbientSoundVolume,
                DefaultCameraPosition = this.DefaultCameraPosition,
                DefaultCameraRotation = this.DefaultCameraRotation,
                FOW = this.FOW.Clone(),
                DaySkyboxAssetID = this.DaySkyboxAssetID,
                NightSkyboxAssetID = this.NightSkyboxAssetID,
                DaySkyboxColors = this.DaySkyboxColors.Clone(),
                NightSkyboxColors = this.NightSkyboxColors.Clone(),
                CelestialBodies = this.CelestialBodies.Clone(),
                NeedsSave = true,
            };

            ret.TurnTracker = this.TurnTracker.CloneWithoutObjects(ret);
            ret.ShadowLayer2D.CloneFrom(this.ShadowLayer2D);
            ret.PermanentMarks.AddRange(this.PermanentMarks.Select(x => x.Clone()));
            ret.Drawings.AddRange(this.Drawings.Select(x => x.Clone()));
            lock (this.Lock)
            {
                foreach (MapObject mo in this.Objects)
                {
                    ret.AddObject(mo.Clone());
                }
            }

            foreach (KeyValuePair<Guid, (Guid, float)> kv in this.DarkvisionData)
            {
                ret.DarkvisionData[kv.Key] = (kv.Value.Item1, kv.Value.Item2);
            }

            ret.NeedsSave = ret.IsServer;
            return ret;
        }
    }

    public class MapCelestialBodies : ISerializable, IEnumerable<CelestialBody>
    {
        private readonly Dictionary<Guid, CelestialBody> _bodies = new Dictionary<Guid, CelestialBody>();
        private readonly object @lock = new object();
        private CelestialBody _sunRef;

        public CelestialBody Sun => this._sunRef;

        public MapCelestialBodies() => this.AddBody(this._sunRef = CelestialBody.CreateSun(Guid.NewGuid()));

        public IEnumerator<CelestialBody> GetEnumerator()
        {
            lock (this.@lock)
            {
                foreach (KeyValuePair<Guid, CelestialBody> kv in this._bodies)
                {
                    yield return kv.Value;
                }
            }
        }

        public void AddBody(CelestialBody body)
        {
            lock (this.@lock)
            {
                this._bodies.TryAdd(body.OwnID, body);
            }
        }

        public void RemoveBody(CelestialBody body) => this.RemoveBody(body.OwnID);
        public void RemoveBody(Guid key)
        {
            lock (this.@lock)
            {
                this._bodies.Remove(key);
            }
        }

        public bool TryGetBody(Guid key, out CelestialBody body) => this._bodies.TryGetValue(key, out body);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        public DataElement Serialize()
        {
            lock (this.@lock)
            {
                DataElement ret = new DataElement();
                ret.SetArray("Bodies", this._bodies.Values.ToArray(), (n, c, v) => c.SetMap(n, v.Serialize()));
                return ret;
            }
        }

        public void Deserialize(DataElement e)
        {
            lock (this.@lock)
            {
                this._bodies.Clear();
                CelestialBody[] bodies = e.GetArray("Bodies", (n, c) => new CelestialBody(c.GetMap(n)), Array.Empty<CelestialBody>());
                foreach (CelestialBody b in bodies)
                {
                    this._bodies[b.OwnID] = b;
                    if (b.IsSun)
                    {
                        this._sunRef = b;
                    }
                }

                if (this._sunRef == null && this._bodies.Count != 0)
                {
                    // Very very bad! We need at least one sun! Discard all and start again.
                    this._bodies.Clear();
                }

                if (this._bodies.Count == 0)
                {
                    // Not allowed, sun must always be present!
                    CelestialBody sun = CelestialBody.CreateSun(Guid.NewGuid());
                    this._bodies[sun.OwnID] = this._sunRef = sun;
                }
            }
        }

        public MapCelestialBodies Clone(bool keepIds = false)
        {
            MapCelestialBodies ret = new MapCelestialBodies();
            lock (this.@lock)
            {
                foreach (CelestialBody body in this._bodies.Values)
                {
                    CelestialBody b = body.Clone();
                    if (!keepIds)
                    {
                        b.OwnID = Guid.NewGuid();
                    }

                    ret._bodies[b.OwnID] = b;
                    if (b.IsSun)
                    {
                        ret._sunRef = b;
                    }
                }
            }

            return ret;
        }
    }

    public class MapSkyboxColors : ISerializable
    {
        public ColorsPointerType OwnType { get; set; }
        public Gradient<Vector4> ColorGradient { get; set; } = new Gradient<Vector4>();
        public Guid GradientAssetID { get; set; } = Guid.Empty;
        public Vector4 SolidColor { get; set; } = Vector4.One;

        private Guid _lastGradientAssetID = Guid.Empty;
        private Image<Rgba32> _cachedGradientColors;

        public MapSkyboxColors Clone()
        {
            MapSkyboxColors ret = new MapSkyboxColors()
            {
                OwnType = this.OwnType,
                GradientAssetID = this.GradientAssetID,
                SolidColor = this.SolidColor
            };

            foreach (KeyValuePair<float, Vector4> kv in this.ColorGradient)
            {
                ret.ColorGradient[kv.Key] = kv.Value;
            }

            return ret;
        }

        public void SwitchType(ColorsPointerType typeTo)
        {
            this.OwnType = typeTo;
            this.ColorGradient.Clear();
            this.GradientAssetID = Guid.Empty;
            this.SolidColor = Vector4.Zero;
            switch (typeTo)
            {
                case ColorsPointerType.SolidColor:
                {
                    this.SolidColor = ((Vector4)Color.SkyBlue);
                    break;
                }

                case ColorsPointerType.CustomGradient:
                {
                    this.ColorGradient.Add(0, ((Vector4)Color.Black));
                    this.ColorGradient.Add(12, ((Vector4)Color.White));
                    this.ColorGradient.Add(24, ((Vector4)Color.Black));
                    break;
                }

                case ColorsPointerType.CustomImage:
                case ColorsPointerType.DefaultSky:
                case ColorsPointerType.FullBlack:
                case ColorsPointerType.FullWhite:
                default:
                {
                    break;
                }
            }
        }

        public Vector4 GetColor(Map m, float time)
        {
            switch (this.OwnType)
            {
                case ColorsPointerType.SolidColor:
                {
                    return this.SolidColor;
                }

                case ColorsPointerType.FullWhite:
                {
                    return Vector4.One;
                }

                case ColorsPointerType.FullBlack:
                {
                    return Vector4.Zero;
                }

                case ColorsPointerType.CustomGradient:
                {
                    return this.ColorGradient.Interpolate(time, GradientInterpolators.LerpVec4);
                }

                case ColorsPointerType.CustomImage:
                {
                    if (!Guid.Equals(this._lastGradientAssetID, this.GradientAssetID))
                    {
                        this._cachedGradientColors?.Dispose();
                        this._cachedGradientColors = null;
                        this._lastGradientAssetID = this.GradientAssetID;
                    }

                    if (this.GradientAssetID.IsEmpty())
                    {
                        return Vector4.Zero;
                    }

                    AssetStatus aStat = Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(this.GradientAssetID, AssetType.Texture, out Asset a);
                    if (aStat != AssetStatus.Return || a == null || a.Texture == null || !a.Texture.glReady)
                    {
                        this._cachedGradientColors?.Dispose();
                        this._cachedGradientColors = null;
                        return Vector4.Zero;
                    }

                    if (this._cachedGradientColors == null)
                    {
                        this._cachedGradientColors = a.Texture.CompoundImage();
                    }

                    return this._cachedGradientColors[(int)MathF.Round(Math.Clamp(time / 24.0f, 0f, 1f) * this._cachedGradientColors.Width), 0].ToVector4();
                }

                case ColorsPointerType.DefaultSunlight:
                {
                    return Client.Instance.Frontend.Renderer.SkyRenderer.LightGrad.Interpolate(time, GradientInterpolators.LerpVec4);
                }

                case ColorsPointerType.DefaultSun:
                {
                    return Client.Instance.Frontend.Renderer.SkyRenderer.SunGrad.Interpolate(time, GradientInterpolators.LerpVec4);
                }

                case ColorsPointerType.DefaultSky:
                default:
                {
                    return Client.Instance.Frontend.Renderer.SkyRenderer.SkyGradient.Interpolate(time, GradientInterpolators.LerpVec4);
                }

            }
        }

        public void Deserialize(DataElement e)
        {
            this.OwnType = e.GetEnum("Kind", ColorsPointerType.DefaultSky);
            if (e.HasKeyOfAnyType("Gradient"))
            {
                KeyValuePair<float, Vector3>[] col = e.GetPrimitiveArrayWithLegacySupport("Gradient", (n, c) =>
                {
                    DataElement de = c.GetMap(n);
                    return new KeyValuePair<float, Vector3>(de.GetSingle("k"), de.GetVec3Legacy("v"));
                }, Array.Empty<KeyValuePair<float, Vector3>>());
                this.ColorGradient.Clear();
                foreach (KeyValuePair<float, Vector3> kv in col)
                {
                    this.ColorGradient[kv.Key] = new Vector4(kv.Value, 1.0f);
                }
            }

            this.SolidColor = e.HasKeyOfAnyType("SolidColor")
                ? new Vector4(e.GetVec3Legacy("SolidColor", Vector3.One), 1.0f)
                : e.GetVec4("SolidColor4", Vector4.One);

            this.GradientAssetID = e.GetGuidLegacy("AssetID", Guid.Empty);
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetEnum("Kind", this.OwnType);
            ret.SetPrimitiveArray("Gradient4", this.ColorGradient.ToArray());
            ret.SetVec4("SolidColor4", this.SolidColor);
            ret.SetGuid("AssetID", this.GradientAssetID);
            return ret;
        }

        public enum ColorsPointerType
        {
            DefaultSky,
            FullBlack,
            FullWhite,
            SolidColor,
            CustomGradient,
            CustomImage,
            DefaultSunlight,
            DefaultSun,
        }
    }

    public enum MapGridType : uint
    {
        Square = 0,
        HexHorizontal = 1,
        HexVertical = 2
    }
}
