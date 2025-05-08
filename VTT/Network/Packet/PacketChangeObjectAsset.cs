namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketChangeObjectAsset : PacketBaseWithCodec
    {
        public override uint PacketID => 16;

        public Guid MapID { get; set; }
        public Guid ObjectID { get; set; }
        public Guid AssetID { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Debug, "Got object asset change packet");
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

                if (!server.AssetManager.Refs.ContainsKey(this.AssetID))
                {
                    l.Log(LogLevel.Warn, "Got object asset change packet for non existing asset, discarding.");
                    return;
                }
            }
            else
            {
                m = client.CurrentMapIfMatches(this.MapID);
                m?.GetObject(this.ObjectID, out mo);
            }

            if (m == null)
            {
                l.Log(LogLevel.Warn, "Got object asset change packet for non existing map, discarding.");
                return;
            }

            if (mo == null)
            {
                l.Log(LogLevel.Warn, "Got object asset change packet for non existing object, discarding.");
                return;
            }

            if (!allowed)
            {
                l.Log(LogLevel.Warn, "Client asked for object asset change without permissions!");
                return;
            }

            mo.AssetID = this.AssetID;
            if (!isServer) // Reset asset cached AABB
            {
                mo.ClientBoundingBox = new(-0.5f, -0.5f, -0.5f, 0.5f, 0.5f, 0.5f);
                mo.ClientAssignedModelBounds = false;
            }
            else
            {
                m.NeedsSave = true;
                this.Broadcast(c => c.ClientMapID.Equals(this.MapID));
            }
        }

        public override void LookupData(Codec c)
        {
            this.MapID = c.Lookup(this.MapID);
            this.ObjectID = c.Lookup(this.ObjectID);
            this.AssetID = c.Lookup(this.AssetID);
        }
    }
}
