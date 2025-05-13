namespace VTT.Control
{
    using SixLabors.ImageSharp;
    using System;
    using System.IO;
    using System.Numerics;
    using VTT.GL;
    using VTT.Render;
    using VTT.Util;

    public class RulerInfo : ISerializable, ICustomNetworkHandler
    {
        public Guid SelfID { get; set; }
        public Guid OwnerID { get; set; }
        public Color Color { get; set; }
        public bool IsDead { get; set; }
        public bool KeepAlive { get; set; }

        public string OwnerName { get; set; }
        public string Tooltip { get; set; }

        public RulerType Type { get; set; }

        public Vector3[] Points { get; set; } = new Vector3[2];

        public Vector3 Start
        {
            get => this.Points[0];
            set => this.Points[0] = value;
        }

        public Vector3 End
        {
            get => this.Points[^1];
            set => this.Points[^1] = value;
        }

        public Vector3 PreEnd
        {
            get => this.Points[^2];
            set => this.Points[^2] = value;
        }

        public float CumulativeLength
        {
            get
            {
                float accum = 0;
                for (int i = 0; i < this.Points.Length - 1; ++i)
                {
                    Vector3 v = this.Points[i];
                    Vector3 v1 = this.Points[i + 1];
                    accum += (v1 - v).Length();
                }

                return accum;
            }
        }

        public Vector3 CumulativeCenter
        {
            get
            {
                Vector3 accum = default;
                foreach (Vector3 p in this.Points)
                {
                    accum += p;
                }

                accum /= this.Points.Length;
                return accum;
            }
        }

        public float ExtraInfo { get; set; }
        public bool DisplayInfo { get; set; } = true;

        public long NextDeleteTime { get; set; }

        #region Render Data
        public VertexArray VAO { get; set; }
        public GPUBuffer VBO { get; set; }
        public GPUBuffer EBO { get; set; }
        public int NumIndices { get; set; }
        public bool RenderIsDirty { get; set; }
        #endregion

        public void Write(BinaryWriter bw)
        {
            bw.Write(this.SelfID);
            bw.Write(this.OwnerID);
            bw.Write(this.OwnerName);
            bw.Write(this.Tooltip);
            bw.Write(this.Color.Argb());
            bw.Write((byte)this.Type);
            bw.WriteArray(this.Points, (bw, t) => bw.Write(t));
            bw.Write(this.ExtraInfo);
            bw.Write(this.IsDead);
            bw.Write(this.KeepAlive);
            bw.Write(this.DisplayInfo);
        }

        public void Read(BinaryReader br)
        {
            this.SelfID = br.ReadGuid();
            this.OwnerID = br.ReadGuid();
            this.OwnerName = br.ReadString();
            this.Tooltip = br.ReadString();
            this.Color = Extensions.FromArgb(br.ReadUInt32());
            this.Type = (RulerType)br.ReadByte();
            this.Points = br.ReadArray(x => x.ReadVec3());
            this.ExtraInfo = br.ReadSingle();
            this.NextDeleteTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 1500; // Expect a packet ~every 500ms, leave space for 750ms ping (both ways)
            this.IsDead = br.ReadBoolean();
            this.KeepAlive = br.ReadBoolean();
            this.DisplayInfo = br.ReadBoolean();
        }

        public RulerInfo Clone()
        {
            RulerInfo ret = new RulerInfo();
            ret.CloneData(this);
            return ret;
        }

        public void CloneData(RulerInfo other) // Assume SelfID check succeeded
        {
            this.OwnerID = other.OwnerID;
            this.OwnerName = other.OwnerName;
            this.Tooltip = other.Tooltip;
            this.Color = other.Color;
            this.Type = other.Type;
            this.Points = new Vector3[other.Points.Length];
            for (int i = 0; i < other.Points.Length; ++i)
            {
                this.Points[i] = other.Points[i];
            }

            this.Start = other.Start;
            this.End = other.End;
            this.ExtraInfo = other.ExtraInfo;
            this.NextDeleteTime = other.NextDeleteTime;
            this.IsDead = other.IsDead;
            this.KeepAlive = other.KeepAlive;
            this.DisplayInfo = other.DisplayInfo;
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("SelfID", this.SelfID);
            ret.SetGuid("OwnerID", this.OwnerID);
            ret.SetString("OwnerName", this.OwnerName);
            ret.SetString("Tooltip", this.Tooltip);
            ret.SetColor("Color", this.Color);
            ret.SetEnum("Type", this.Type);
            ret.SetSingle("ExtraInfo", this.ExtraInfo);
            ret.SetBool("DisplayInfo", this.DisplayInfo);
            ret.SetPrimitiveArray("Points", this.Points);
            return ret;
        }

        public void Deserialize(DataElement e)
        {
            this.SelfID = e.GetGuidLegacy("SelfID");
            this.OwnerID = e.GetGuidLegacy("OwnerID");
            this.OwnerName = e.GetString("OwnerName");
            this.Tooltip = e.GetString("Tooltip");
            this.Color = e.GetColor("Color");
            this.Type = e.GetEnum<RulerType>("Type");
            this.Start = e.GetVec3Legacy("Start");
            this.End = e.GetVec3Legacy("End");
            this.ExtraInfo = e.GetSingle("ExtraInfo");
            this.DisplayInfo = e.GetBool("DisplayInfo", true);
            if (e.Has("Start", DataType.Map))
            {
                this.Points = new Vector3[2];
                this.Points[0] = e.GetVec3Legacy("Start");
                this.Points[1] = e.GetVec3Legacy("End");
            }
            else
            {
                this.Points = e.GetPrimitiveArrayWithLegacySupport("Points", (n, c) => c.GetVec3Legacy(n), new Vector3[2]);
            }

            this.IsDead = false;
            this.KeepAlive = true;
        }
    }
}
