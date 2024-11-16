namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using System.Numerics;
    using VTT.Control;
    using VTT.Util;

    public class PacketShadow2DBoxChangeProperty : PacketBase
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

        public override void Decode(BinaryReader br)
        {
            this.MapID = br.ReadGuid();
            this.BoxID = br.ReadGuid();
            this.ChangeType = br.ReadEnumSmall<PropertyType>();
            switch (this.ChangeType)
            {
                case PropertyType.Position:
                {
                    this.Property = br.ReadVec4();
                    break;
                }

                case PropertyType.Rotation:
                {
                    this.Property = br.ReadSingle();
                    break;
                }

                case PropertyType.IsActive:
                {
                    this.Property = br.ReadBoolean();
                    break;
                }
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.MapID);
            bw.Write(this.BoxID);
            bw.WriteEnumSmall(this.ChangeType);
            switch (this.ChangeType)
            {
                case PropertyType.Position:
                {
                    bw.Write((Vector4)this.Property);
                    break;
                }

                case PropertyType.Rotation:
                {
                    bw.Write((float)this.Property);
                    break;
                }

                case PropertyType.IsActive:
                {
                    bw.Write((bool)this.Property);
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
