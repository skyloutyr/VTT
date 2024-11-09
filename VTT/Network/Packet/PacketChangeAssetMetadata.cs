namespace VTT.Network.Packet
{
    using Newtonsoft.Json;
    using System;
    using System.IO;
    using VTT.Asset;
    using VTT.Util;

    public class PacketChangeAssetMetadata : PacketBase
    {
        public Guid RefID { get; set; }
        public Guid AssetID { get; set; }
        public AssetMetadata NewMeta { get; set; }
        public override uint PacketID => 59;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(LogLevel.Debug, "Got asset metadata change packet");
                if (!this.Sender.IsAdmin)
                {
                    server.Logger.Log(LogLevel.Warn, "Client requested asset metadata change without permissions!");
                    return;
                }

                AssetManager am = server.AssetManager;
                if (am.Refs.ContainsKey(this.RefID))
                {
                    AssetRef aRef = am.Refs[this.RefID];
                    if (aRef != null)
                    {
                        aRef.Meta = this.NewMeta;
                        string path = aRef.ServerPointer.FileLocation;
                        File.WriteAllText(path + ".json", JsonConvert.SerializeObject(aRef.Meta));
                        this.Broadcast();
                    }
                }
                else
                {
                    server.Logger.Log(LogLevel.Warn, "Client requested asset metadata change for non-existing asset!");
                    return;
                }
            }
            else
            {
                client.Logger.Log(LogLevel.Debug, "Got asset metadata change request");
                if (client.AssetManager.Refs.ContainsKey(this.RefID))
                {
                    client.AssetManager.Refs[this.RefID].Meta = this.NewMeta; // Update metadata
                }

                Client.Instance.DoTask(() => client.AssetManager.ClientAssetLibrary.Assets.EraseRecord(this.AssetID));
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.RefID = br.ReadGuid();
            this.AssetID = br.ReadGuid();
            this.NewMeta = new AssetMetadata(br);
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.RefID);
            bw.Write(this.AssetID);
            this.NewMeta.Serialize().Write(bw);
        }
    }
}
