namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketDeleteShadow2DBox : PacketBase
    {
        public override uint PacketID => 76;
        public Guid MapID { get; set; }
        public Guid BoxID { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger logger = this.ContextLogger;
            if (isServer)
            {
                if (!this.Sender.IsAdmin)
                {
                    logger.Log(LogLevel.Warn, $"Client {this.Sender.ID} attempted to delete a 2d shadow rectangle without permissions!");
                    return;
                }
            }

            Map m = null;
            if (isServer)
            {
                if (!server.TryGetMap(this.MapID, out m))
                {
                    logger.Log(LogLevel.Warn, $"Client {this.Sender.ID} attempted to delete a 2d shadow rectangle for a non-existing map!");
                    return;
                }
            }
            else
            {
                if (client.CurrentMap == null || !Guid.Equals(client.CurrentMap.ID, this.MapID))
                {
                    logger.Log(LogLevel.Info, $"Server asked to delete a 2d shadow rectangle for non-current map. Aborting.");
                    return;
                }
                else
                {
                    m = client.CurrentMap;
                }
            }

            if (m == null)
            {
                logger.Log(LogLevel.Warn, $"Could not delete a 2d shadow rectangle for non-existing map!");
                return;
            }

            if (!m.Is2D)
            {
                logger.Log(LogLevel.Warn, $"Asked for a 2d shadow rectangle deletion for a non-2d map, which is not allowed.");
                // Explicitly not returning here, though the op is illegal, just logging
            }

            if (!m.ShadowLayer2D.TryGetBox(this.BoxID, out Shadow2DBox box))
            {
                logger.Log(LogLevel.Warn, $"Can't delete { this.BoxID } as this box doesn't exist!");
                return;
            }

            logger.Log(LogLevel.Debug, $"Deleted shadow box {this.BoxID}.");
            m.ShadowLayer2D.RemoveBox(this.BoxID, !isServer);
            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast(x => x.ClientMapID.Equals(this.MapID));
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.MapID = br.ReadGuid();
            this.BoxID = br.ReadGuid();
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.MapID);
            bw.Write(this.BoxID);
        }
    }
}
