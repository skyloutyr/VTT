namespace VTT.Control
{
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using System;
    using System.IO;
    using VTT.Render;
    using VTT.Util;

    public class RulerInfo : ISerializable
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
                    accum += (v1 - v).Length;
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

        public void Write(BinaryWriter bw)
        {
            bw.Write(this.SelfID.ToByteArray());
            bw.Write(this.OwnerID.ToByteArray());
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

        public void Read(BinaryReader br) // Assume SelfID already read
        {
            this.OwnerID = new Guid(br.ReadBytes(16));
            this.OwnerName = br.ReadString();
            this.Tooltip = br.ReadString();
            this.Color = Extensions.FromArgb(br.ReadUInt32());
            this.Type = (RulerType)br.ReadByte();
            this.Points = br.ReadArray(x => x.ReadGlVec3());
            this.ExtraInfo = br.ReadSingle();
            this.NextDeleteTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 1500; // Expect a packet ~every 500ms, leave space for 750ms ping (both ways)
            this.IsDead = br.ReadBoolean();
            this.KeepAlive = br.ReadBoolean();
            this.DisplayInfo = br.ReadBoolean();
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
            ret.Set("OwnerName", this.OwnerName);
            ret.Set("Tooltip", this.Tooltip);
            ret.SetColor("Color", this.Color);
            ret.SetEnum("Type", this.Type);
            ret.Set("ExtraInfo", this.ExtraInfo);
            ret.Set("DisplayInfo", this.DisplayInfo);
            ret.SetArray("Points", this.Points, (n, c, v) => c.SetVec3(n, v));
            return ret;
        }

        public void Deserialize(DataElement e)
        {
            this.IsDead = false;
            this.KeepAlive = true;
            this.SelfID = e.GetGuid("SelfID");
            this.OwnerID = e.GetGuid("OwnerID");
            this.OwnerName = e.Get<string>("OwnerName");
            this.Tooltip = e.Get<string>("Tooltip");
            this.Color = e.GetColor("Color");
            this.Type = e.GetEnum<RulerType>("Type");
            this.Start = e.GetVec3("Start");
            this.End = e.GetVec3("End");
            this.ExtraInfo = e.Get<float>("ExtraInfo");
            this.DisplayInfo = e.Get("DisplayInfo", true);
            if (e.Has("Start", DataType.Map))
            {
                this.Points = new Vector3[2];
                this.Points[0] = e.GetVec3("Start");
                this.Points[1] = e.GetVec3("End");
            }
            else
            {
                this.Points = e.GetArray("Points", (n, c) => c.GetVec3(n), new Vector3[2]);
            }
        }
    }
}
