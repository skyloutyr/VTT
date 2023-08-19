namespace VTT.Control
{
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
        public float AmbietIntensity { get; set; } = 1.0f;

        public bool EnableShadows { get; set; } = true;
        public bool EnableDirectionalShadows { get; set; } = false;
        public bool EnableDarkvision { get; set; }

        public bool Is2D { get; set; }
        public float Camera2DHeight { get; set; } = 5.0f;

        public Vector3 DefaultCameraPosition { get; set; } = new Vector3(5, 5, 5);
        public Vector3 DefaultCameraRotation { get; set; } = new Vector3(-1, -1, -1).Normalized();

        // Server-only
        public FOWCanvas FOW { get; set; }
        public TurnTracker TurnTracker { get; set; }


        public List<MapObject> Objects { get; } = new List<MapObject>();
        public List<RulerInfo> PermanentMarks { get; } = new List<RulerInfo>();

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
            this.Name = e.Get<string>("Name");
            this.Folder = e.Get("Folder", string.Empty);
            this.GridEnabled = e.Get<bool>("GridEnabled");
            this.GridDrawn = e.Get<bool>("GridDrawn");
            this.GridSize = e.Get<float>("GridSize");
            this.GridUnit = e.Get("GridUnit", 5.0f);
            this.GridColor = e.GetColor("GridColor");
            this.BackgroundColor = e.GetColor("BackgroundColor");
            this.SunColor = e.GetColor("SunColor", new Color(new Rgba32(0.2f, 0.2f, 0.2f, 1.0f)));
            this.AmbientColor = e.GetColor("AmbientColor", new Color(new Rgba32(0.03f, 0.03f, 0.03f, 0.03f)));
            this.SunEnabled = e.Get("SunEnabled", true);
            this.SunYaw = e.Get<float>("SunYaw");
            this.SunPitch = e.Get<float>("SunPitch");
            this.SunIntensity = e.Get<float>("SunIntensity");
            this.AmbietIntensity = e.Get("AmbietIntensity", 1.0f);
            this.TurnTracker.Deserialize(e.Get("TurnTracker", new DataElement()));
            this.EnableShadows = e.Get<bool>("EnableShadows");
            this.EnableDirectionalShadows = e.Get<bool>("EnableDirectionalShadows");
            this.EnableDarkvision = e.Get<bool>("EnableDarkvision");
            this.DefaultCameraPosition = e.GetVec3("DefaultCameraPosition", this.DefaultCameraPosition);
            this.DefaultCameraRotation = e.GetVec3("DefaultCameraRotation", this.DefaultCameraRotation);
            (Guid, Guid, float)[] dvData = e.GetArray("DarkvisionData", (n, c) =>
            {
                DataElement de = c.Get<DataElement>(n);
                return (de.GetGuid("k"), de.GetGuid("o"), de.Get<float>("v"));
            }, Array.Empty<(Guid, Guid, float)>());
            this.DarkvisionData.Clear();
            foreach ((Guid, Guid, float) kv in dvData)
            {
                this.DarkvisionData[kv.Item1] = (kv.Item2, kv.Item3);
            }

            this.PermanentMarks.Clear();
            this.PermanentMarks.AddRange(e.GetArray("PermanentMarks", (n, c) =>
            {
                DataElement de = c.Get<DataElement>(n);
                RulerInfo ri = new RulerInfo();
                ri.Deserialize(de);
                return ri;
            }, Array.Empty<RulerInfo>()));

            this.Is2D = e.Get("Is2D", false);
            this.Camera2DHeight = e.Get("Camera2DHeight", 5.0f);
            if (this.IsServer)
            {
                this.Objects.AddRange(e.GetArray("Objects", (name, elem) =>
                {
                    MapObject r = new MapObject() { Container = this };
                    r.Deserialize(elem.Get<DataElement>(name));
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
                    container.Set(name, e);
                });
            }

            return ret;
        }

        public DataElement SerializeWithoutObjects()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("ID", this.ID);
            ret.Set("Name", this.Name);
            ret.Set("Folder", this.Folder);
            ret.Set("GridEnabled", this.GridEnabled);
            ret.Set("GridDrawn", this.GridDrawn);
            ret.Set("GridSize", this.GridSize);
            ret.Set("GridUnit", this.GridUnit);
            ret.SetColor("GridColor", this.GridColor);
            ret.SetColor("BackgroundColor", this.BackgroundColor);
            ret.SetColor("AmbientColor", this.AmbientColor);
            ret.SetColor("SunColor", this.SunColor);
            ret.Set("SunEnabled", this.SunEnabled);
            ret.Set("SunYaw", this.SunYaw);
            ret.Set("SunPitch", this.SunPitch);
            ret.Set("SunIntensity", this.SunIntensity);
            ret.Set("AmbietIntensity", this.AmbietIntensity);
            ret.Set("TurnTracker", this.TurnTracker.Serialize());
            ret.Set("EnableShadows", this.EnableShadows);
            ret.Set("EnableDirectionalShadows", this.EnableDirectionalShadows);
            ret.Set("EnableDarkvision", this.EnableDarkvision);
            ret.SetVec3("DefaultCameraPosition", this.DefaultCameraPosition);
            ret.SetVec3("DefaultCameraRotation", this.DefaultCameraRotation);
            ret.SetArray("DarkvisionData", this.DarkvisionData.Select(kv => (kv.Key, kv.Value.Item1, kv.Value.Item2)).ToArray(), (n, c, e) =>
            {
                DataElement d = new DataElement();
                d.SetGuid("k", e.Key);
                d.SetGuid("o", e.Item2);
                d.Set("v", e.Item3);
                c.Set(n, d);
            });

            ret.SetArray("PermanentMarks", this.PermanentMarks.ToArray(), (n, c, e) => c.Set(n, e.Serialize()));
            ret.Set("Is2D", this.Is2D);
            ret.Set("Camera2DHeight", this.Camera2DHeight);
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
