namespace VTT.Network.Packet
{
    using System;
    using VTT.Control;
    using VTT.Util;

    public class PacketSetMapSkyboxAsset : PacketBaseWithCodec
    {
        public override uint PacketID => 83;

        public Guid MapID { get; set; }
        public bool IsNightSkybox { get; set; }
        public Guid AssetID { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                if (!this.Sender.IsAdmin)
                {
                    this.ContextLogger.Log(LogLevel.Warn, $"Client {this.Sender.ID} tried to change the map skybox without permissions!");
                    return;
                }
            }

            Map m = isServer ? server.GetExistingMap(this.MapID) : client.CurrentMapIfMatches(this.MapID);
            if (m == null)
            {
                this.ContextLogger.Log(LogLevel.Warn, $"Can't change map skybox for non-existing map {this.MapID}");
                return;
            }

            if (this.IsNightSkybox)
            {
                m.NightSkyboxAssetID = this.AssetID;
            }
            else
            {
                m.DaySkyboxAssetID = this.AssetID;
            }

            if (isServer)
            {
                m.NeedsSave = true;
                this.Broadcast(x => x.ClientMapID.Equals(this.MapID));
            }
        }

        public override void LookupData(Codec c)
        {
            this.MapID = c.Lookup(this.MapID);
            this.IsNightSkybox = c.Lookup(this.IsNightSkybox);
            this.AssetID = c.Lookup(this.AssetID);
        }
    }
}
