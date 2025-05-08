namespace VTT.Network.Packet
{
    using System;
    using VTT.Asset;

    public class PacketAssetRequest : PacketBaseWithCodec
    {
        public override uint PacketID => 7;

        public Guid AssetID { get; set; }
        public AssetType AssetType { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                ServerClient sc = (ServerClient)server.FindSession(sessionID);
                AssetManager am = server.AssetManager;
                server.Logger.Log(Util.LogLevel.Debug, "Client " + sc.ID + " asked for asset at " + this.AssetID);
                if (am.Refs.ContainsKey(this.AssetID))
                {
                    try
                    {
                        AssetRef aRef = am.Refs[this.AssetID];
                        byte[] binary = aRef.Type == AssetType.Sound && !aRef.Meta.SoundInfo.IsFullData
                            ? AssetBinaryPointer.EmptyHeaderV1
                            : am.ServerAssetCache.GetBinary(this.AssetID);

                        PacketAssetResponse par = new PacketAssetResponse() { AssetID = this.AssetID, AssetType = aRef.Type, Binary = binary, Metadata = aRef.Meta, IsServer = true, ResponseType = AssetResponseType.Ok, Session = sessionID };
                        par.Send(sc);
                        server.Logger.Log(Util.LogLevel.Debug, "Sent client asset");
                    }
                    catch (Exception e)
                    {
                        PacketAssetResponse par = new PacketAssetResponse() { AssetID = this.AssetID, AssetType = this.AssetType, Binary = Array.Empty<byte>(), Metadata = AssetMetadata.Broken, IsServer = true, ResponseType = AssetResponseType.InternalError, Session = sessionID };
                        par.Send(sc);
                        server.Logger.Log(Util.LogLevel.Error, "Internal server error while sending asset!");
                        server.Logger.Exception(Util.LogLevel.Error, e);
                    }
                }
                else
                {
                    PacketAssetResponse par = new PacketAssetResponse() { AssetID = this.AssetID, AssetType = this.AssetType, Binary = Array.Empty<byte>(), Metadata = AssetMetadata.Broken, IsServer = true, ResponseType = AssetResponseType.NoAsset, Session = sessionID };
                    par.Send(sc);
                    server.Logger.Log(Util.LogLevel.Warn, "Client requested a non-existing asset!");
                }
            }
        }

        public override void LookupData(Codec c)
        {
            this.AssetID = c.Lookup(this.AssetID);
            this.AssetType = c.Lookup(this.AssetType);
        }
    }
}
