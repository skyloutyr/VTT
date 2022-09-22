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

        public Vector3 Start { get; set; }
        public Vector3 End { get; set; }
        public float ExtraInfo { get; set; }

        public long NextDeleteTime { get; set; }

        public void Write(BinaryWriter bw)
        {
            bw.Write(this.SelfID.ToByteArray());
            bw.Write(this.OwnerID.ToByteArray());
            bw.Write(this.OwnerName);
            bw.Write(this.Tooltip);
            bw.Write(this.Color.Argb());
            bw.Write((byte)this.Type);
            bw.Write(this.Start.X);
            bw.Write(this.Start.Y);
            bw.Write(this.Start.Z);
            bw.Write(this.End.X);
            bw.Write(this.End.Y);
            bw.Write(this.End.Z);
            bw.Write(this.ExtraInfo);
            bw.Write(this.IsDead);
            bw.Write(this.KeepAlive);
        }

        public void Read(BinaryReader br) // Assume SelfID already read
        {
            this.OwnerID = new Guid(br.ReadBytes(16));
            this.OwnerName = br.ReadString();
            this.Tooltip = br.ReadString();
            this.Color = Extensions.FromArgb(br.ReadUInt32());
            this.Type = (RulerType)br.ReadByte();
            this.Start = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            this.End = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            this.ExtraInfo = br.ReadSingle();
            this.NextDeleteTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 1500; // Expect a packet ~every 500ms, leave space for 750ms ping (both ways)
            this.IsDead = br.ReadBoolean();
            this.KeepAlive = br.ReadBoolean();
        }

        public void CloneData(RulerInfo other) // Assume SelfID check succeeded
        {
            this.OwnerID = other.OwnerID;
            this.OwnerName = other.OwnerName;
            this.Tooltip = other.Tooltip;
            this.Color = other.Color;
            this.Type = other.Type;
            this.Start = other.Start;
            this.End = other.End;
            this.ExtraInfo = other.ExtraInfo;
            this.NextDeleteTime = other.NextDeleteTime;
            this.IsDead = other.IsDead;
            this.KeepAlive = other.KeepAlive;
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
            ret.SetVec3("Start", this.Start);
            ret.SetVec3("End", this.End);
            ret.Set("ExtraInfo", this.ExtraInfo);
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
        }
    }
}
