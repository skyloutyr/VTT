namespace VTT.Sound
{
    using System;
    using VTT.Network;
    using VTT.Sound.Bindings;

    public class ALSoundContainer
    {
        private uint _alId;
        public bool IsValid { get; set; }
        public bool IsDataLoaded { get; set; }
        public ISoundProvider WaveData { get; set; }

        public ALSoundContainer() => this.IsValid = false;

        public ALSoundContainer(WaveAudio waveData)
        {
            this._alId = AL.GenBuffer();
            this.WaveData = waveData;
        }

        public void CreateFromData(WaveAudio waveData)
        {
            this._alId = AL.GenBuffer();
            this.WaveData = waveData;
        }

        public int Instantiate()
        {
            this.LoadData(this.WaveData);
            if (this.IsValid)
            {
                uint src = AL.GenSource();
                AL.SourceBuffer(src, this._alId);
                return (int)src;
            }

            return -1;
        }

        public void LoadData(ISoundProvider data)
        {
            if (data.IsReady && !this.IsDataLoaded)
            {
                data.GetRawDataFull(out IntPtr dataPtr, out int dataLength);
                AL.BufferData(this._alId, data.NumChannels == 1 ? SoundDataFormat.Mono16 : SoundDataFormat.Stereo16, dataPtr, dataLength, data.SampleRate);
                ALError ale = AL.GetError();
                if (ale != ALError.NoError)
                {
                    Client.Instance.Logger.Log(Util.LogLevel.Error, $"OpenAL error when loading audio data - {ale}");
                    this.IsValid = false;
                }
                else
                {
                    this.IsValid = true;
                }

                this.IsDataLoaded = true;
            }
        }

        public void Free()
        {
            AL.DeleteBuffer(this._alId);
            this.WaveData.Free();
        }
    }
}
