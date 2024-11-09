namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Asset;
    using VTT.Util;

    public class PacketAssetUpdate : PacketBase
    {
        public Guid AssetID { get; set; }
        public byte[] NewBinary { get; set; }
        public byte[] NewPreviewBinary { get; set; }
        public override uint PacketID => 9;
        public override bool Compressed => true;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                if (!this.Sender.IsAdmin)
                {
                    server.Logger.Log(LogLevel.Warn, "Client asked for asset change without permissions!");
                    return;
                }

                AssetRef aRef = server.AssetManager.Refs.ContainsKey(this.AssetID) ? server.AssetManager.Refs[this.AssetID] : null;
                if (aRef == null)
                {
                    server.Logger.Log(LogLevel.Warn, "Client asked for asset update for non-existing asset!");
                    return;
                }

                string path = aRef.ServerPointer.FileLocation;
                File.WriteAllBytes(path, this.NewBinary);
                server.AssetManager.ServerAssetCache.DeleteCache(aRef.AssetID);
                if (this.NewPreviewBinary.Length > 0)
                {
                    string previewDir = Path.Combine(IOVTT.ServerDir, "Previews");
                    Directory.CreateDirectory(previewDir);
                    File.WriteAllBytes(Path.Combine(previewDir, this.AssetID.ToString() + ".png"), this.NewPreviewBinary);
                }

                this.Broadcast();
            }
            else
            {
                client.DoTask(() => client.AssetManager.ClientAssetLibrary.Assets.EraseRecord(this.AssetID));
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.AssetID = br.ReadGuid();
            if (this.IsServer)
            {
                this.NewBinary = br.ReadBytes(br.ReadInt32());
                this.NewPreviewBinary = br.ReadBytes(br.ReadInt32());
            }
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.AssetID);
            if (!this.IsServer)
            {
                bw.Write(this.NewBinary.Length);
                bw.Write(this.NewBinary);
                bw.Write(this.NewPreviewBinary.Length);
                bw.Write(this.NewPreviewBinary);
            }
        }
    }
}
