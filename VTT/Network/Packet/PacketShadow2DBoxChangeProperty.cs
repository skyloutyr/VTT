namespace VTT.Network.Packet
{
    using System;
    using System.Numerics;
    using VTT.Control;
    using VTT.Util;

    public class PacketShadow2DBoxChangeProperty : PacketBaseWithCodec
    {
        public override uint PacketID => 75;

        public Guid MapID { get; set; }
        public Guid BoxID { get; set; }
        public object Property { get; set; }
        public PropertyType ChangeType { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger logger = this.ContextLogger;
            if (isServer)
            {
                if (!this.Sender.IsAdmin)
                {
                    logger.Log(LogLevel.Warn, $"Client { this.Sender.ID } attempted a change to the 2d shadow rectangle property without permissions!");
                    return;
                }
            }

            Map m =  null;
            if (isServer)
            {
                if (!server.TryGetMap(this.MapID, out m))
                {
                    logger.Log(LogLevel.Warn, $"Client {this.Sender.ID} attempted a change to the 2d shadow rectangle property for a non-existing map!");
                    return;
                }
            }
            else
            {
                if (client.CurrentMap == null || !Guid.Equals(client.CurrentMap.ID, this.MapID))
                {
                    logger.Log(LogLevel.Info, $"Server asked to change map 2d shadow rectangle property for non-current map. Aborting.");
                    return;
                }
                else
                {
                    m = client.CurrentMap;
                }
            }

            if (m == null)
            {
                logger.Log(LogLevel.Warn, $"Could not change 2d shadow rectangle property for non-existing map!");
                return;
            }

            if (!m.Is2D)
            {
                logger.Log(LogLevel.Warn, $"Asked for a 2d shadow rectangle property change for a non-2d map, which is not allowed.");
                // Explicitly not returning here, though the op is illegal, just logging
            }

            if (!m.ShadowLayer2D.TryGetBox(this.BoxID, out Shadow2DBox box))
            {
                logger.Log(LogLevel.Warn, $"Can't change property { this.ChangeType } for a non-existing shadow 2d box!");
                return;
            }

            switch (this.ChangeType)
            {
                case PropertyType.Position:
                {
                    Vector4 v = (Vector4)this.Property;
                    box.Start = v.Xy();
                    box.End = v.Zw();
                    break;
                }

                case PropertyType.Rotation:
                {
                    box.Rotation = (float)this.Property;
                    break;
                }

                case PropertyType.IsActive:
                {
                    box.IsActive = (bool)this.Property;
                    break;
                }
            }

            logger.Log(LogLevel.Debug, $"Changed a 2d shadow box property of type { this.ChangeType }");
            if (!isServer) // Servers don't build a BVH, just keep data
            {
                m.ShadowLayer2D.NotifyOfAnyChange();
            }
            else
            {
                this.Broadcast(x => x.ClientMapID.Equals(this.MapID));
            }
        }

        public override void LookupData(Codec c)
        {
            this.MapID = c.Lookup(this.MapID);
            this.BoxID = c.Lookup(this.BoxID);
            this.ChangeType = c.Lookup(this.ChangeType);
            switch (this.ChangeType)
            {
                case PropertyType.Position:
                {
                    this.Property = c.LookupBox<Vector4>(this.Property, c.Lookup);
                    break;
                }

                case PropertyType.Rotation:
                {
                    this.Property = c.LookupBox<float>(this.Property, c.Lookup);
                    break;
                }

                case PropertyType.IsActive:
                {
                    this.Property = c.LookupBox<bool>(this.Property, c.Lookup);
                    break;
                }
            }
        }

        public enum PropertyType
        {
            Position,
            Rotation,
            IsActive
        }
    }
}
