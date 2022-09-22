namespace VTT.Network.Packet
{
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using VTT.Control;
    using VTT.Util;

    public class PacketAura : PacketBase
    {
        public Guid MapID { get; set; }
        public Guid ObjectID { get; set; }
        public int Index { get; set; }
        public Color AuraColor { get; set; }
        public float AuraRange { get; set; }
        public Action ActionType { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.GetContextLogger();
            l.Log(LogLevel.Debug, "Got aura packet");
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
                l.Log(LogLevel.Warn, "Got aura change packet for non existing map, discarding.");
                return;
            }

            if (mo == null)
            {
                l.Log(LogLevel.Warn, "Got aura change packet for non existing object, discarding.");
                return;
            }

            if (!allowed)
            {
                l.Log(LogLevel.Warn, "Client asked for aura change without permissions!");
                return;
            }

            switch (this.ActionType)
            {
                case Action.Add:
                {
                    lock (mo.Lock)
                    {
                        mo.Auras.Add((this.AuraRange, this.AuraColor));
                    }

                    break;
                }

                case Action.Delete:
                {
                    lock (mo.Lock)
                    {
                        mo.Auras.RemoveAt(this.Index);
                    }

                    break;
                }

                case Action.Update:
                {
                    mo.Auras[this.Index] = (this.AuraRange, this.AuraColor);
                    break;
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
            this.MapID = br.ReadGuid();
            this.ObjectID = br.ReadGuid();
            this.ActionType = br.ReadEnumSmall<Action>();
            if (this.ActionType != Action.Add)
            {
                this.Index = br.ReadInt32();
            }

            if (this.ActionType != Action.Delete)
            {
                this.AuraColor = br.ReadColor();
                this.AuraRange = br.ReadSingle();
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.MapID);
            bw.Write(this.ObjectID);
            bw.WriteEnumSmall(this.ActionType);
            if (this.ActionType != Action.Add)
            {
                bw.Write(this.Index);
            }

            if (this.ActionType != Action.Delete)
            {
                bw.Write(this.AuraColor);
                bw.Write(this.AuraRange);
            }
        }

        public enum Action
        {
            Add,
            Delete,
            Update
        }
    }
}
