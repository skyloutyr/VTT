namespace VTT.Control
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
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
        public Color BackgroundColor { get; set; }
        public Color AmbientColor { get; set; }
        public Color SunColor { get; set; }

        public bool SunEnabled { get; set; } = true;
        public float SunYaw { get; set; }
        public float SunPitch { get; set; }
        public float SunIntensity { get; set; } = 1.0f;
        public float AmbientIntensity { get; set; } = 1.0f;

        public bool EnableShadows { get; set; } = true;
        public bool EnableDirectionalShadows { get; set; } = false;
        public bool EnableDarkvision { get; set; }
        public bool EnableDrawing { get; set; } = true;
        public bool Has2DShadows { get; set; } = false;

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

        public List<MapObject> Objects { get; } = new List<MapObject>();
        public List<RulerInfo> PermanentMarks { get; } = new List<RulerInfo>();
        public List<DrawingPointContainer> Drawings { get; } = new List<DrawingPointContainer>();
        public Dictionary<Guid, MapObject> ObjectsByID { get; } = new Dictionary<Guid, MapObject>();

        public Dictionary<Guid, (Guid, float)> DarkvisionData { get; } = new Dictionary<Guid, (Guid, float)>();

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
                if (!this.IsServer)
                {
                    obj.Particles.UploadAllConainers();
                }
            }
        }

        public void RemoveObject(MapObject obj)
        {
            if (this.IsServer)
            {
                lock (this.Lock)
                {
                    this.Objects.Remove(obj);
                    obj.Container = null;
                    obj.MapID = Guid.Empty;
                    this.ObjectsByID.Remove(obj.ID);
                    this.NeedsSave = true;
                }
            }
            else
            {
                obj.IsRemoved = true;
            }
        }

        public bool GetObject(Guid id, out MapObject mo)
        {
            MapObject ret = null;
            lock (this.Lock)
            {
                if (this.ObjectsByID.ContainsKey(id))
                {
                    ret = this.ObjectsByID[id];
                }
            }

            mo = ret;
            return ret != null;
        }

        public bool GetObjectUnsafe(Guid id, out MapObject mo)
        {
            MapObject ret = null;
            if (this.ObjectsByID.ContainsKey(id))
            {
                ret = this.ObjectsByID[id];
            }

            mo = ret;
            return ret != null;
        }

        public IEnumerable<MapObject> IterateObjects(int? layer)
        {
            lock (this.Lock)
            {
                for (int i = this.Objects.Count - 1; i >= 0; i--)
                {
                    if (i >= this.Objects.Count)
                    {
                        continue;
                    }

                    MapObject mo = this.Objects[i];
                    if (!layer.HasValue || mo.MapLayer == layer.Value)
                    {
                        yield return mo;
                    }
                }
            }

            yield break;
        }

        public IEnumerable<MapObject> IterateObjectsUnsafe(int? layer)
        {
            for (int i = this.Objects.Count - 1; i >= 0; i--)
            {
                if (i >= this.Objects.Count)
                {
                    continue;
                }

                MapObject mo = this.Objects[i];
                if (!layer.HasValue || mo.MapLayer == layer.Value)
                {
                    yield return mo;
                }
            }

            yield break;
        }

        public void Deserialize(DataElement e)
        {
            this.ID = e.GetGuid("ID");
            this.Name = e.GetString("Name");
            this.Folder = e.GetString("Folder", string.Empty);
            this.GridEnabled = e.GetBool("GridEnabled");
            this.GridDrawn = e.GetBool("GridDrawn");
            this.GridSize = e.GetSingle("GridSize");
            this.GridUnit = e.GetSingle("GridUnit", 5.0f);
            this.GridColor = e.GetColor("GridColor");
            this.BackgroundColor = e.GetColor("BackgroundColor");
            this.SunColor = e.GetColor("SunColor", new Color(new Rgba32(0.2f, 0.2f, 0.2f, 1.0f)));
            this.AmbientColor = e.GetColor("AmbientColor", new Color(new Rgba32(0.03f, 0.03f, 0.03f, 0.03f)));
            this.SunEnabled = e.GetBool("SunEnabled", true);
            this.SunYaw = e.GetSingle("SunYaw");
            this.SunPitch = e.GetSingle("SunPitch");
            this.SunIntensity = e.GetSingle("SunIntensity");
            this.AmbientIntensity = e.GetSingle("AmbietIntensity", 1.0f);
            this.TurnTracker.Deserialize(e.GetMap("TurnTracker", new DataElement()));
            this.EnableShadows = e.GetBool("EnableShadows");
            this.EnableDirectionalShadows = e.GetBool("EnableDirectionalShadows");
            this.EnableDarkvision = e.GetBool("EnableDarkvision");
            this.DefaultCameraPosition = e.GetVec3("DefaultCameraPosition", this.DefaultCameraPosition);
            this.DefaultCameraRotation = e.GetVec3("DefaultCameraRotation", this.DefaultCameraRotation);
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
            (Guid, Guid, float)[] dvData = e.GetArray("DarkvisionData", (n, c) =>
            {
                DataElement de = c.GetMap(n);
                return (de.GetGuid("k"), de.GetGuid("o"), de.GetSingle("v"));
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
            this.AmbientSoundID = e.GetGuid("AmbientSoundID", Guid.Empty);
            this.AmbientSoundVolume = e.GetSingle("AmbientVolume", 1.0f);
            this.ShadowLayer2D.Deserialize(e.GetMap("ShadowLayer2D", new DataElement()));
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
            ret.SetColor("BackgroundColor", this.BackgroundColor);
            ret.SetColor("AmbientColor", this.AmbientColor);
            ret.SetColor("SunColor", this.SunColor);
            ret.SetBool("SunEnabled", this.SunEnabled);
            ret.SetSingle("SunYaw", this.SunYaw);
            ret.SetSingle("SunPitch", this.SunPitch);
            ret.SetSingle("SunIntensity", this.SunIntensity);
            ret.SetSingle("AmbietIntensity", this.AmbientIntensity);
            ret.SetMap("TurnTracker", this.TurnTracker.Serialize());
            ret.SetBool("EnableShadows", this.EnableShadows);
            ret.SetBool("EnableDirectionalShadows", this.EnableDirectionalShadows);
            ret.SetBool("EnableDarkvision", this.EnableDarkvision);
            ret.SetVec3("DefaultCameraPosition", this.DefaultCameraPosition);
            ret.SetVec3("DefaultCameraRotation", this.DefaultCameraRotation);
            ret.SetBool("EnableDrawing", this.EnableDrawing);
            ret.SetBool("Has2DShadows", this.Has2DShadows);
            ret.SetArray("DarkvisionData", this.DarkvisionData.Select(kv => (kv.Key, kv.Value.Item1, kv.Value.Item2)).ToArray(), (n, c, e) =>
            {
                DataElement d = new DataElement();
                d.SetGuid("k", e.Key);
                d.SetGuid("o", e.Item2);
                d.SetSingle("v", e.Item3);
                c.SetMap(n, d);
            });

            ret.SetArray("PermanentMarks", this.PermanentMarks.ToArray(), (n, c, e) => c.SetMap(n, e.Serialize()));
            ret.SetArray("Drawings", this.Drawings.ToArray(), (n, c, e) => c.SetMap(n, e.Serialize()));
            ret.SetBool("Is2D", this.Is2D);
            ret.SetSingle("Camera2DHeight", this.Camera2DHeight);
            ret.SetGuid("AmbientSoundID", this.AmbientSoundID);
            ret.SetSingle("AmbientVolume", this.AmbientSoundVolume);
            ret.SetMap("ShadowLayer2D", this.ShadowLayer2D.Serialize());
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
    }
}
