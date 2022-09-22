namespace VTT.Network.Packet
{
    using Newtonsoft.Json;
    using System;
    using System.IO;
    using VTT.Asset;

    public class PacketAssetRename : PacketBase
    {
        public Guid RefID { get; set; }
        public string Name { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            this.GetContextLogger().Log(VTT.Util.LogLevel.Debug, "Got asset rename packet for " + this.RefID);
            if (isServer)
            {
                if (this.Sender.IsAdmin)
                {
                    if (server.AssetManager.Refs.ContainsKey(this.RefID))
                    {
                        AssetRef aRef = server.AssetManager.Refs[RefID];
                        if (AssetBinaryPointer.ReadAssetMetadata(aRef.ServerPointer.FileLocation, out AssetMetadata am))
                        {
                            am.Name = this.Name;
                            if (am.ConstructedFromOldBinaryEncoding)
                            {
                                AssetBinaryPointer.ChangeAssetNameForOldEncoding(aRef.ServerPointer.FileLocation, am);
                                server.Logger.Log(VTT.Util.LogLevel.Warn, "Metadata change forced asset version migration.");
                            }
                            else
                            {
                                File.WriteAllText(aRef.ServerPointer.FileLocation + ".json", JsonConvert.SerializeObject(am));
                            }
                        }
                        else
                        {
                            server.Logger.Log(VTT.Util.LogLevel.Warn, "Client asked for asset name change for an asset but there was a trouble retreiving metadata. Asset name change will not persist!");
                        }

                        aRef.Name = this.Name;
                        this.Broadcast(c => c.IsAdmin);
                    }
                    else
                    {
                        server.Logger.Log(VTT.Util.LogLevel.Warn, "Client asked for asset name change for non-existing asset!");
                        return;
                    }
                }
                else
                {
                    server.Logger.Log(VTT.Util.LogLevel.Warn, "Client asked for asset name change without permissions!");
                    return;
                }
            }
            else
            {
                if (client.AssetManager.Refs.ContainsKey(this.RefID))
                {
                    AssetRef aRef = client.AssetManager.Refs[this.RefID];
                    aRef.Name = this.Name;
                }
                else
                {
                    client.Logger.Log(VTT.Util.LogLevel.Warn, "Got asset name change packet for non-existing asset!");
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
