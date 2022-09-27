namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Asset;

    public class PacketAssetResponse : PacketBase
    {
        public Guid AssetID { get; set; }
        public byte[] Binary { get; set; }
        public AssetMetadata Metadata { get; set; }
        public AssetType AssetType { get; set; }
        public AssetResponseType ResponseType { get; set; }
        public override uint PacketID => 8;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (!isServer)
            {
                client.Logger.Log(VTT.Util.LogLevel.Debug, "Got asset response for " + this.AssetID + ", " + this.ResponseType);
                ClientAssetLibrary cal = client.AssetManager.ClientAssetLibrary;
                if (this.ResponseType == AssetResponseType.Ok)
                {
                    cal.ReceiveAsset(this.AssetID, this.AssetType, this.Binary, this.Metadata);
                }
                else 
                {
                    cal.ErrorAsset(this.AssetID, this.ResponseType);
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.AssetID = new Guid(br.ReadBytes(16));
            this.AssetType = (AssetType)br.ReadInt32();
            this.ResponseType = (AssetResponseType)br.ReadInt32();
            this.Binary = br.ReadBytes(br.ReadInt32());
            this.Metadata = new AssetMetadata(br);
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.AssetID.ToByteArray());
            bw.Write((int)this.AssetType);
            bw.Write((int)this.ResponseType);
            bw.Write(this.Binary.Length);
            bw.Write(this.Binary);
            this.Metadata.Serialize().Write(bw);
        }
    }
}
