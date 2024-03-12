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
            this.OwnerID = e.GetGuid("OwnerID");
            this.DeathTime = e.Get<long>("DeathTime");
            this.OwnerName = e.Get<string>("OwnerName");
            this.OwnerColor = e.GetColor("OwnerColor");
            this.Position = e.GetVec3("Position");
            this.Type = (PingType)e.Get<byte>("Type");
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("OwnerID", this.OwnerID);
            ret.Set("DeathTime", this.DeathTime);
            ret.Set("OwnerName", this.OwnerName);
            ret.SetColor("OwnerColor", this.OwnerColor);
            ret.SetVec3("Position", this.Position);
            ret.Set("Type", (byte)this.Type);
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
