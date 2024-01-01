namespace VTT.Sound
{
    using OpenTK.Audio.OpenAL;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using VTT.Network;

    public class SoundBuffer
    {
        public Guid AssetID { get; set; }

        private readonly int _front;
        private readonly int _back;
        private readonly int _freq;
        private readonly int _maxChunks;

        private bool _initialized;

        private bool _haveFront;
        private bool _haveBack;
        private int _lastChunkFetched;
        private int _operationalBuffer = -1;
        private readonly int _nChannels;

        public bool Initialized => this._initialized;

        private readonly Dictionary<int, IntPtr> _ptrs = new Dictionary<int, IntPtr>(); // Needed due to openal's async issues with some implementations that don't copy the data on the spot

        public SoundBuffer(int frequency, int numChannels, int numChunks, Guid assetID, out int src)
        {
            this._front = AL.GenBuffer();
            this._back = AL.GenBuffer();

            src = AL.GenSource();

            this._freq = frequency;
            this._nChannels = numChannels;
            this._maxChunks = numChunks;
            this.AssetID = assetID;
        }

        public bool QueueBuffer(Guid cId, int buffer, int chunkIndex)
        {
            SoundManager mgr = Client.Instance.Frontend.Sound;
            if (mgr.RequestBufferedSound(cId, this.AssetID, chunkIndex, out ushort[] data) && data != null)
            {
                if (this._ptrs.TryGetValue(buffer, out IntPtr val))
                {
                    Marshal.FreeHGlobal(val);
                }

                IntPtr ptr = this._ptrs[buffer] = Marshal.AllocHGlobal(data.Length * sizeof(ushort));
                unsafe
                {
                    fixed (ushort* srcPtr = data)
                    {
                        Buffer.MemoryCopy(srcPtr, (void*)ptr, data.Length * sizeof(ushort), data.Length * sizeof(ushort));
                    }
                }

                AL.BufferData(buffer, this._nChannels == 1 ? ALFormat.Mono16 : ALFormat.Stereo16, ptr, data.Length * sizeof(ushort), this._freq);
                return true;
            }

            return false;
        }

        public void Update(AssetSound container)
        {
            if (!this._initialized)
            {
                SoundManager mgr = Client.Instance.Frontend.Sound;
                if (!this._haveFront)
                {
                    if (this.QueueBuffer(container.ID, this._front, 0))
                    {
                        this._haveFront = true;
                    }
                }

                if (!this._haveBack)
                {
                    if (this.QueueBuffer(container.ID, this._back, 1))
                    {
                        this._haveBack = true;
                    }
                }

                if (this._haveFront && this._haveBack)
                {
                    this._lastChunkFetched = 1;
                    this._initialized = true;
                    AL.SourceQueueBuffer(container.SourceID, this._front);
                    AL.SourceQueueBuffer(container.SourceID, this._back);
                    mgr.BufferedSourceReadyCallback(container.Category, container.SourceID);
                    AL.SourcePlay(container.SourceID);
                }
            }
            else
            {
                AL.GetSource(container.SourceID, ALGetSourcei.BuffersProcessed, out int i);
                if (i > 0 && this._operationalBuffer == -1)
                {
                    int buf = AL.SourceUnqueueBuffer(container.SourceID);
                    this._operationalBuffer = buf;
                }

                if (this._operationalBuffer != -1)
                {
                    if (this._lastChunkFetched + 1 < this._maxChunks)
                    {
                        if (this.QueueBuffer(container.ID, this._operationalBuffer, this._lastChunkFetched + 1))
                        {
                            AL.SourceQueueBuffer(container.SourceID, this._operationalBuffer);
                            this._lastChunkFetched += 1;
                            this._operationalBuffer = -1;
                        }
                    }
                }
            }
        }

        public void Free()
        {
            if (this._initialized)
            {
                AL.DeleteBuffer(this._front);
                AL.DeleteBuffer(this._back);
            }

            foreach (KeyValuePair<int, IntPtr> a in this._ptrs)
            {
                Marshal.FreeHGlobal(a.Value);
            }
        }
    }
}
