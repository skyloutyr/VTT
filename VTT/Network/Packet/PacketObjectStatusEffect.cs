namespace VTT.Network.Packet
{
    using VTT.Util;
    using System;
    using System.IO;
    using VTT.Control;

    public class PacketObjectStatusEffect : PacketBase
    {
        public Guid MapID { get; set; }
        public Guid ObjectID { get; set; }
        public string EffectName { get; set; }
        public bool Remove { get; set; }
        public float S { get; set; }
        public float T { get; set; }
        public override uint PacketID => 50;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.GetContextLogger();
            l.Log(LogLevel.Debug, "Got object status effect packet");
            Map m = isServer ? server.Maps.ContainsKey(this.MapID) ? server.Maps[this.MapID] : null : client.CurrentMapIfMatches(this.MapID);
            MapObject mo = null;
            m?.GetObject(this.ObjectID, out mo);
            if (m == null)
            {
                l.Log(LogLevel.Warn, "Object container not found, discarding.");
                return;
            }

            if (mo == null)
            {
                l.Log(LogLevel.Warn, "Effect container not found, discarding.");
                return;
            }

            bool allowed = true;
            if (isServer)
            {
                allowed = this.Sender.IsAdmin || mo.CanEdit(this.Sender.ID);
            }

            if (!allowed)
            {
                l.Log(LogLevel.Warn, "Got object effect change request without permissions.");
                return;
            }

            lock (mo.Lock)
            {
                if (this.Remove)
                {
                    mo.StatusEffects.Remove(this.EffectName);
                }
                else
                {
                    mo.StatusEffects[this.EffectName] = (this.S, this.T);
                }
            }

            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast(c => c.ClientMapID.Equals(this.MapID));
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.MapID = new Guid(br.ReadBytes(16));
            this.ObjectID = new Guid(br.ReadBytes(16));
            this.EffectName = br.ReadString();
            this.Remove = br.ReadBoolean();
            if (!this.Remove)
            {
                this.S = br.ReadSingle();
                this.T = br.ReadSingle();
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.MapID.ToByteArray());
            bw.Write(this.ObjectID.ToByteArray());
            bw.Write(this.EffectName);
            bw.Write(this.Remove);
            if (!this.Remove)
            {
                bw.Write(this.S);
                bw.Write(this.T);
            }
        }
    }
}
