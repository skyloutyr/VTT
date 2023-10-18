namespace VTT.Sound
{
    using OpenTK.Audio.OpenAL;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using VTT.Network;

    public class SoundManager
    {
        public bool IsAvailable { get; set; }

        private ALDevice _device;
        private ALContext _ctx;

        public ALSoundContainer ChatMessage { get; set; }
        public ALSoundContainer YourTurn { get; set; }

        public List<(SoundCategory, int)> ActiveSources { get; } = new List<(SoundCategory, int)>();

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

            AL.Source(src, ALSourcef.Gain, volume);
        }

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
                    if (state == ALSourceState.Stopped)
                    {
                        this.ActiveSources.RemoveAt(i);
                        AL.DeleteSource(src.Item2);
                    }
                    else
                    {
                        if (this._volumeChangedNotification)
                        {
                            this.SetSourceVolume(src.Item1, src.Item2);
                        }
                    }
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
            }
        }
    }

    public enum SoundCategory
    {
        Unknown,
        UI
    }
}
