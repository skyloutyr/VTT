namespace VTT.Sound
{
    using System;
    using VTT.Asset;
    using VTT.Network;
    using VTT.Sound.Bindings;

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
        public AssetDataType DataType { get; private set; } = AssetDataType.Unknown;
        public int NumChunksSpecified { get; set; }
        public float SecondsPlayed { get; set; }
        public float Volume
        {
            get => this._volumeBase;
            set
            {
                if (this._volumeBase != value)
                {
                    this._volumeChanged = true;
                }

                this._volumeBase = value;
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

            AL.Source((uint)this._srcId, SourceFloatProperty.Gain, this.Container.AdjustVolumeForNonLinearity(vCat * this._volumeBase));
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
                    AL.SourcePlay((uint)this._srcId);
                    this.Started = true;
                    this.DataType = AssetDataType.Full;
                    Client.Instance.Logger.Log(Util.LogLevel.Debug, $"Sound {this.ID} is RAW, playing.");
                }
                else
                {
                    AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(this.AssetID, AssetType.Sound, out Asset a);
                    if (status == AssetStatus.Return)
                    {
                        if (a?.Type == AssetType.Sound && a?.Sound != null && a?.Sound?.Meta != null)
                        {
                            this.NumChunksSpecified = a.Sound.Meta.TotalChunks;
                            SoundData sd = a.Sound;
                            if (sd.Meta.IsFullData) // Have raw audio
                            {
                                this.Container.AddFullSoundData(this.AssetID, a);
                                this.DataType = AssetDataType.Full;
                                Client.Instance.Logger.Log(Util.LogLevel.Debug, $"Sound {this.ID} was specified as full data, requeueing after receiving asset.");
                                return; // Will play on next tick unless sound manager had issues/cleared the sound list
                            }
                            else
                            {
                                this.Buffer = new SoundBuffer(sd.Meta.SampleRate, sd.Meta.NumChannels, sd.Meta.TotalChunks, this.AssetID, out this._srcId);
                                // Mpeg container
                                if (sd.Meta.SoundType == SoundData.Metadata.StorageType.Mpeg)
                                {
                                    this.DataType = AssetDataType.StreamingMpeg;
                                    this.MpegDecoder = new StreamingMpeg();
                                    Client.Instance.Logger.Log(Util.LogLevel.Debug, $"Sound {this.ID} was specified as mpeg compressed, buffer and decoder created, sound queued.");
                                }
                                else
                                {
                                    this.DataType = AssetDataType.StreamingPCM;
                                    Client.Instance.Logger.Log(Util.LogLevel.Debug, $"Sound {this.ID} was specified as non-mpeg streaming sound, buffer created and queued.");
                                }

                                this.VolumeChangedNotification();
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

            if (AL.IsSource((uint)this._srcId))
            {
                this.Buffer?.Update(this);
                SourceState alss = AL.GetSourceState((uint)this._srcId);

                if (alss == SourceState.Playing)
                {
                    if (this._volumeChanged && this.Started)
                    {
                        this.VolumeChangedNotification();
                        this._volumeChanged = false;
                    }
                }

                if (alss == SourceState.Stopped)
                {
                    this.Stopped = true;
                    AL.DeleteSource((uint)this._srcId);
                    this._srcId = -1;
                    return;
                }

                float vS = AL.GetSource((uint)this._srcId, SourceFloatProperty.SecOffset);
                float delta = vS - this._lastSoundVS;
                if (delta > 0)
                {
                    this.SecondsPlayed += delta;
                    this._lastSoundDelta = delta;
                }
                else
                {
                    if (float.IsNegative(delta))
                    {
                        this.SecondsPlayed += this._lastSoundDelta;
                    }
                }

                this._lastSoundVS = vS;
            }
        }

        private float _lastSoundVS;
        private float _lastSoundDelta;

        public void Free()
        {
            Client.Instance.Logger.Log(Util.LogLevel.Debug, $"Sound {this.ID} stopped and freed.");
            if (this._srcId != -1)
            {
                this.Stopped = true;
                if (AL.IsSource((uint)this._srcId))
                {
                    if (AL.GetSourceState((uint)this._srcId) == SourceState.Playing)
                    {
                        AL.SourceStop((uint)this._srcId);
                    }

                    AL.DeleteSource((uint)this._srcId);
                }
            }

            this.Buffer?.Free();
            this.MpegDecoder?.Free();
        }

        public enum Type
        {
            Asset,
            Music,
            Ambient
        }

        public enum AssetDataType
        {
            Unknown,
            Full,
            StreamingPCM,
            StreamingMpeg
        }
    }
}
