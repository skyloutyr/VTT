namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketAddShadow2DBox : PacketBase
    {
        public override uint PacketID => 77;
        public Guid MapID { get; set; }
        public Shadow2DBox Box { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger logger = this.GetContextLogger();
            if (isServer)
            {
                if (!this.Sender.IsAdmin)
                {
                    logger.Log(LogLevel.Warn, $"Client {this.Sender.ID} attempted to create a 2d shadow rectangle without permissions!");
                    return;
                }
            }

            Map m = null;
            if (isServer)
            {
                if (!server.TryGetMap(this.MapID, out m))
                {
                    logger.Log(LogLevel.Warn, $"Client {this.Sender.ID} attempted to create a 2d shadow rectangle for a non-existing map!");
                    return;
                }
            }
            else
            {
                if (client.CurrentMap == null || !Guid.Equals(client.CurrentMap.ID, this.MapID))
                {
                    logger.Log(LogLevel.Info, $"Server asked to create a 2d shadow rectangle for non-current map. Aborting.");
                    return;
                }
                else
                {
                    m = client.CurrentMap;
                }
            }

            if (m == null)
            {
                logger.Log(LogLevel.Warn, $"Could not create a 2d shadow rectangle for non-existing map!");
                return;
            }

            if (!m.Is2D)
            {
                logger.Log(LogLevel.Warn, $"Asked for a 2d shadow rectangle creation for a non-2d map, which is not allowed.");
                return;
            }

            if (m.ShadowLayer2D.TryGetBox(this.Box.BoxID, out _))
            {
                logger.Log(LogLevel.Warn, $"Can't create a new box as a box with the same ID already exists!");
                return;
            }

            logger.Log(LogLevel.Debug, $"Created new shadow box.");
            m.ShadowLayer2D.AddBox(this.Box.FullClone(), !isServer);
            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast(x => x.ClientMapID.Equals(this.MapID));
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.MapID = br.ReadGuid();
            this.Box = new Shadow2DBox();
            this.Box.Deserialize(new DataElement(br));
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.MapID);
            this.Box.Serialize().Write(bw);
        }
    }
}
