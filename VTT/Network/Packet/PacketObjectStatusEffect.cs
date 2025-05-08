namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketObjectStatusEffect : PacketBaseWithCodec
    {
        public override uint PacketID => 50;

        public Guid MapID { get; set; }
        public Guid ObjectID { get; set; }
        public string EffectName { get; set; }
        public bool Remove { get; set; }
        public float S { get; set; }
        public float T { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Debug, "Got object status effect packet");
            Map m;
            if (isServer)
            {
                server.TryGetMap(this.MapID, out m);
            }
            else
            {
                m = client.CurrentMapIfMatches(this.MapID);
            }

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

        public override void LookupData(Codec c)
        {
            this.MapID = c.Lookup(this.MapID);
            this.ObjectID = c.Lookup(this.ObjectID);
            this.EffectName = c.Lookup(this.EffectName);
            this.Remove = c.Lookup(this.Remove);
            if (!this.Remove)
            {
                this.S = c.Lookup(this.S);
                this.T = c.Lookup(this.T);
            }
        }
    }
}
