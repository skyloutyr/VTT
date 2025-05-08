namespace VTT.Network.Packet
{
    using System;
    using VTT.Util;

    public class PacketPlaySoundAsset : PacketBaseWithCodec
    {
        public override uint PacketID => 69;

        public Guid SoundID { get; set; }
        public bool Stop { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer)
            {
                if (!this.Sender.IsAdmin)
                {
                    server.Logger.Log(LogLevel.Warn, $"Client {this.Sender.ID} asked to broadcast a sound without permissions!");
                    return;
                }

                if (!server.AssetManager.Refs.TryGetValue(this.SoundID, out Asset.AssetRef asset) || (asset?.Type != Asset.AssetType.Sound))
                {
                    server.Logger.Log(LogLevel.Warn, $"Client asked to broadcast an asset {this.SoundID} as a sound, but the asset is not a sound.");
                    return;
                }

                this.Broadcast();
            }
            else
            {
                if (this.Stop)
                {
                    client.Frontend.Sound.StopAsset(this.SoundID);
                }
                else
                {
                    client.Frontend.Sound.PlayAsset(this.SoundID);
                }
            }
        }

        public override void LookupData(Codec c)
        {
            this.SoundID = c.Lookup(this.SoundID);
            this.Stop = c.Lookup(this.Stop);
        }
    }
}
