namespace VTT.Network.Packet
{
    using System;
    using System.IO;
    using VTT.Control;
    using VTT.Util;

    public class PacketMusicPlayerAction : PacketBase
    {
        public override uint PacketID => 72;
        public int IndexMain { get; set; }
        public int IndexMoveTo { get; set; }
        public (Guid, float) Data { get; set; }
        public MusicPlayer.LoopMode LoopMode { get; set; }
        public Type ActionType { get; set; }

        public override void Act(Guid sessionID, Server server, Client client, bool isServer)
        {
            MusicPlayer mp = isServer ? server.MusicPlayer : client.Frontend?.Sound?.MusicPlayer;
            bool hasPermission = !isServer || this.Sender.IsAdmin;
            bool anyActions = false;
            Logger l = this.GetContextLogger();
            if (mp != null)
            {
                switch (this.ActionType)
                {
                    case Type.Add:
                    {
                        if (hasPermission)
                        {
                            mp.AddTrack(this.Data.Item1, this.Data.Item2, this.IndexMain);
                            mp.NeedsSave = true;
                            anyActions = true;
                        }
                        else
                        {
                            l.Log(LogLevel.Warn, $"Client {this.Sender.ID} asked to add a track to the player without permissions!");
                        }

                        break;
                    }

                    case Type.Remove:
                    {
                        if (hasPermission)
                        {
                            mp.RemoveTrack(this.IndexMain);
                            mp.NeedsSave = true;
                            anyActions = true;
                        }
                        else
                        {
                            l.Log(LogLevel.Warn, $"Client {this.Sender.ID} asked to remove a track from the player without permissions!");
                        }

                        break;
                    }

                    case Type.ChangeVolume:
                    {
                        if (hasPermission)
                        {
                            mp.DoGuardedActionNow(x =>
                            {
                                if (this.IndexMain >= 0 && this.IndexMain < x.Tracks.Count)
                                {
                                    (Guid, float) d = x.Tracks[this.IndexMain];
                                    x.Tracks[this.IndexMain] = (d.Item1, this.Data.Item2);
                                }
                                else
                                {
                                    l.Log(LogLevel.Warn, $"Music player accessed out of range for volume change!");
                                }
                            });
                        }
                        else
                        {
                            l.Log(LogLevel.Warn, $"Client {this.Sender.ID} asked to remove a track from the player without permissions!");
                        }

                        break;
                    }

                    case Type.Move:
                    {
                        if (hasPermission)
                        {
                            mp.MoveTrack(this.IndexMain, this.IndexMoveTo);
                            mp.NeedsSave = true;
                            anyActions = true;
                        }
                        else
                        {
                            l.Log(LogLevel.Warn, $"Client {this.Sender.ID} asked to remove a track from the player without permissions!");
                        }

                        break;
                    }

                    case Type.SetMode:
                    {
                        if (hasPermission)
                        {
                            mp.RepeatState = this.LoopMode;
                            mp.NeedsSave = true;
                            anyActions = true;
                        }
                        else
                        {
                            l.Log(LogLevel.Warn, $"Client {this.Sender.ID} asked to change music player loop mode without permissions!");
                        }

                        break;
                    }

                    case Type.ForceNext:
                    {
                        if (hasPermission)
                        {
                            mp.NeedsSave = true;
                            mp.DoGuardedActionNow(x => x.CurrentTrackPosition = x.AdvanceIndex());
                            anyActions = true;
                        }
                        else
                        {
                            l.Log(LogLevel.Warn, $"Client {this.Sender.ID} asked to move the music player to the next track without permissions!");
                        }

                        break;
                    }

                    case Type.PlayerVolumeChange:
                    {
                        if (hasPermission)
                        {
                            mp.NeedsSave = true;
                            mp.Volume = this.Data.Item2;
                            anyActions = true;
                        }
                        else
                        {
                            l.Log(LogLevel.Warn, $"Client {this.Sender.ID} asked to change the player's global volume without permissions!");
                        }

                        break;
                    }
                }

                if (anyActions && isServer)
                {
                    this.Broadcast();
                }
            }
        }

        public override void Decode(BinaryReader br)
        {
            this.ActionType = br.ReadEnumSmall<Type>();
            this.IndexMain = br.ReadInt32();
            if (this.ActionType is Type.Add or Type.ChangeVolume or Type.PlayerVolumeChange)
            {
                this.Data = (br.ReadGuid(), br.ReadSingle());
            }

            if (this.ActionType == Type.Move)
            {
                this.IndexMoveTo = br.ReadInt32();
            }

            if (this.ActionType == Type.SetMode)
            {
                this.LoopMode = br.ReadEnumSmall<MusicPlayer.LoopMode>();
            }
        }
        public override void Encode(BinaryWriter bw)
        {
            bw.WriteEnumSmall(this.ActionType);
            bw.Write(this.IndexMain);
            if (this.ActionType is Type.Add or Type.ChangeVolume or Type.PlayerVolumeChange)
            {
                bw.Write(this.Data.Item1);
                bw.Write(this.Data.Item2);
            }

            if (this.ActionType == Type.Move)
            {
                bw.Write(this.IndexMoveTo);
            }

            if (this.ActionType == Type.SetMode)
            {
                bw.WriteEnumSmall(this.LoopMode);
            }
        }

        public enum Type
        {
            Add,
            Remove,
            Move,
            ChangeVolume,
            SetMode,
            ForceNext,
            PlayerVolumeChange
        }
    }
}
