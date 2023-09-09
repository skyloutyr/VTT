namespace VTT.Asset
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class AssetBinaryCache
    {
        private readonly Dictionary<Guid, byte[]> _binaries = new Dictionary<Guid, byte[]>();
        private readonly Dictionary<Guid, long> _cacheAccessTimes = new Dictionary<Guid, long>();
        private long _cacheLength;

        public long MaxCacheLength { get; set; } = 1024 * 1024 * 1024;

        private bool _nocache = false;
        private readonly AssetManager _container;
        private readonly object _guiLock;

        public bool Enabled
        {
            get => !this._nocache;
            set => this._nocache = !value;
        }

        public long Occupancy => this._cacheLength;

        public AssetBinaryCache(AssetManager container)
        {
            this._container = container;
            this._guiLock = new object();
        }

        public IEnumerable<(Guid, long)> GetDebugOccupancyInfo()
        {
            lock (this._guiLock)
            {
                foreach (KeyValuePair<Guid, byte[]> d in this._binaries)
                {
                    yield return (d.Key, d.Value.LongLength);
                }
            }
        }

        public void DeleteCache(Guid id)
        {
            if (!this._nocache)
            {
                lock (this._guiLock)
                {
                    if (this._binaries.Remove(id, out byte[] dVal))
                    {
                        this._cacheLength -= dVal.LongLength;
                        this._cacheAccessTimes.Remove(id);
                    }
                }
            }
        }

        public byte[] GetBinary(Guid id)
        {
            if (this._nocache)
            {
                return this._container.Refs.TryGetValue(id, out AssetRef aref) ? File.ReadAllBytes(aref.ServerPointer.FileLocation) : null;
            }
            else
            {
                byte[] value;
                lock (this._guiLock)
                {
                    if (!this._binaries.TryGetValue(id, out value))
                    {
                        value = this.UpdateCache(id);
                    }
                }

                return value;
            }
        }

        private byte[] UpdateCache(Guid id)
        {
            if (this._container.Refs.TryGetValue(id, out AssetRef aref))
            {
                AssetBinaryPointer abp = aref.ServerPointer;
                byte[] binary = File.ReadAllBytes(abp.FileLocation);
                this._cacheLength += binary.LongLength;
                this._cacheAccessTimes[id] = DateTimeOffset.Now.Ticks;
                this._binaries[id] = binary;
                this.ShuffleCache();
                return binary;
            }

            return null;
        }

        private void ShuffleCache()
        {
            while (this._cacheLength > MaxCacheLength && this._cacheAccessTimes.Count > 0)
            {
                Guid leastScoringCacheElement = Guid.Empty;
                long smallestScore = long.MaxValue;
                long now = DateTimeOffset.Now.Ticks;
                foreach (Guid g in this._cacheAccessTimes.Keys)
                {
                    long ticksLastAccess = this._cacheAccessTimes[g];
                    long ticksDelta = now - ticksLastAccess;
                    long fs = this._binaries[g].LongLength;
                    long score = Math.Max(0, TimeSpan.TicksPerMinute - ticksDelta) + fs;
                    if (score < smallestScore)
                    {
                        smallestScore = score;
                        leastScoringCacheElement = g;
                    }
                }

                if (leastScoringCacheElement != Guid.Empty)
                {
                    long l = this._binaries[leastScoringCacheElement].LongLength;
                    this._binaries.Remove(leastScoringCacheElement);
                    this._cacheAccessTimes.Remove(leastScoringCacheElement);
                    this._cacheLength -= l;
                }
            }
        }
    }
}
