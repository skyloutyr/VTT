﻿namespace VTT.Network.Packet
{
    using System;
    using System.IO;

    public class PacketAssetPreview : PacketBase
    {
        public Guid ID { get; set; }
        public byte[] ImageBinary { get; set; }
        public AssetResponseType Response { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(VTT.Util.LogLevel.Info, "Got an asset preview request for " + this.ID + " by " + this.Sender.ID);
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
                        server.Logger.Log(VTT.Util.LogLevel.Error, "Could not locate preview for id " + this.ID);
                        server.Logger.Exception(VTT.Util.LogLevel.Error, e);
                        PacketAssetPreview pap = new PacketAssetPreview() { ID = this.ID, IsServer = true, ImageBinary = new byte[0], Session = sessionID, Response = AssetResponseType.NoAsset };
                        pap.Send(this.Sender);
                    }
                }
                else
                {
                    server.Logger.Log(VTT.Util.LogLevel.Warn, "Client " + this.Sender.ID + " requested an asset preview without being an administrator!");
                    PacketAssetPreview pap = new PacketAssetPreview() { ID = this.ID, IsServer = true, ImageBinary = new byte[0], Session = sessionID, Response = AssetResponseType.InternalError };
                    pap.Send(this.Sender);
                }
            }
            else
            {
                client.Logger.Log(VTT.Util.LogLevel.Info, "Got asset preview for id " + this.ID + ", result " + this.Response);
                client.AssetManager.ClientAssetLibrary.ReceivePreview(this.ID, this.Response, this.ImageBinary);
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
