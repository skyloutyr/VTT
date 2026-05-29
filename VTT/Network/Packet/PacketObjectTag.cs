namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketObjectTag : PacketBaseWithCodec
    {
        public Guid MapID { get; set; }
        public Guid ObjectID { get; set; }
        public ActionType Action { get; set; }
        public Guid TagID { get; set; }
        public DataElement TagData { get; set; }

        public override uint PacketID => 92;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            Map m = null;
            if (isServer)
            {
                server.TryGetMap(this.MapID, out m);
            }
            else
            {
                m = client.CurrentMapIfMatches(this.MapID);
            }

            if (m == null)
            {
                l.Log(LogLevel.Warn, $"Got a ObjectTag packet for non-existing map {this.MapID}! Ignoring.");
                return;
            }

            if (!m.GetObject(this.ObjectID, out MapObject mo))
            {
                l.Log(LogLevel.Warn, $"Got a ObjectTag packet for non-existing object {this.ObjectID}! Ignoring.");
                return;
            }

            if (isServer)
            {
                if (!this.Sender.IsAdmin && !mo.CanEdit(this.Sender.ID))
                {
                    l.Log(LogLevel.Warn, $"ObjectTag changes attempted by a client witout permissions!");
                    return;
                }
            }

            lock (mo.TagsLock)
            {
                switch (this.Action)
                {
                    case ActionType.Update:
                    {
                        Tag t = mo.Tags.Find(x => Guid.Equals(x.ID, this.TagID));
                        if (t == null)
                        {
                            t = new Tag();
                            mo.Tags.Add(t);
                        }

                        t.Deserialize(this.TagData);
                        break;
                    }

                    case ActionType.Create:
                    {
                        Tag t = new Tag();
                        t.Deserialize(this.TagData);
                        mo.Tags.Add(t);
                        break;
                    }

                    case ActionType.Delete:
                    {
                        mo.Tags.RemoveAll(x => Guid.Equals(this.TagID, x.ID));
                        break;
                    }

                    case ActionType.MoveUp:
                    case ActionType.MoveDown:
                    {
                        int tIndex = mo.Tags.FindIndex(x => Guid.Equals(this.TagID, x.ID));
                        if (tIndex != -1)
                        {
                            Tag t = mo.Tags[tIndex];
                            if (this.Action == ActionType.MoveUp && tIndex > 0)
                            {
                                mo.Tags.RemoveAt(tIndex);
                                mo.Tags.Insert(tIndex - 1, t);
                            }

                            if (this.Action == ActionType.MoveDown && tIndex < mo.Tags.Count - 1)
                            {
                                bool willBeAtEnd = tIndex + 1 == mo.Tags.Count - 1;
                                mo.Tags.RemoveAt(tIndex);
                                if (willBeAtEnd)
                                {
                                    mo.Tags.Add(t);
                                }
                                else
                                {
                                    mo.Tags.Insert(tIndex + 1, t);
                                }
                            }
                        }

                        break;
                    }
                }
            }

            if (this.IsServer)
            {
                m.NeedsSave = true;
                this.Broadcast(x => x.ClientMapID.Equals(this.MapID));
            }
        }

        public override void LookupData(Codec c)
        {
            this.MapID = c.Lookup(this.MapID);
            this.ObjectID = c.Lookup(this.ObjectID);
            this.TagID = c.Lookup(this.TagID);
            if ((this.Action = c.Lookup(this.Action)) is ActionType.Create or ActionType.Update)
            {
                this.TagData = c.Lookup(this.TagData);
            }
        }

        public enum ActionType
        {
            Create,
            Delete,
            Update,
            MoveUp,
            MoveDown
        }
    }
}
