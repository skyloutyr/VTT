namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketParticleContainer : PacketBaseWithCodec
    {
        public override uint PacketID => 51;

        public Guid MapID { get; set; }
        public Guid ObjectID { get; set; }
        public Guid ParticleID { get; set; }
        public Action ActionType { get; set; }
        public DataElement Container { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Debug, "Got particle packet");
            bool allowed = true;
            Map m = null;
            MapObject mo = null;
            if (isServer)
            {
                if (server.TryGetMap(this.MapID, out m))
                {
                    if (m.GetObject(this.ObjectID, out mo))
                    {
                        allowed = this.Sender.IsAdmin || mo.CanEdit(this.Sender.ID);
                    }
                }
            }
            else
            {
                m = client.CurrentMapIfMatches(this.MapID);
                m?.GetObject(this.ObjectID, out mo);
            }

            if (m == null)
            {
                l.Log(LogLevel.Warn, "Got particle packet for non existing map, discarding.");
                return;
            }

            if (mo == null)
            {
                l.Log(LogLevel.Warn, "Got particle packet for non existing object, discarding.");
                return;
            }

            if (!allowed)
            {
                l.Log(LogLevel.Warn, "Client asked for particle change without permissions!");
                return;
            }

            lock (mo.Lock)
            {
                switch (this.ActionType)
                {
                    case Action.Add:
                    {
                        ParticleContainer pc = new ParticleContainer(mo);
                        pc.Deserialize(this.Container);
                        if (isServer)
                        {
                            pc.ID = Guid.NewGuid();
                            this.Container = pc.Serialize();
                        }

                        mo.Particles.AddContainer(pc);
                        break;
                    }

                    case Action.Delete:
                    {
                        mo.Particles.RemoveContainer(this.ParticleID);
                        break;
                    }

                    case Action.Edit:
                    {
                        mo.Particles.UpdateContainer(this.ParticleID, this.Container);
                        break;
                    }
                }
            }

            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast(c => c.ClientMapID.Equals(m.ID));
            }
        }

        public override void LookupData(Codec c)
        {
            this.MapID = c.Lookup(this.MapID);
            this.ObjectID = c.Lookup(this.ObjectID);
            this.ActionType = c.Lookup(this.ActionType);
            switch (this.ActionType)
            {
                case Action.Delete:
                {
                    this.ParticleID = c.Lookup(this.ParticleID);
                    break;
                }

                case Action.Add:
                {
                    this.Container = c.Lookup(this.Container);
                    break;
                }

                case Action.Edit:
                {
                    this.ParticleID = c.Lookup(this.ParticleID);
                    this.Container = c.Lookup(this.Container);
                    break;
                }
            }
        }

        public enum Action
        {
            Add,
            Delete,
            Edit
        }
    }
}
