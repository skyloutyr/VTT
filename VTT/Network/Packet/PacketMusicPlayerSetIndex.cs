namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketMusicPlayerSetIndex : PacketBase
    {
        public override uint PacketID => 71;

        public int Index { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            Logger l = this.ContextLogger;
            MusicPlayer mp;
            if (isServer)
            {
                mp = server.MusicPlayer;
                if (this.Sender.IsAdmin)
                {
                    if (this.Index >= mp.Tracks.Count)
                    {
                        l.Log(LogLevel.Warn, $"Client {this.Sender.ID} asked for a music player position change outside of the player's bounds, ignoring!");
                    }
                    else
                    {
                        if (this.Index < -1)
                        {
                            this.Index = -1;
                        }

                        mp.CurrentTrackPosition = this.Index;
                        mp.NeedsSave = true;
                        this.Broadcast();
                    }
                }
                else
                {
                    l.Log(LogLevel.Warn, $"Client {this.Sender.ID} asked for a music player position change without permissions!");
                }
            }
            else
            {
                mp = client.Frontend?.Sound?.MusicPlayer;
                if (mp != null)
                {
                    l.Log(LogLevel.Info, $"Server asked to set music player's position to {this.Index}");
                    mp.DoAction(x =>
                    {
                        x.CurrentTrackPosition = this.Index;
                        if (x.CurrentTrackPosition >= x.Tracks.Count || x.CurrentTrackPosition < -1)
                        {
                            x.CurrentTrackPosition = -1;
                            Client.Instance.Logger.Log(LogLevel.Warn, $"Server asked for a music player position change outside of the player's bounds, player will stop!");
                        }
                    });
                }
            }
        }

        public override void Decode(BinaryReader br) => this.Index = br.ReadInt32();
        public override void Encode(BinaryWriter bw) => bw.Write(this.Index);
    }
}
