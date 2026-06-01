namespace VTT.Network.Packet
{
    using System;
    using System.Numerics;
    using VTT.Control;
    using VTT.Util;

    public class PacketObjectStatusEffect : PacketBaseWithCodec
    {
        public override uint PacketID => 50;

        public Guid MapID { get; set; }
        public Guid ObjectID { get; set; }
        public Guid EffectID { get; set; }
        public UpdateType ActionKind { get; set; }
        public object Data { get; set; }

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
                StatusEffect e = mo.StatusEffects.Find(x => Guid.Equals(this.EffectID, x.ID));
                if (this.ActionKind != UpdateType.Full && e == null)
                {
                    l.Log(LogLevel.Warn, "Got object effect change request for non-existing effect.");
                    return;
                }

                switch (this.ActionKind)
                {
                    case UpdateType.Delete:
                    {
                        mo.StatusEffects.Remove(e);
                        break;
                    }

                    case UpdateType.Full:
                    {
                        if (e == null)
                        {
                            e = new StatusEffect();
                            mo.StatusEffects.Add(e);
                        }

                        e.Deserialize((DataElement)this.Data);
                        break;
                    }

                    case UpdateType.Tag:
                    {
                        e.Tag.Deserialize((DataElement)this.Data);
                        break;
                    }

                    case UpdateType.Stack:
                    {
                        e.Stack = (int)this.Data;
                        break;
                    }

                    case UpdateType.Counter:
                    {
                        e.Counter = (int)this.Data;
                        break;
                    }

                    case UpdateType.CounterMax:
                    {
                        e.CounterMax = (int)this.Data;
                        break;
                    }

                    case UpdateType.CounterAutomation:
                    {
                        e.CounterAutomation = (StatusEffect.CounterAutomationType)this.Data;
                        break;
                    }

                    case UpdateType.Tooltip:
                    {
                        e.Tooltip = (string)this.Data;
                        break;
                    }

                    case UpdateType.UVs:
                    {
                        e.UVs = (Vector2)this.Data;
                        break;
                    }

                    case UpdateType.Color:
                    {
                        e.Color = (Vector4)this.Data;
                        break;
                    }

                    case UpdateType.IsPublic:
                    {
                        e.IsPublic = (bool)this.Data;
                        break;
                    }
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
            this.EffectID = c.Lookup(this.EffectID);
            switch (this.ActionKind = c.Lookup(this.ActionKind))
            {
                case UpdateType.Full:
                case UpdateType.Tag:
                {
                    this.Data = c.LookupBox<DataElement>(this.Data, c.Lookup);
                    break;
                }

                case UpdateType.Stack:
                case UpdateType.Counter:
                case UpdateType.CounterMax:
                {
                    this.Data = c.LookupBox<int>(this.Data, c.Lookup);
                    break;
                }

                case UpdateType.CounterAutomation:
                {
                    this.Data = c.LookupBox<StatusEffect.CounterAutomationType>(this.Data, c.Lookup);
                    break;
                }

                case UpdateType.Tooltip:
                {
                    this.Data = c.LookupBox<string>(this.Data, c.Lookup);
                    break;
                }

                case UpdateType.UVs:
                {
                    this.Data = c.LookupBox<Vector2>(this.Data, c.Lookup);
                    break;
                }

                case UpdateType.Color:
                {
                    this.Data = c.LookupBox<Vector4>(this.Data, c.Lookup);
                    break;
                }

                case UpdateType.IsPublic:
                {
                    this.Data = c.LookupBox<bool>(this.Data, c.Lookup);
                    break;
                }

                case UpdateType.Delete:
                default:
                {
                    break;
                }
            }
        }

        public enum UpdateType
        {
            Delete,
            Full,
            Tag,
            Stack,
            Counter,
            CounterMax,
            CounterAutomation,
            Tooltip,
            UVs,
            Color,
            IsPublic
        }
    }
}
