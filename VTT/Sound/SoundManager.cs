namespace VTT.Sound
{
    using OpenTK.Audio.OpenAL;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using VTT.Asset;
    using VTT.Network;
    using VTT.Network.Packet;

    public class SoundManager
    {
        public bool IsAvailable { get; set; }

        private ALDevice _device;
        private ALContext _ctx;

        public ALSoundContainer ChatMessage { get; set; }
        public ALSoundContainer YourTurn { get; set; }
        public ALSoundContainer PingAny { get; set; }

        public List<(SoundCategory, int)> ActiveSources { get; } = new List<(SoundCategory, int)>();
        private readonly Dictionary<int, BufferedSound> _bufferedSounds = new Dictionary<int, BufferedSound>();

        private readonly Dictionary<Guid, ALSoundContainer> _assetContainers = new Dictionary<Guid, ALSoundContainer>();

        public void Init()
        {
            if (Client.Instance.Settings.DisableSounds)
            {
                Client.Instance.Logger.Log(Util.LogLevel.Warn, $"Sound system disabled.");
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
                    Client.Instance.Logger.Log(Util.LogLevel.Error, $"OpenAL creation error - {AL.GetErrorString(ale)}");
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
                Client.Instance.Logger.Log(Util.LogLevel.Info, $"OpenAL {major}.{minor} initialized.");
                this.IsAvailable = true;
                this.LoadSounds();
            }
            catch (DllNotFoundException e)
            {
                Client.Instance.Logger.Log(Util.LogLevel.Error, "OpenAL subsystem not available!");
                Client.Instance.Logger.Exception(Util.LogLevel.Error, e);
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
                    Client.Instance.Logger.Log(Util.LogLevel.Error, $"Error loading embedded sound effect {name}!");
                }
            }
            else
            {
                Client.Instance.Logger.Log(Util.LogLevel.Error, $"Tried to load a non-existing embedded sound effect {name}!");
            }

            return null;
        }

        private readonly ConcurrentQueue<(SoundCategory, ALSoundContainer)> _queuedSounds = new ConcurrentQueue<(SoundCategory, ALSoundContainer)>();
        private readonly ConcurrentQueue<Guid> _waitingOnAssetRequests = new ConcurrentQueue<Guid>();
        
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
        public void ClearAssets()
        {
            this._assetsClear = true;
        }

        public void PlayAsset(Guid assetID)
        {
            Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(assetID, AssetType.Sound, out _); // Request here
            this._waitingOnAssetRequests.Enqueue(assetID);
        }

        private bool _volumeChangedNotification;
        public void NotifyOfVolumeChanges() => this._volumeChangedNotification = true;

        private void SetSourceVolume(SoundCategory cat, int src)
        {
            if (Client.Instance.Settings.DisableSounds)
            {
                return;
            }

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

            AL.Source(src, ALSourcef.Gain, volume);
        }

        private readonly List<Guid> _rewaitAssetList = new List<Guid>();
        public void Update()
        {
            if (Client.Instance.Settings.DisableSounds)
            {
                return;
            }

            if (this.IsAvailable)
            {
                ALC.MakeContextCurrent(this._ctx);
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
                        if (this._bufferedSounds.TryGetValue(src.Item2, out BufferedSound bs))
                        {
                            if (bs.Initialized) // May have a sound that isn't initialized yet and is reporting stopped
                            {
                                bs.Free();
                                this._bufferedSounds.Remove(src.Item2);
                                this.ActiveSources.RemoveAt(i);
                            }
                        }
                        else
                        {
                            AL.DeleteSource(src.Item2);
                            this.ActiveSources.RemoveAt(i);
                        }
                    }
                    else
                    {
                        if (this._volumeChangedNotification)
                        {
                            this.SetSourceVolume(src.Item1, src.Item2);
                        }
                    }
                }

                if (this._assetsClear)
                {
                    this._queuedSounds.Clear();
                    this._rewaitAssetList.Clear();
                    foreach (KeyValuePair<Guid, ALSoundContainer> item in this._assetContainers)
                    {
                        item.Value.Free();
                    }

                    this._assetContainers.Clear();
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

                this._rewaitAssetList.Clear();
                while (!this._waitingOnAssetRequests.IsEmpty)
                {
                    if (!this._waitingOnAssetRequests.TryDequeue(out Guid aID))
                    {
                        break;
                    }

                    if (this._assetContainers.TryGetValue(aID, out ALSoundContainer asc))
                    {
                        this.PlaySound(asc, SoundCategory.Asset);
                    }

                    AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(aID, AssetType.Sound, out Asset a);
                    if (status == AssetStatus.Await)
                    {
                        this._rewaitAssetList.Add(aID);
                        continue;
                    }

                    if (status is AssetStatus.Error or AssetStatus.NoAsset)
                    {
                        Client.Instance.Logger.Log(Util.LogLevel.Error, "Sound system did not receive requested sound " + aID);
                        continue;
                    }

                    if (a == null)
                    {
                        this._rewaitAssetList.Add(aID);
                        continue;
                    }

                    if (status == AssetStatus.Return)
                    {
                        if (a.Type != AssetType.Sound)
                        {
                            Client.Instance.Logger.Log(Util.LogLevel.Error, "Sound system received a non-sound for id " + aID);
                            continue;
                        }

                        if (a.Sound == null)
                        {
                            this._rewaitAssetList.Add(aID);
                            continue;
                        }

                        if (a.Sound.Meta.IsFullData)
                        {
                            this._assetContainers.Add(aID, new ALSoundContainer(a.Sound.RawAudio));
                            this._rewaitAssetList.Add(aID); // allow sound to play on next update
                            continue;
                        }
                        else
                        {
                            BufferedSound bs = new BufferedSound(a.Sound.Meta.SampleRate, a.Sound.Meta.TotalChunks, aID);
                            this._bufferedSounds[bs.Source] = bs;
                            this.ActiveSources.Add((SoundCategory.Asset, bs.Source));
                        }
                    }
                }

                foreach (Guid id in this._rewaitAssetList)
                {
                    this._waitingOnAssetRequests.Enqueue(id);
                }
            }
        }

        private Dictionary<(Guid, int), byte[]> _soundBuffersResponses = new Dictionary<(Guid, int), byte[]>();
        private readonly object _soundBuffersResponsesLock = new object();

        public bool RequestBufferedSound(Guid assetID, int cIndex, out ushort[] data)
        {
            lock (this._soundBuffersResponsesLock)
            {
                (Guid, int) key = (assetID, cIndex);
                bool haveRequest = this._soundBuffersResponses.TryGetValue(key, out byte[] val);
                if (haveRequest && val == null)
                {
                    data = null;
                    return false; // Wait
                }

                if (haveRequest && val != null)
                {
                    data = this.ConvertBytesToSound(val);
                    this._soundBuffersResponses.Remove(key);
                    return true;
                }

                this._soundBuffersResponses.Add(key, null);
                Client.Instance.Frontend.EnqueueOrExecuteTask(() => new PacketSoundBuffer() { AssetID = assetID, ChunkIndex = cIndex }.Send());
                data = null;
                return false;
            }
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

        public void BufferedSourceReadyCallback(int src)
        {
            this.SetSourceVolume(SoundCategory.Asset, src);
        }

        public void ReceiveSoundBuffer(Guid soundID, int cIndex, byte[] buffer)
        {
            lock (this._soundBuffersResponsesLock)
            {
                this._soundBuffersResponses[(soundID, cIndex)] = buffer;
            }
        }
    }

    public enum SoundCategory
    {
        Unknown,
        UI,
        MapFX,
        Asset
    }
}
