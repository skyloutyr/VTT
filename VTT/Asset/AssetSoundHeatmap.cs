namespace VTT.Asset
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using VTT.Network;

    public class AssetSoundHeatmap
    {
        public AssetManager Container { get; init; }
        public Dictionary<Guid, Accessor> DataMap { get; } = new Dictionary<Guid, Accessor>();

        private readonly object _locker = new object();

        public AssetSoundHeatmap(AssetManager assetManager) => this.Container = assetManager;

        public AssetStatus Get(Guid assetID, int index, out Spot spot)
        {
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            Accessor a;
            lock (this._locker)
            {
                if (!this.DataMap.TryGetValue(assetID, out a))
                {
                    if (this.Container.Refs.TryGetValue(assetID, out AssetRef aRef))
                    {
                        if (aRef.Type == AssetType.Sound && aRef.Meta != null && !aRef.Meta.SoundInfo.IsFullData)
                        {
                            string fl = aRef.ServerPointer.FileLocation;
                            a = new Accessor(fl, aRef.Meta.SoundInfo.TotalChunks, aRef.Meta.SoundInfo.SampleRate, aRef.Meta.SoundInfo.NumChannels);
                            this.DataMap[assetID] = a;
                        }
                        else
                        {
                            spot = null;
                            return AssetStatus.Error;
                        }
                    }
                    else
                    {
                        spot = null;
                        return AssetStatus.NoAsset;
                    }
                }
            }

            spot = a.Get(index, now);
            return spot != null ? AssetStatus.Return : AssetStatus.Error;
        }

        public void Pulse()
        {
            lock (this._locker)
            {
                foreach (KeyValuePair<Guid, Accessor> a in this.DataMap)
                {
                    a.Value.Pulse();
                }
            }
        }

        public class Accessor
        {
            private Stream _fsStream;
            private readonly string _fsPath;
            private readonly object _locker = new object();
            private readonly int _chunkLength;

            public Spot[] Spots { get; init; }

            public const long ExpirationTime = 60000; // 1 min spot lifetime

            public Accessor(string fsPath, int spots, int freq, int nCh)
            {
                this._fsStream = File.OpenRead(fsPath);
                this._fsPath = fsPath;
                this.Spots = new Spot[spots];
                this._chunkLength = freq * nCh * 5 * sizeof(ushort); // 5s of stereo16 wave sound
            }

            public void Pulse()
            {
                long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                bool haveAny = false;
                for (int i = this.Spots.Length - 1; i >= 0; i--)
                {
                    Spot s = this.Spots[i];
                    if (s != null)
                    {
                        if (now - s.LastRequestTime > ExpirationTime)
                        {
                            lock (this._locker)
                            {
                                this.Spots[i] = null;
                            }
                        }
                        else
                        {
                            haveAny = true;
                        }
                    }
                }

                if (!haveAny && this._fsStream != null)
                {
                    lock (this._locker)
                    {
                        try
                        {
                            this._fsStream.Close();
                            this._fsStream.Dispose();
                        }
                        catch
                        {
                            // NOOP - just release unused resources
                        }
                        finally
                        {
                            this._fsStream = null;
                        }
                    }
                }
            }

            public Spot Get(int index, in long at)
            {
                if (index < 0 || index >= this.Spots.Length)
                {
                    return null;
                }

                Spot s = this.Spots[index];
                lock (this._locker)
                {
                    try
                    {
                        if (s == null)
                        {
                            s = new Spot();
                            s.DataIndex = index;
                            Stream stream = this.GetFS();
                            stream.Position = index * this._chunkLength;
                            s.Data = new byte[this._chunkLength];
                            stream.Read(s.Data, 0, s.Data.Length);
                            this.Spots[index] = s;
                        }
                    }
                    catch (Exception e)
                    {
                        Server.Instance.Logger.Log(Util.LogLevel.Error, "Sound reading error - could not read sound chunk " + index);
                        Server.Instance.Logger.Exception(Util.LogLevel.Error, e);
                    }
                }

                s.LastRequestTime = at;
                return s;
            }

            private Stream GetFS()
            {
                if (this._fsStream == null)
                {
                    this._fsStream = File.OpenRead(this._fsPath);
                }

                try
                {
                    return this._fsStream.CanRead ? this._fsStream : throw new IOException();
                }
                catch
                {
                    try
                    {
                        this._fsStream.Close();
                        this._fsStream.Dispose();
                    }
                    finally
                    {
                        this._fsStream = File.OpenRead(this._fsPath);
                    }
                }

                return this._fsStream;
            }
        }

        public class Spot
        {
            public byte[] Data { get; set; }
            public int DataIndex { get; set; }
            public long LastRequestTime { get; set; }
        }
    }
}
