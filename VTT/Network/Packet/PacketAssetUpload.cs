namespace VTT.Network.Packet
{
    using Newtonsoft.Json;
    using System;
    using System.IO;
    using VTT.Asset;
    using VTT.Util;

    public class PacketAssetUpload : PacketBase
    {
        public byte[] AssetBinary { get; set; }
        public byte[] AssetPreview { get; set; }
        public string Path { get; set; }
        public AssetMetadata Meta { get; set; }
        public override uint PacketID => 10;
        public override bool Compressed => true;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(LogLevel.Info, "Asset upload packet received");
                if (this.Sender.IsAdmin)
                {
                    AssetDirectory ad = server.AssetManager.GetDirAt(this.Path);
                    Guid id = Guid.NewGuid();
                    string path = System.IO.Path.Combine(server.AssetManager.GetFSPath(ad), id + ".ab");
                    File.WriteAllBytes(path, this.AssetBinary);
                    File.WriteAllText(path + ".json", JsonConvert.SerializeObject(this.Meta));
                    string previewDir = System.IO.Path.Combine(IOVTT.ServerDir, "Previews");
                    Directory.CreateDirectory(previewDir);
                    File.WriteAllBytes(System.IO.Path.Combine(previewDir, id.ToString() + ".png"), this.AssetPreview);
                    server.Logger.Log(LogLevel.Info, "Saved asset as " + path);
                    AssetBinaryPointer abp = new AssetBinaryPointer() { FileLocation = path, PreviewPointer = id };
                    AssetRef aRef = new AssetRef() { AssetID = id, AssetPreviewID = id, IsServer = true, ServerPointer = abp, Meta = this.Meta };
                    ad.Refs.Add(aRef);
                    server.AssetManager.Refs[aRef.AssetID] = aRef;
                    server.Logger.Log(LogLevel.Info, "Asset reference added at " + ad.GetPath() + ":" + aRef.Name + ", id " + aRef.AssetID);
                    new PacketAssetDef() { ActionType = AssetDefActionType.Add, Root = this.Path, Ref = aRef }.Broadcast(c => c.IsAdmin);
                }
                else
                {
                    server.Logger.Log(LogLevel.Warn, "Client " + this.Sender.ID + " attempted to upload an asset without being an administrator!");
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.AssetBinary = br.ReadBytes(br.ReadInt32());
            this.AssetPreview = br.ReadBytes(br.ReadInt32());
            this.Path = br.ReadString();
            this.Meta = new AssetMetadata();
            DataElement e = new DataElement(br);
            this.Meta.Deserialize(e);
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.AssetBinary.Length);
            bw.Write(this.AssetBinary);
            bw.Write(this.AssetPreview.Length);
            bw.Write(this.AssetPreview);
            bw.Write(this.Path);
            DataElement e = this.Meta.Serialize();
            e.Write(bw);
        }
    }
}
