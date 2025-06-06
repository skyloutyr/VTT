﻿namespace VTT.Network.Packet
{
    using Newtonsoft.Json;
    using System;
    using System.IO;
    using VTT.Asset;
    using VTT.Util;

    public class PacketChangeTextureMetadata : PacketBaseWithCodec
    {
        public override uint PacketID => 18;

        public Guid RefID { get; set; }
        public Guid AssetID { get; set; }
        public TextureData.Metadata Metadata { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                server.Logger.Log(LogLevel.Debug, "Got texture metadata change packet");
                if (!this.Sender.IsAdmin)
                {
                    server.Logger.Log(LogLevel.Warn, "Client requested texture metadata change without permissions!");
                    return;
                }

                AssetManager am = server.AssetManager;
                if (am.Refs.ContainsKey(this.RefID))
                {
                    AssetRef aRef = am.Refs[this.RefID];
                    if (aRef != null)
                    {
                        if (aRef.Meta.Type == AssetType.Texture)
                        {
                            aRef.Meta.TextureInfo = this.Metadata;
                            string path = aRef.ServerPointer.FileLocation;
                            File.WriteAllText(path + ".json", JsonConvert.SerializeObject(aRef.Meta));
                            this.Broadcast();
                        }
                        else
                        {
                            server.Logger.Log(LogLevel.Warn, "Client requested texture metadata change for non-texture!");
                            return;
                        }
                    }
                }
                else
                {
                    server.Logger.Log(LogLevel.Warn, "Client requested texture metadata change for non-existing texture!");
                    return;
                }
            }
            else
            {
                client.Logger.Log(LogLevel.Debug, "Got texture metadata change request");
                if (client.AssetManager.Refs.ContainsKey(this.RefID))
                {
                    client.AssetManager.Refs[this.RefID].Meta.TextureInfo = this.Metadata; // Update metadata
                }


                client.Logger.Log(LogLevel.Debug, "Erasing asset record.");
                client.DoTask(() => client.AssetManager.ClientAssetLibrary.Assets.EraseRecord(this.AssetID));
            }
        }

        public override void LookupData(Codec c)
        {
            this.RefID = c.Lookup(this.RefID);
            this.AssetID = c.Lookup(this.AssetID);
            c.Lookup(this.Metadata ??= new TextureData.Metadata());
        }
    }
}
