namespace VTT.Network.Packet
{
    using System;
    using System.IO;

    public class PacketAssetPreview : PacketBase
    {
        public Guid ID { get; set; }
        public byte[] ImageBinary { get; set; }
        public AssetResponseType Response { get; set; }
        public override uint PacketID => 5;
        public override bool Compressed => true;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(Util.LogLevel.Info, "Got an asset preview request for " + this.ID + " by " + this.Sender.ID);
                if (this.Sender.IsAdmin)
                {
                    try
                    {
                        byte[] preview = server.AssetManager.GetServerPreview(this.ID);
                        PacketAssetPreview pap = new PacketAssetPreview() { ID = this.ID, IsServer = true, ImageBinary = preview, Session = sessionID, Response = AssetResponseType.Ok };
                        pap.Send(this.Sender);
                    }
                    catch (Exception e)
                    {
                        server.Logger.Log(Util.LogLevel.Error, "Could not locate preview for id " + this.ID);
                        server.Logger.Exception(Util.LogLevel.Error, e);
                        PacketAssetPreview pap = new PacketAssetPreview() { ID = this.ID, IsServer = true, ImageBinary = Array.Empty<byte>(), Session = sessionID, Response = AssetResponseType.NoAsset };
                        pap.Send(this.Sender);
                    }
                }
                else
                {
                    server.Logger.Log(Util.LogLevel.Warn, "Client " + this.Sender.ID + " requested an asset preview without being an administrator!");
                    PacketAssetPreview pap = new PacketAssetPreview() { ID = this.ID, IsServer = true, ImageBinary = Array.Empty<byte>(), Session = sessionID, Response = AssetResponseType.InternalError };
                    pap.Send(this.Sender);
                }
            }
            else
            {
                client.Logger.Log(Util.LogLevel.Info, "Got asset preview for id " + this.ID + ", result " + this.Response);
                client.AssetManager.ClientAssetLibrary.Previews.ReceiveAsync(this.ID, Asset.AssetType.Texture, this.Response, this.ImageBinary, null);
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.ID = new Guid(br.ReadBytes(16));
            if (!this.IsServer)
            {
                this.Response = (AssetResponseType)br.ReadInt32();
                this.ImageBinary = br.ReadBytes(br.ReadInt32());
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.ID.ToByteArray());
            if (this.IsServer)
            {
                bw.Write((int)this.Response);
                bw.Write(this.ImageBinary.Length);
                bw.Write(this.ImageBinary);
            }
        }
    }
}
