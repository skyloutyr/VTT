namespace VTT.Network.Packet
{
    using Newtonsoft.Json;
    using System;
    using System.IO;
    using VTT.Asset;
    using VTT.Util;

    public class PacketAssetRename : PacketBase
    {
        public Guid RefID { get; set; }
        public string Name { get; set; }
        public override uint PacketID => 6;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            this.GetContextLogger().Log(LogLevel.Debug, "Got asset rename packet for " + this.RefID);
            if (isServer)
            {
                if (this.Sender.IsAdmin)
                {
                    if (server.AssetManager.Refs.ContainsKey(this.RefID))
                    {
                        AssetRef aRef = server.AssetManager.Refs[RefID];
                        bool isSoundNfo = false;
                        if (AssetBinaryPointer.ReadAssetMetadata(aRef.ServerPointer.FileLocation, out AssetMetadata am))
                        {
                            am.Name = this.Name;
                            if (am.Type == AssetType.Sound && am.SoundInfo != null)
                            {
                                am.SoundInfo.SoundAssetName = this.Name;
                                isSoundNfo = true;
                            }

                            if (am.ConstructedFromOldBinaryEncoding)
                            {
                                AssetBinaryPointer.ChangeAssetNameForOldEncoding(aRef.ServerPointer.FileLocation, am);
                                server.Logger.Log(LogLevel.Warn, "Metadata change forced asset version migration.");
                            }
                            else
                            {
                                File.WriteAllText(aRef.ServerPointer.FileLocation + ".json", JsonConvert.SerializeObject(am));
                            }
                        }
                        else
                        {
                            server.Logger.Log(LogLevel.Warn, "Client asked for asset name change for an asset but there was a trouble retreiving metadata. Asset name change will not persist!");
                        }

                        aRef.Name = this.Name;
                        this.Broadcast(c => isSoundNfo || c.IsAdmin);
                    }
                    else
                    {
                        server.Logger.Log(LogLevel.Warn, "Client asked for asset name change for non-existing asset!");
                        return;
                    }
                }
                else
                {
                    server.Logger.Log(LogLevel.Warn, "Client asked for asset name change without permissions!");
                    return;
                }
            }
            else
            {
                if (client.AssetManager.Refs.ContainsKey(this.RefID))
                {
                    AssetRef aRef = client.AssetManager.Refs[this.RefID];
                    aRef.Name = this.Name;
                    if (client.AssetManager.Assets.TryGetValue(this.RefID, out Asset a) && a != null && a.Type == AssetType.Sound && a.Sound?.Meta != null)
                    {
                        a.Sound.Meta.SoundAssetName = this.Name;
                    }
                }
                else
                {
                    if (client.AssetManager.Assets.TryGetValue(this.RefID, out Asset a) && a != null && a.Type == AssetType.Sound && a.Sound?.Meta != null)
                    {
                        a.Sound.Meta.SoundAssetName = this.Name;
                    }
                    else
                    {
                        client.Logger.Log(LogLevel.Warn, "Got asset name change packet for non-existing asset!");
                    }
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.RefID = new Guid(br.ReadBytes(16));
            this.Name = br.ReadString();
        }

        public override void Encode(BinaryWriter bw)
        {
            bw.Write(this.RefID.ToByteArray());
            bw.Write(this.Name);
        }
    }
}
