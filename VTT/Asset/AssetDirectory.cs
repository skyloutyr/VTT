namespace VTT.Asset
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using VTT.Util;

    public class AssetDirectory : ISerializable
    {
        public string Name { get; set; }
        public AssetDirectory Parent { get; set; }
        public List<AssetDirectory> Directories { get; } = new List<AssetDirectory>();
        public List<AssetRef> Refs { get; } = new List<AssetRef>();

        public string GetPath()
        {
            string ret = this.Parent == null ? this.Name : this.Parent.GetPath() + "/" + this.Name;
            return ret;
        }

        public string GetUniqueSubdirName(string name)
        {
            int incr = 0;
            string oName = name;
            while (true)
            {
                bool found = false;
                foreach (AssetDirectory ad in this.Directories)
                {
                    if (ad.Name.Equals(name))
                    {
                        found = true;
                        incr += 1;
                        name = oName + " (" + incr + ")";
                        break;
                    }
                }

                if (!found)
                {
                    break;
                }
            }

            return name;
        }

        public void Deserialize(DataElement e)
        {
            this.Refs.Clear();
            this.Directories.Clear();
            this.Name = e.Get<string>("Name");
            this.Refs.AddRange(e.GetArray("Refs", (n, e) =>
            {
                AssetRef assetRef = new AssetRef();
                assetRef.Deserialize(e.Get<DataElement>(n));
                return assetRef;
            }));

            this.Directories.AddRange(e.GetArray("Sub", (n, e) => {
                AssetDirectory d = new AssetDirectory();
                d.Deserialize(e.Get<DataElement>(n));
                d.Parent = this;
                return d;
            }));
        }
        
        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.Set("Name", this.Name);
            ret.SetArray("Refs", this.Refs.ToArray(), (n, c, v) => c.Set(n, v.Serialize()));
            ret.SetArray("Sub", this.Directories.ToArray(), (n, c, v) => c.Set(n, v.Serialize()));
            return ret;
        }

        public IEnumerable<AssetRef> EnumerateAllRefsRecursively()
        {
            IEnumerable<AssetRef> e = Enumerable.Empty<AssetRef>();
            e = e.Concat(this.Refs);
            foreach (AssetDirectory d in this.Directories)
            {
                e = e.Concat(d.EnumerateAllRefsRecursively());
            }

            return e;
        }
    }

    public class AssetRef : ISerializable
    { 
        public Guid AssetID { get; set; }
        public Guid AssetPreviewID { get; set; }

        public string Name 
        {
            get => this.Meta.Name;
            set => this.Meta.Name = value;
        }

        public AssetType Type
        {
            get => this.Meta.Type;
            set => this.Meta.Type = value;
        }

        public AssetMetadata Meta { get; set; }

        public bool IsServer { get; set; }
        public AssetBinaryPointer ServerPointer { get; set; }

        public AssetRef()
        {
        }

        public void Deserialize(DataElement e)
        {
            this.AssetID = e.GetGuid("ID");
            this.AssetPreviewID = e.GetGuid("Preview");
            this.Meta = new AssetMetadata();
            this.Meta.Deserialize(e.Get<DataElement>("Meta"));
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("ID", this.AssetID);
            ret.SetGuid("Preview", this.AssetPreviewID);
            ret.Set("Meta", this.Meta.Serialize());
            return ret;
        }
    }
}
