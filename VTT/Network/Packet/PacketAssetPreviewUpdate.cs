namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Asset;
    using VTT.Util;

    public class PacketAssetPreviewUpdate : PacketBase
    {
        public override uint PacketID => 78;
        public override bool Compressed => true;

        public Guid AssetID { get; set; }
        public byte[] NewPreviewBinary { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                if (!this.Sender.IsAdmin)
                {
                    server.Logger.Log(LogLevel.Warn, "Client asked for asset preview change without permissions!");
                    return;
                }

                AssetRef aRef = server.AssetManager.Refs.ContainsKey(this.AssetID) ? server.AssetManager.Refs[this.AssetID] : null;
                if (aRef == null)
                {
                    server.Logger.Log(LogLevel.Warn, "Client asked for asset preview update for non-existing asset!");
                    return;
                }

                string path = aRef.ServerPointer.FileLocation;
                string previewDir = Path.Combine(IOVTT.ServerDir, "Previews");
                Directory.CreateDirectory(previewDir);
                File.WriteAllBytes(Path.Combine(previewDir, this.AssetID.ToString() + ".png"), this.NewPreviewBinary);
                this.Broadcast();
            }
            else
            {
                client.DoTask(() => client.AssetManager.ClientAssetLibrary.Previews.EraseRecord(this.AssetID));
            }
        }
        
        public override void Decode(BinaryReader br)
        {
            this.AssetID = br.ReadGuid();
            if (this.IsServer)
            {
                this.NewPreviewBinary = br.ReadBytes(br.ReadInt32());
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.AssetID);
            if (!this.IsServer)
            {
                bw.Write(this.NewPreviewBinary.Length);
                bw.Write(this.NewPreviewBinary);
            }
        }
    }
}
