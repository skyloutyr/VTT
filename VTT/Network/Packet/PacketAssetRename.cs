namespace VTT.Network.Packet
{
    using Newtonsoft.Json;
    using System;
    using System.IO;
    using VTT.Asset;
    using VTT.Util;

    public class PacketAssetRename : PacketBaseWithCodec
    {
        public override uint PacketID => 6;

        public Guid RefID { get; set; }
        public string Name { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            this.            ContextLogger.Log(LogLevel.Debug, "Got asset rename packet for " + this.RefID);
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
                client.DoTask(() => {
                    if (client.AssetManager.Refs.ContainsKey(this.RefID))
                    {
                        AssetRef aRef = client.AssetManager.Refs[this.RefID];
                        aRef.Name = this.Name;
                        client.AssetManager.ClientAssetLibrary.Assets.TryUpdate(this.RefID, a =>
                        {
                            if (a != null && a.Type == AssetType.Sound && a.Sound?.Meta != null)
                            {
                                a.Sound.Meta.SoundAssetName = this.Name;
                            }
                        });
                    }
                    else
                    {
                        bool result = client.AssetManager.ClientAssetLibrary.Assets.TryUpdate(this.RefID, a =>
                        {
                            if (a != null && a.Type == AssetType.Sound && a.Sound?.Meta != null)
                            {
                                a.Sound.Meta.SoundAssetName = this.Name;
                            }
                        });

                        if (!result)
                        {
                            client.Logger.Log(LogLevel.Warn, "Got asset name change packet for non-existing asset!");
                        }
                    }
                });
            }
        }

        public override void LookupData(Codec c)
        {
            this.RefID = c.Lookup(this.RefID);
            this.Name = c.Lookup(this.Name);
        }
    }
}
