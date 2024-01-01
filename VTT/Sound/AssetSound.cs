namespace VTT.Sound
{
    using OpenTK.Audio.OpenAL;
    using System;
    using VTT.Asset;
    using VTT.Control;
    using VTT.Network;

    public class AssetSound
    {
        private int _srcId = -1;
        private float _volumeBase = 1;
        private bool _volumeChanged = false;

        public Guid ID { get; set; }
        public Guid AssetID { get; set; }
        public bool Started { get; private set; }
        public bool Stopped { get; set; }
        public SoundCategory Category { get; init; }
        public SoundManager Container { get; init; }
        public Type SoundType { get; init; }
        public float Volume
        {
            get => this._volumeBase;
            set
            {
                this._volumeBase = value;
                this._volumeChanged = true;
            }
        }

        public SoundBuffer Buffer { get; set; }
        public StreamingMpeg MpegDecoder { get; set; }

        private int _returnedDataErroredCounter = 0;

        public int SourceID => this._srcId;

        public AssetSound(SoundManager container, Guid assetID, SoundCategory cat, Type type)
        {
            this.ID = Guid.NewGuid();
            this.AssetID = assetID;
            this.Category = cat;
            this.Container = container;
            this.SoundType = type;
            Client.Instance.Logger.Log(Util.LogLevel.Debug, $"New asset sound enqueued to category {cat} by sound ID {this.ID} for asset {assetID}.");
        }

        public void VolumeChangedNotification()
        {
            float vCat = this.Container.GetCategoryVolume(this.Category);
            if (this.SoundType == Type.Ambient)
            {
                vCat *= Client.Instance.CurrentMap?.AmbientSoundVolume ?? 1.0f;
            }

            AL.Source(this._srcId, ALSourcef.Gain, vCat * this._volumeBase);
        }

        public void Update()
        {
            if (this.Stopped)
            {
                return;
            }

            if (!this.Started)
            {
                if (this.Container.TrtGetRawSoundContainer(this.AssetID, out ALSoundContainer soundContainer))
                {
                    this._srcId = soundContainer.Instantiate();
                    this.Container.SetSourceVolume(this.Category, this._srcId);
                    AL.SourcePlay(this._srcId);
                    this.Started = true;
                    Client.Instance.Logger.Log(Util.LogLevel.Debug, $"Sound {this.ID} is RAW, playing.");
                }
                else
                {
                    AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(this.AssetID, AssetType.Sound, out Asset a);
                    if (status == AssetStatus.Return)
                    {
                        if (a?.Type == AssetType.Sound && a?.Sound != null && a?.Sound?.Meta != null)
                        {
                            SoundData sd = a.Sound;
                            if (sd.Meta.IsFullData) // Have raw audio
                            {
                                this.Container.AddFullSoundData(this.AssetID, a);
                                Client.Instance.Logger.Log(Util.LogLevel.Debug, $"Sound {this.ID} was specified as full data, requeueing after receiving asset.");
                                return; // Will play on next tick unless sound manager had issues/cleared the sound list
                            }
                            else
                            {
                                this.Buffer = new SoundBuffer(sd.Meta.SampleRate, sd.Meta.NumChannels, sd.Meta.TotalChunks, this.AssetID, out this._srcId);
                                // Mpeg container
                                if (sd.Meta.SoundType == SoundData.Metadata.StorageType.Mpeg)
                                {
                                    this.MpegDecoder = new StreamingMpeg();
                                    Client.Instance.Logger.Log(Util.LogLevel.Debug, $"Sound {this.ID} was specified as mpeg compressed, buffer and decoder created, sound queued.");
                                }
                                else
                                {
                                    Client.Instance.Logger.Log(Util.LogLevel.Debug, $"Sound {this.ID} was specified as non-mpeg streaming sound, buffer created and queued.");
                                }

                                this.Started = true;
                            }
                        }
                        else
                        {
                            if (++this._returnedDataErroredCounter >= 1800)
                            {
                                Client.Instance.Logger.Log(Util.LogLevel.Error, $"Sound issue at sound {this.ID}: got asset ({this.AssetID}), but metadata or sound data never got populated after 30 seconds!");
                                this.Stopped = true;
                            }
                        }
                    }
                    else
                    {
                        if (status is AssetStatus.Error or AssetStatus.NoAsset)
                        {
                            Client.Instance.Logger.Log(Util.LogLevel.Error, $"Sound issue at sound {this.ID}: got no asset for request {this.AssetID} (ec:{status})!");
                            this.Stopped = true;
                        }
                    }
                }
            }

            if (AL.IsSource(this._srcId))
            {
                if (this._volumeChanged)
                {
                    this.VolumeChangedNotification();
                    this._volumeChanged = false;
                }

                this.Buffer?.Update(this);
                if (AL.GetSourceState(this._srcId) == ALSourceState.Stopped)
                {
                    this.Stopped = true;
                    AL.DeleteSource(this._srcId);
                    this._srcId = -1;
                }
            }
        }

        public void Free()
        {
            Client.Instance.Logger.Log(Util.LogLevel.Debug, $"Sound {this.ID} stopped and freed.");
            if (this._srcId != -1)
            {
                this.Stopped = true;
                AL.DeleteSource(this._srcId);
            }

            this.Buffer?.Free();
            this.MpegDecoder?.Free();
        }

        public enum Type
        {
            Asset,
            Playlist,
            Ambient
        }
    }
}
