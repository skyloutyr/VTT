namespace VTT.Network.Packet
{
    using System;
    using VTT.Util;

    public class PacketMusicPlayerFullData : PacketBaseWithCodec
    {
        public override uint PacketID => 70;

        public DataElement SerializedMusicPlayer { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            if (isServer) // ?
            {
                l.Log(LogLevel.Warn, $"Client {this.Sender.ID} sent a music player full data packet which is not allowed, but interpreting as request.");
                this.SerializedMusicPlayer = server.MusicPlayer.Serialize();
                this.Send(this.Sender);
            }
            else
            {
                client?.Frontend?.Sound?.MusicPlayer.Deserialize(this.SerializedMusicPlayer);
                l.Log(LogLevel.Info, "Got music data from server");
            }
        }

        public override void LookupData(Codec c) => this.SerializedMusicPlayer = c.Lookup(this.SerializedMusicPlayer);
    }
}
