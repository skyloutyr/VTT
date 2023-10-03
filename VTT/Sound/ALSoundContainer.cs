namespace VTT.Sound
{
    using OpenTK.Audio.OpenAL;
    using VTT.Network;

    public class ALSoundContainer
    {
        private int _alId;
        public bool IsValid { get; private set; }
        public bool IsDataLoaded { get; private set; }
        public WaveAudio WaveData { get; }

        public ALSoundContainer(WaveAudio waveData)
        {
            this._alId = AL.GenBuffer();
            this.WaveData = waveData;
        }

        public int Instantiate()
        {
            this.LoadData(this.WaveData);
            if (this.IsValid)
            {
                int src = AL.GenSource();
                AL.Source(src, ALSourcei.Buffer, this._alId);
                return src;
            }

            return -1;
        }

        public void LoadData(WaveAudio data)
        {
            if (data.IsReady && !this.IsDataLoaded)
            {
                AL.BufferData(this._alId, data.NumChannels == 1 ? ALFormat.Mono16 : ALFormat.Stereo16, data.DataPtr, data.DataLength, data.SampleRate);
                ALError ale = AL.GetError();
                if (ale != ALError.NoError)
                {
                    Client.Instance.Logger.Log(Util.LogLevel.Error, $"OpenAL error when loading audio data - {AL.GetErrorString(ale)}");
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
