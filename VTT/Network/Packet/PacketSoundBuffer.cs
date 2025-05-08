namespace VTT.Network.Packet
{
    using System;
    using VTT.Asset;
    using VTT.Util;

    public class PacketSoundBuffer : PacketBaseWithCodec
    {
        public override uint PacketID => 68;
        public override bool Compressed => true;

        public Guid SoundID { get; set; }
        public Guid AssetID { get; set; }
        public int ChunkIndex { get; set; }
        public byte[] ServerChunkData { get; set; }
        public AssetStatus ServerReturnStatus { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            if (isServer) // Client asked for a sound buffer
            {
                AssetManager mgr = server.AssetManager;
                this.ServerReturnStatus = mgr.ServerSoundHeatmap.Get(this.AssetID, this.ChunkIndex, out AssetSoundHeatmap.Spot spot);
                if (spot != null)
                {
                    this.ServerChunkData = spot.Data;
                }

                this.Send(this.Sender); // Send data back
            }
            else // Got buffer response from server
            {
                if (this.ServerReturnStatus == AssetStatus.Return && this.ServerChunkData != null && this.ServerChunkData.Length > 0)
                {
                    client.Frontend.Sound.ReceiveSoundBuffer(this.SoundID, this.AssetID, this.ChunkIndex, this.ServerChunkData);
                }
                else
                {
                    client.Logger.Log(LogLevel.Error, "Could not get server sound info for sound " + this.AssetID + " chunk " + this.ChunkIndex + ", got " + this.ServerReturnStatus + " in response!");
                }
            }
        }

        public override void LookupData(Codec c)
        {
            this.SoundID = c.Lookup(this.SoundID);
            this.AssetID = c.Lookup(this.AssetID);
            this.ChunkIndex = c.Lookup(this.ChunkIndex);
            this.ServerReturnStatus = c.Lookup(this.ServerReturnStatus);
            this.ServerChunkData = c.Lookup(this.ServerChunkData);
        }
    }
}
