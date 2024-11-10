namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Asset;
    using VTT.Util;

    public class PacketAssetDelete : PacketBase
    {
        public Guid RefID { get; set; }

        public override uint PacketID => 62;

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            l.Log(LogLevel.Info, "Got asset deletion packet");
            if (isServer)
            {
                if (!this.Sender.IsAdmin)
                {
                    server.Logger.Log(LogLevel.Warn, "Client requested asset deletion without permissions!");
                    return;
                }

                AssetManager am = server.AssetManager;
                if (am.Refs.TryGetValue(this.RefID, out AssetRef aref))
                {
                    am.Refs.Remove(this.RefID);
                    am.FindDirForRef(this.RefID)?.Refs.Remove(aref);
                    am.ServerAssetCache.DeleteCache(aref.AssetID);

                    string fLoc = aref.ServerPointer.FileLocation;
                    string jsLoc = fLoc + ".json";
                    string pLoc = Path.Combine(IOVTT.ServerDir, "Previews", aref.ServerPointer.PreviewPointer.ToString() + ".png");
                    
                    if (aref.Type == AssetType.Sound)
                    {
                        server.AssetManager.ServerSoundHeatmap.TryFreeAccessor(aref.AssetID);
                    }
                    
                    try
                    {
                        File.Delete(fLoc);
                    }
                    catch (Exception e)
                    {
                        l.Log(LogLevel.Error, $"Could not delete asset binary for {this.RefID}!");
                        l.Exception(LogLevel.Error, e);
                    }

                    try
                    {
                        File.Delete(jsLoc);
                    }
                    catch (Exception e)
                    {
                        l.Log(LogLevel.Error, $"Could not delete asset binary metadata for {this.RefID}!");
                        l.Exception(LogLevel.Error, e);
                    }

                    try
                    {
                        File.Delete(pLoc);
                    }
                    catch (Exception e)
                    {
                        l.Log(LogLevel.Error, $"Could not delete asset preview for {this.RefID}!");
                        l.Exception(LogLevel.Error, e);
                    }

                    l.Log(LogLevel.Info, $"Deleted asset {aref.AssetID} at {this.RefID}.");
                    this.Broadcast(x => x.IsAdmin);
                }
                else
                {
                    l.Log(LogLevel.Warn, $"Client asked for deletion of non-existing asset!");
                    return;
                }
            }
            else
            {
                Guid assetID = Guid.Empty;
                if (client.AssetManager.Refs.TryGetValue(this.RefID, out AssetRef aref))
                {
                    Client.Instance.DoTask(() =>
                    {
                        AssetDirectory ad = client.AssetManager.FindDirForRef(this.RefID);
                        ad?.Refs.Remove(aref);
                        client.AssetManager.Refs.Remove(this.RefID);
                    });

                    assetID = aref.AssetID;
                }

                if (!Guid.Empty.Equals(assetID))
                {
                    client.Logger.Log(LogLevel.Debug, "Erasing asset record.");
                    Client.Instance.DoTask(() => client.AssetManager.ClientAssetLibrary.Assets.EraseRecord(assetID));
                }
            }
        }

        public override void Decode(BinaryReader br) => this.RefID = br.ReadGuid();
        public override void Encode(BinaryWriter bw) => bw.Write(this.RefID);
    }
}
