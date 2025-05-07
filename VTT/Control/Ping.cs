namespace VTT.Control
{
    using System.Numerics;
    using SixLabors.ImageSharp;
    using System;
    using VTT.Util;

    public class Ping : ISerializable
    {
        public Guid OwnerID { get; set; }
        public long DeathTime { get; set; }

        public string OwnerName { get; set; }
        public Color OwnerColor { get; set; }
        public Vector3 Position { get; set; }

        public PingType Type { get; set; }

        public void Deserialize(DataElement e)
        {
            this.OwnerID = e.GetGuidLegacy("OwnerID");
            this.DeathTime = e.GetLong("DeathTime");
            this.OwnerName = e.GetString("OwnerName");
            this.OwnerColor = e.GetColor("OwnerColor");
            this.Position = e.GetVec3Legacy("Position");
            this.Type = (PingType)e.GetByte("Type");
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("OwnerID", this.OwnerID);
            ret.SetLong("DeathTime", this.DeathTime);
            ret.SetString("OwnerName", this.OwnerName);
            ret.SetColor("OwnerColor", this.OwnerColor);
            ret.SetVec3("Position", this.Position);
            ret.SetByte("Type", (byte)this.Type);
            return ret;
        }

        public bool IsEmote() => this.Type > PingType.Defend;

        public enum PingType
        {
            Generic,
            Exclamation,
            Question,
            Attack,
            Defend,

            Smiling,
            Laughing,
            Thinking,
            HappyTear,
            Suggestive,
            StarryEyes,
            Sunglasses,
            HeartEyes,
            Feared,
            Crying,
            VeryAngry,
            Vomiting,
        }
    }
}
