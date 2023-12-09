﻿namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Util;

    public class PacketPlaySoundAsset : PacketBase
    {
        public override uint PacketID => 69;

        public Guid SoundID { get; set; }

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
                client.Frontend.Sound.PlayAsset(this.SoundID);
            }
        }

        public override void Decode(BinaryReader br) => this.SoundID = br.ReadGuid();
        public override void Encode(BinaryWriter bw) => bw.Write(this.SoundID);
    }
}