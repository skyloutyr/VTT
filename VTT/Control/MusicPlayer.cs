namespace VTT.Control
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using VTT.Sound;
    using VTT.Util;

    public class MusicPlayer : ISerializable
    {
        public readonly object @lock = new object();

        public List<(Guid, float)> Tracks { get; } = new List<(Guid, float)>();
        public LoopMode RepeatState { get; set; } = LoopMode.None;
        public int CurrentTrackPosition { get; set; } = -1;
        public bool NeedsSave { get; set; }
        public float Volume { get; set; } = 1;

        public void ForcePlay(int trackIndex) => this.CurrentTrackPosition = trackIndex;

        public void DoGuardedActionNow(Action<MusicPlayer> act)
        {
            lock (this.@lock)
            {
                act(this);
            }
        }

        public void SwapTracks(int from, int to)
        {
            lock (this.@lock)
            {
                if (from >= 0 && from < this.Tracks.Count && to >= 0 && to < this.Tracks.Count)
                {
                    if (this.CurrentTrackPosition == from)
                    {
                        this.CurrentTrackPosition = to;
                    }
                    else
                    {
                        if (this.CurrentTrackPosition == to)
                        {
                            this.CurrentTrackPosition = from;
                        }
                    }

                    (Guid, float) i1 = this.Tracks[from];
                    (Guid, float) i2 = this.Tracks[to];
                    this.Tracks[to] = i1;
                    this.Tracks[from] = i2;
                }
            }
        }

        public void MoveTrack(int from, int to)
        {
            lock (this.@lock)
            {
                if (from >= 0 && from < this.Tracks.Count)
                {
                    (Guid, float) track = this.Tracks[from];
                    this.RemoveTrack(from);
                    this.AddTrack(track.Item1, track.Item2, to);
                }
            }
        }

        public IEnumerable<(Guid, float)> EnumerateItemsSafe()
        {
            lock (this.@lock)
            {
                foreach ((Guid, float) track in this.Tracks)
                {
                    yield return track;
                }
            }

            yield break;
        }

        public void AddTrack(Guid assetID, float volume, int trackIndex)
        {
            lock (this.@lock)
            {
                (Guid, float) dt = (assetID, volume);
                if (trackIndex >= this.Tracks.Count)
                {
                    this.Tracks.Add(dt);
                }
                else
                {
                    if (this.CurrentTrackPosition != -1)
                    {
                        if (this.CurrentTrackPosition >= trackIndex)
                        {
                            this.CurrentTrackPosition += 1;
                        }
                    }

                    this.Tracks.Insert(trackIndex, dt);
                }
            }

            this.NeedsSave = true;
        }

        public void RemoveTrack(int trackIndex)
        {
            lock (this.@lock)
            {
                if (trackIndex >= 0 && trackIndex < this.Tracks.Count)
                {
                    this.Tracks.RemoveAt(trackIndex);
                    if (this.CurrentTrackPosition != -1)
                    {
                        if (this.CurrentTrackPosition >= trackIndex)
                        {
                            this.CurrentTrackPosition -= 1;
                        }

                        if (this.CurrentTrackPosition < 0)
                        {
                            this.CurrentTrackPosition = 0;
                        }
                    }                    
                }
            }

            this.NeedsSave = true;
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            lock (this.@lock)
            {
                ret.SetArray("Tracks", this.Tracks.ToArray(), (n, c, v) =>
                {
                    DataElement m = new DataElement();
                    m.SetGuid("id", v.Item1);
                    m.SetSingle("v", v.Item2);
                    c.SetMap(n, m);
                });

                ret.SetEnum("LoopMode", this.RepeatState);
                ret.SetSingle("Volume", this.Volume);
            }

            return ret;
        }
        public void Deserialize(DataElement e)
        {
            lock (this.@lock)
            {
                this.Tracks.Clear();
                this.Tracks.AddRange(e.GetArray("Tracks", (n, c) =>
                {
                    DataElement m = c.GetMap(n, null);
                    return m != null ? (m.GetGuidLegacy("id"), m.GetSingle("v")) : ((Guid, float))(Guid.Empty, 0);

                }, Array.Empty<(Guid, float)>()));

                this.Tracks.RemoveAll(x => x.Item1.Equals(Guid.Empty));
                this.RepeatState = e.GetEnum<LoopMode>("LoopMode");
                this.Volume = e.GetSingle("Volume", 1f);
            }
        }

        private Guid _currentSoundID = Guid.Empty;
        private readonly ConcurrentQueue<Action<MusicPlayer>> _actions = new ConcurrentQueue<Action<MusicPlayer>>();

        public void DoAction(Action<MusicPlayer> a) => this._actions.Enqueue(a);

        public Guid CurrentSoundID => this._currentSoundID;

        public void ClientUpdate(SoundManager container)
        {
            lock (this.@lock)
            {
                if (this.CurrentTrackPosition >= this.Tracks.Count)
                {
                    this.CurrentTrackPosition = this.RepeatState == LoopMode.Loop ? 0 : -1;
                }

                while (!this._actions.IsEmpty)
                {
                    if (!this._actions.TryDequeue(out Action<MusicPlayer> a))
                    {
                        break;
                    }

                    a(this);
                }

                if (this.CurrentTrackPosition != -1 && this.CurrentTrackPosition < this.Tracks.Count)
                {
                    (Guid, float) soundData = this.Tracks[this.CurrentTrackPosition];
                    if (this._currentSoundID.Equals(Guid.Empty)) // Have a position, but no sound, play sound at position
                    {
                        this._currentSoundID = container.PlayAsset(soundData.Item1, soundData.Item2 * this.Volume, SoundCategory.Music, AssetSound.Type.Music);
                    }
                    else // We have a sound
                    {
                        if (container.TryGetAssetSound(this._currentSoundID, out AssetSound asound))
                        {
                            if (asound.AssetID.Equals(soundData.Item1))
                            {
                                asound.Volume = this.Tracks[this.CurrentTrackPosition].Item2 * this.Volume; // Adjust volume
                            }
                            else // Sound mismatch!
                            {
                                asound.Stopped = true;
                                this._currentSoundID = Guid.Empty;
                            }
                        }
                        else // Our sound ID points to a non-existing sound asset, maybe played to full or cleared?
                        {
                            this.CurrentTrackPosition = this.AdvanceIndex();
                            this._currentSoundID = Guid.Empty; // No further op, enqueue sound next tick
                        }
                    }
                }

                if (this.CurrentTrackPosition == -1 && !Guid.Empty.Equals(this._currentSoundID))
                {
                    if (container.TryGetAssetSound(this._currentSoundID, out AssetSound asound))
                    {
                        asound.Stopped = true;
                    }

                    this._currentSoundID = Guid.Empty;
                }
            }
        }

        public int AdvanceIndex()
        {
            switch (this.RepeatState)
            {
                case LoopMode.LoopSingle:
                {
                    return this.CurrentTrackPosition;
                }

                case LoopMode.Random:
                {
                    Random rng = new Random();
                    return rng.Next(this.Tracks.Count);
                }

                case LoopMode.Loop:
                {
                    int i = this.CurrentTrackPosition + 1;
                    if (i >= this.Tracks.Count)
                    {
                        i = this.RepeatState == LoopMode.None ? -1 : 0;
                    }

                    return i;
                }

                case LoopMode.None:
                default:
                {
                    return -1;
                }
            }
        }

        public enum LoopMode
        {
            None,
            Loop,
            LoopSingle,
            Random
        }
    }
}
