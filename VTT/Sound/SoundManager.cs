namespace VTT.Sound
{
    using OpenTK.Audio.OpenAL;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using VTT.Asset;
    using VTT.Control;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public class SoundManager
    {
        public bool IsAvailable { get; set; }

        private ALDevice _device;
        private ALContext _ctx;

        public ALSoundContainer ChatMessage { get; set; }
        public ALSoundContainer YourTurn { get; set; }
        public ALSoundContainer PingAny { get; set; }

        public List<(SoundCategory, int)> ActiveSources { get; } = new List<(SoundCategory, int)>();

        private readonly object _assetLock = new object();
        public List<AssetSound> AllPlayingAssets { get; } = new List<AssetSound>();
        public Dictionary<Guid, AssetSound> PlayingAssetsByID { get; } = new Dictionary<Guid, AssetSound>();
        public MusicPlayer MusicPlayer { get; } = new MusicPlayer();

        public void Init()
        {
            if (Client.Instance.Settings.DisableSounds)
            {
                Client.Instance.Logger.Log(LogLevel.Warn, $"Sound system disabled.");
                return;
            }

            try
            {
                List<string> devices = ALC.GetString(AlcGetStringList.DeviceSpecifier);
                string deviceName = ALC.GetString(ALDevice.Null, AlcGetString.DefaultDeviceSpecifier);
                foreach (string d in devices)
                {
                    if (d.Contains("OpenAL Soft")) // find AL soft
                    {
                        deviceName = d;
                    }
                }

                this._device = ALC.OpenDevice(deviceName);
                this._ctx = ALC.CreateContext(this._device, (int[])null);
                ALC.MakeContextCurrent(this._ctx);
                ALError ale = AL.GetError();
                if (ale != ALError.NoError)
                {
                    Client.Instance.Logger.Log(LogLevel.Error, $"OpenAL creation error - {AL.GetErrorString(ale)}");
                    IsAvailable = false;
                    try
                    {
                        ALC.MakeContextCurrent(ALContext.Null);
                        ALC.DestroyContext(this._ctx);
                        ALC.CloseDevice(this._device);
                        return;
                    }
                    catch
                    {
                        // NOOP
                    }
                }

                int major = ALC.GetInteger(this._device, AlcGetInteger.MajorVersion);
                int minor = ALC.GetInteger(this._device, AlcGetInteger.MinorVersion);
                Client.Instance.Logger.Log(LogLevel.Info, $"OpenAL {major}.{minor} initialized.");
                this.IsAvailable = true;
                this.LoadSounds();
            }
            catch (DllNotFoundException e)
            {
                Client.Instance.Logger.Log(LogLevel.Error, "OpenAL subsystem not available!");
                Client.Instance.Logger.Exception(LogLevel.Error, e);
                IsAvailable = false;
                return;
            }
        }

        public void LoadSounds()
        {
            this.ChatMessage = this.LoadEmbedSound("simple-interface-tone");
            this.YourTurn = this.LoadEmbedSound("pop-ding-notification-effect-2");
            this.PingAny = this.LoadEmbedSound("game-radar-ping-3");
        }

        private ALSoundContainer LoadEmbedSound(string name)
        {
            string fname = "VTT.Embed." + name + ".wav";
            Stream s = Program.Code.GetManifestResourceStream(fname);
            if (s != null)
            {
                try
                {
                    WaveAudio wa = new WaveAudio();
                    wa.Load(s);
                    return new ALSoundContainer(wa);
                }
                catch
                {
                    Client.Instance.Logger.Log(LogLevel.Error, $"Error loading embedded sound effect {name}!");
                }
            }
            else
            {
                Client.Instance.Logger.Log(LogLevel.Error, $"Tried to load a non-existing embedded sound effect {name}!");
            }

            return null;
        }

        private readonly ConcurrentQueue<(SoundCategory, ALSoundContainer)> _queuedSounds = new ConcurrentQueue<(SoundCategory, ALSoundContainer)>();
        private readonly ConcurrentQueue<Guid> _assetsToStop = new ConcurrentQueue<Guid>();

        public void PlaySound(ALSoundContainer sc, SoundCategory cat)
        {
            if (Client.Instance.Settings.DisableSounds)
            {
                return;
            }

            if (this.IsAvailable && sc != null)
            {
                this._queuedSounds.Enqueue((cat, sc));
            }
        }

        private bool _assetsClear;
        public void ClearAssets() => this._assetsClear = true;

        public Guid PlayAsset(Guid assetID, float volume = 1.0f, SoundCategory cat = SoundCategory.Asset, AssetSound.Type aType = AssetSound.Type.Asset)
        {
            Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(assetID, AssetType.Sound, out _); // Request here
            Guid sId = Guid.Empty;
            lock (this._assetLock)
            {
                AssetSound asound = new AssetSound(this, assetID, cat, aType) { Volume = volume };
                this.AllPlayingAssets.Add(asound);
                this.PlayingAssetsByID.Add(asound.ID, asound);
                sId = asound.ID;
            }

            return sId;
        }

        public bool TryGetAssetSound(Guid soundId, out AssetSound asound)
        {
            lock (this._assetLock)
            {
                return this.PlayingAssetsByID.TryGetValue(soundId, out asound);
            }
        }

        public void StopAsset(Guid assetID) => this._assetsToStop.Enqueue(assetID);

        private bool _volumeChangedNotification;
        public void NotifyOfVolumeChanges() => this._volumeChangedNotification = true;

        public float GetCategoryVolume(SoundCategory cat)
        {
            float volume = Client.Instance.Settings.SoundMasterVolume;
            if (cat == SoundCategory.UI)
            {
                volume *= Client.Instance.Settings.SoundUIVolume;
            }

            if (cat == SoundCategory.MapFX)
            {
                volume *= Client.Instance.Settings.SoundMapFXVolume;
            }

            if (cat == SoundCategory.Asset)
            {
                volume *= Client.Instance.Settings.SoundAssetVolume;
            }

            if (cat == SoundCategory.Ambiance)
            {
                volume *= Client.Instance.Settings.SoundAmbianceVolume;
            }

            if (cat == SoundCategory.Music)
            {
                volume *= Client.Instance.Settings.SoundMusicVolume;
            }

            return volume;
        }

        public void SetSourceVolume(SoundCategory cat, int src)
        {
            if (Client.Instance.Settings.DisableSounds)
            {
                return;
            }

            AL.Source(src, ALSourcef.Gain, this.AdjustVolumeForNonLinearity(this.GetCategoryVolume(cat)));
        }

        public float AdjustVolumeForNonLinearity(float vIn) => vIn < 0.0001f ? 0 : MathF.Pow(100, vIn - 1.0f);

        public void Update()
        {
            if (Client.Instance.Settings.DisableSounds)
            {
                return;
            }

            if (this.IsAvailable)
            {
                Map m = Client.Instance.CurrentMap;
                ALC.MakeContextCurrent(this._ctx);

                while (!this._assetsToStop.IsEmpty)
                {
                    if (!this._assetsToStop.TryDequeue(out Guid aSID))
                    {
                        break;
                    }

                    lock (this._assetLock)
                    {
                        for (int i = this.AllPlayingAssets.Count - 1; i >= 0; i--)
                        {
                            AssetSound asound = this.AllPlayingAssets[i];
                            if (asound.AssetID.Equals(aSID))
                            {
                                asound.Free();
                                this.AllPlayingAssets.RemoveAt(i);
                                this.PlayingAssetsByID.Remove(asound.ID);
                            }
                        }
                    }
                }

                for (int i = this.ActiveSources.Count - 1; i >= 0; i--)
                {
                    (SoundCategory, int) src = this.ActiveSources[i];
                    ALSourceState state = AL.GetSourceState(src.Item2);
                    if (this._assetsClear)
                    {
                        AL.SourceStop(src.Item2);
                    }

                    if (state == ALSourceState.Stopped || this._assetsClear)
                    {
                        AL.DeleteSource(src.Item2);
                        this.ActiveSources.RemoveAt(i);
                    }
                    else
                    {
                        if (this._volumeChangedNotification)
                        {
                            this.SetSourceVolume(src.Item1, src.Item2);
                        }
                    }
                }

                lock (this._assetLock)
                {
                    AssetSound ambientSound = null;
                    for (int i = this.AllPlayingAssets.Count - 1; i >= 0; i--)
                    {
                        AssetSound asound = this.AllPlayingAssets[i];
                        asound.Update();
                        if (asound.Stopped)
                        {
                            asound.Free();
                            this.AllPlayingAssets.RemoveAt(i);
                            this.PlayingAssetsByID.Remove(asound.ID);
                        }
                        else
                        {
                            if (this._volumeChangedNotification && asound.Started)
                            {
                                asound.VolumeChangedNotification();
                            }

                            if (asound.SoundType == AssetSound.Type.Ambient)
                            {
                                if (ambientSound != null)
                                {
                                    asound.Free();
                                    this.AllPlayingAssets.RemoveAt(i);
                                    this.PlayingAssetsByID.Remove(asound.ID);
                                }
                                else
                                {
                                    ambientSound = asound;
                                }
                            }
                        }
                    }

                    Guid asID = m?.AmbientSoundID ?? Guid.Empty;
                    if (asID.Equals(Guid.Empty)) // Map doesn't have ambient sound or no map present
                    {
                        if (ambientSound != null)
                        {
                            ambientSound.Free();
                            this.AllPlayingAssets.Remove(ambientSound);
                            this.PlayingAssetsByID.Remove(ambientSound.ID);
                        }
                    }
                    else // Map has ambient sound
                    {
                        if (ambientSound != null) // We have ambient sound
                        {
                            if (!ambientSound.AssetID.Equals(asID)) // Ambient sound ID doesn't match
                            {
                                ambientSound.Free();
                                this.AllPlayingAssets.Remove(ambientSound);
                                this.PlayingAssetsByID.Remove(ambientSound.ID);
                            } // No handler for if the sound matches - is removed already if fully played, NOOP if still playing anyways
                        }
                        else // We don't have ambient sound
                        {
                            // Request a new ambient sound of type ambient
                            AssetSound asound = new AssetSound(this, asID, SoundCategory.Ambiance, AssetSound.Type.Ambient);
                            this.AllPlayingAssets.Add(asound);
                            this.PlayingAssetsByID.Add(asound.ID, asound);
                        }
                    }
                }

                if (this._assetsClear)
                {
                    this._queuedSounds.Clear();
                    lock (this._assetLock)
                    {
                        foreach (AssetSound asound in this.AllPlayingAssets)
                        {
                            asound.Free();
                        }

                        this.AllPlayingAssets.Clear();
                        this.PlayingAssetsByID.Clear();
                        foreach (KeyValuePair<Guid, ALSoundContainer> kv in this._assetContainers)
                        {
                            kv.Value.Free();
                        }

                        this._assetContainers.Clear();
                    }

                    this._assetsClear = false;
                }

                this._volumeChangedNotification = false;
                while (!this._queuedSounds.IsEmpty)
                {
                    if (!this._queuedSounds.TryDequeue(out (SoundCategory, ALSoundContainer) sc))
                    {
                        break;
                    }

                    int src = sc.Item2.Instantiate();
                    if (src != -1)
                    {
                        this.ActiveSources.Add((sc.Item1, src));
                        this.SetSourceVolume(sc.Item1, src);
                        AL.SourcePlay(src);
                    }
                }

                if (Client.Instance.NetClient?.IsConnected ?? false)
                {
                    this.MusicPlayer.ClientUpdate(this);
                }
            }
        }

        private readonly Dictionary<(Guid, Guid, int), ushort[]> _soundBuffersResponses = new Dictionary<(Guid, Guid, int), ushort[]>();

        public bool RequestBufferedSound(Guid soundID, Guid assetID, int cIndex, out ushort[] data)
        {
            (Guid, Guid, int) key = (soundID, assetID, cIndex);
            bool haveRequest = this._soundBuffersResponses.TryGetValue(key, out ushort[] val);
            if (haveRequest && val == null)
            {
                data = null;
                return false; // Wait
            }

            if (haveRequest && val != null)
            {
                data = val;
                this._soundBuffersResponses.Remove(key);
                return true;
            }

            this._soundBuffersResponses.Add(key, null);
            Client.Instance.Frontend.EnqueueOrExecuteTask(() => new PacketSoundBuffer() { SoundID = soundID, AssetID = assetID, ChunkIndex = cIndex }.Send());
            data = null;
            return false;
        }

        private ushort[] ConvertBytesToSound(byte[] data)
        {
            ushort[] ret = new ushort[data.Length / 2];
            int index = 0;
            for (int i = 0; i < data.Length; i += 2)
            {
                ret[index++] = BitConverter.ToUInt16(data, i);
            }

            return ret;
        }

        public void BufferedSourceReadyCallback(SoundCategory cat, int src) => this.SetSourceVolume(cat, src);

        public void ReceiveSoundBuffer(Guid soundID, Guid assetID, int cIndex, byte[] buffer)
        {
            Client.Instance.Logger.Log(LogLevel.Debug, $"Got sound data for sound {soundID}, chunk index of {cIndex}.");
            bool soundExistanceStatus;
            AssetSound asound;
            lock (this._assetLock)
            {
                soundExistanceStatus = this.PlayingAssetsByID.TryGetValue(soundID, out asound);
            }

            if (!soundExistanceStatus)
            {
                Client.Instance.Logger.Log(LogLevel.Warn, $"Got sound buffer info for sound {soundID}, asset {assetID} and chunk {cIndex}, but no such sound is queued. This may be due to a sound purge. All data will be discarded.");
                return;
            }

            if (Client.Instance.AssetManager.Refs.TryGetValue(assetID, out AssetRef aRef))
            {
                if (aRef?.Meta?.SoundInfo?.SoundType == SoundData.Metadata.StorageType.Mpeg)
                {
                    if (asound.MpegDecoder == null)
                    {
                        Client.Instance.Logger.Log(LogLevel.Error, $"Sound data for sound {soundID} appears to be an Mpeg frame, but the sound itself didn't specify itself as an mpeg sound. This should be impossible. Sound and data will be discarded!");
                        asound.Stopped = true;
                        return;
                    }

                    bool lastBuffer = cIndex + 1 >= aRef.Meta.SoundInfo.TotalChunks;
                    asound.MpegDecoder.AddData(buffer);
                    byte[] bytes = asound.MpegDecoder.ReadSamples(!lastBuffer, out bool readToEnd);
                    lock (this._assetLock)
                    {
                        this._soundBuffersResponses[(soundID, assetID, cIndex)] = this.ConvertBytesToSound(bytes);
                    }

                    if (readToEnd && !lastBuffer)
                    {
                        Client.Instance.Logger.Log(LogLevel.Warn, $"Sound data read to end for sound {soundID}, asset {assetID} at chunk {cIndex}, but more chunks were still expected!");
                    }

                    if (lastBuffer)
                    {
                        Client.Instance.Logger.Log(LogLevel.Debug, $"Mpeg data was read to end (steaming context says {readToEnd}).");
                    }
                }
                else
                {
                    lock (this._assetLock)
                    {
                        this._soundBuffersResponses[(soundID, assetID, cIndex)] = this.ConvertBytesToSound(buffer);
                    }
                }
            }
            else
            {
                lock (this._assetLock)
                {
                    this._soundBuffersResponses[(soundID, assetID, cIndex)] = this.ConvertBytesToSound(buffer);
                }

                Client.Instance.Logger.Log(LogLevel.Warn, $"Got sound data for non-existing asset {assetID}!");
            }
        }

        private readonly Dictionary<Guid, ALSoundContainer> _assetContainers = new Dictionary<Guid, ALSoundContainer>();
        public bool TrtGetRawSoundContainer(Guid assetID, out ALSoundContainer soundContainer) => this._assetContainers.TryGetValue(assetID, out soundContainer);
        public void AddFullSoundData(Guid assetID, Asset a) => this._assetContainers.Add(assetID, new ALSoundContainer(a.Sound.RawAudio));
    }

    public enum SoundCategory
    {
        Unknown,
        UI,
        MapFX,
        Asset,
        Ambiance,
        Music
    }
}
