namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketParticleContainer : PacketBase
    {
        public Guid MapID { get; set; }
        public Guid ObjectID { get; set; }
        public Guid ParticleID { get; set; }
        public Action ActionType { get; set; }
        public DataElement Container { get; set; }
        public override uint PacketID => 51;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.GetContextLogger();
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

                        mo.ParticleContainers[pc.ID] = pc;
                        break;
                    }

                    case Action.Delete:
                    {
                        if (mo.ParticleContainers.ContainsKey(this.ParticleID))
                        {
                            mo.ParticleContainers.Remove(this.ParticleID);
                        }

                        break;
                    }

                    case Action.Edit:
                    {
                        if (mo.ParticleContainers.ContainsKey(this.ParticleID))
                        {
                            mo.ParticleContainers[this.ParticleID].Deserialize(this.Container);
                        }

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

        public override void Decode(BinaryReader br)
        {
            this.MapID = br.ReadGuid();
            this.ObjectID = br.ReadGuid();
            this.ActionType = br.ReadEnumSmall<Action>();
            switch (this.ActionType)
            {
                case Action.Delete:
                {
                    this.ParticleID = br.ReadGuid();
                    break;
                }

                case Action.Add:
                {
                    this.Container = new DataElement(br);
                    break;
                }

                case Action.Edit:
                {
                    this.ParticleID = br.ReadGuid();
                    this.Container = new DataElement(br);
                    break;
                }
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.MapID);
            bw.Write(this.ObjectID);
            bw.WriteEnumSmall(this.ActionType);
            switch (this.ActionType)
            {
                case Action.Delete:
                {
                    bw.Write(this.ParticleID);
                    break;
                }

                case Action.Add:
                {
                    this.Container.Write(bw);
                    break;
                }

                case Action.Edit:
                {
                    bw.Write(this.ParticleID);
                    this.Container.Write(bw);
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
