namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketFastLight : PacketBaseWithCodec
    {
        public override uint PacketID => 60;

        public Guid MapID { get; set; }
        public Guid ObjectID { get; set; }
        public int Index { get; set; }
        public FastLight Light { get; set; }
        public Action ActionType { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Debug, "Got fast light packet");
            bool allowed = true;
            Map m = null;
            MapObject mo = null;
            if (isServer)
            {
                if (server.TryGetMap(this.MapID, out m))
                {
                    if (m.GetObject(this.ObjectID, out mo))
                    {
                        allowed = this.Sender.IsAdmin;
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
                l.Log(LogLevel.Warn, "Got fast light change packet for non existing map, discarding.");
                return;
            }

            if (mo == null)
            {
                l.Log(LogLevel.Warn, "Got fast light change packet for non existing object, discarding.");
                return;
            }

            if (!allowed)
            {
                l.Log(LogLevel.Warn, "Client asked for fast light change without permissions!");
                return;
            }

            switch (this.ActionType)
            {
                case Action.Add:
                {
                    lock (mo.FastLightsLock)
                    {
                        mo.FastLights.Add(this.Light);
                    }

                    break;
                }

                case Action.Delete:
                {
                    lock (mo.FastLightsLock)
                    {
                        mo.FastLights.RemoveAt(this.Index);
                    }

                    break;
                }

                case Action.Update:
                {
                    mo.FastLights[this.Index] = this.Light;
                    break;
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
            this.ActionType = c.Lookup(this.ActionType);
            if (this.ActionType != Action.Add)
            {
                this.Index = c.Lookup(this.Index);
            }

            if (this.ActionType != Action.Delete)
            {
                c.Lookup(this.Light ??= new FastLight());
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
