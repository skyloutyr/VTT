namespace VTT.Network.Packet
{
    using System;
    using VTT.Asset;

    public class PacketAssetResponse : PacketBaseWithCodec
    {
        public override uint PacketID => 8;
        public override bool Compressed => true;

        public Guid AssetID { get; set; }
        public byte[] Binary { get; set; }
        public AssetMetadata Metadata { get; set; }
        public AssetType AssetType { get; set; }
        public AssetResponseType ResponseType { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (!isServer)
            {
                client.Logger.Log(Util.LogLevel.Debug, "Got asset response for " + this.AssetID + ", " + this.ResponseType);
                ClientAssetLibrary cal = client.AssetManager.ClientAssetLibrary;
                if (this.ResponseType == AssetResponseType.Ok)
                {
                    cal.Assets.ReceiveAsync(this.AssetID, this.AssetType, this.ResponseType, this.Binary, this.Metadata);
                }
                else
                {
                    cal.Assets.ReceiveAsync(this.AssetID, this.AssetType, this.ResponseType, null, null);
                }
            }
        }

        public override void LookupData(Codec c)
        {
            this.AssetID = c.Lookup(this.AssetID);
            this.AssetType = c.Lookup(this.AssetType);
            this.ResponseType = c.Lookup(this.ResponseType);
            this.Binary = c.Lookup(this.Binary);
            c.Lookup(this.Metadata ??= new AssetMetadata());
        }
    }
}
