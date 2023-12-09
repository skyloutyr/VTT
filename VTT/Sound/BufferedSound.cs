namespace VTT.Sound
{
    using OpenTK.Audio.OpenAL;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using VTT.Network;

    public class BufferedSound
    {
        public Guid AssetID { get; set; }

        private int _front;
        private int _back;
        private int _freq;
        private int _maxChunks;

        private int _src;

        private bool _initialized;

        private bool _haveFront;
        private bool _haveBack;
        private int _lastChunkFetched;
        private int _operationalBuffer = -1;

        public int Source => this._src;
        public bool Initialized => this._initialized;

        private Dictionary<int, IntPtr> _ptrs = new Dictionary<int, IntPtr>(); // Needed due to openal's async issues with some implementations that don't copy the data on the spot

        public BufferedSound(int frequency, int numChunks, Guid assetID)
        {
            this._front = AL.GenBuffer();
            this._back = AL.GenBuffer();

            this._src = AL.GenSource();

            this._freq = frequency;
            this._maxChunks = numChunks;
            this.AssetID = assetID;
        }

        public bool QueueBuffer(int buffer, int chunkIndex)
        {
            SoundManager mgr = Client.Instance.Frontend.Sound;
            if (mgr.RequestBufferedSound(this.AssetID, chunkIndex, out ushort[] data) && data != null)
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

                AL.BufferData(buffer, ALFormat.Stereo16, ptr, data.Length * sizeof(ushort), this._freq);
                return true;
            }

            return false;
        }

        public void Update()
        {
            if (!this._initialized)
            {
                SoundManager mgr = Client.Instance.Frontend.Sound;
                if (!this._haveFront)
                {
                    if (this.QueueBuffer(this._front, 0))
                    {
                        this._haveFront = true;
                    }
                }

                if (!this._haveBack)
                {
                    if (this.QueueBuffer(this._back, 1))
                    {
                        this._haveBack = true;
                    }
                }

                if (this._haveFront && this._haveBack)
                {
                    this._lastChunkFetched = 1;
                    this._initialized = true;
                    AL.SourceQueueBuffer(this._src, this._front);
                    AL.SourceQueueBuffer(this._src, this._back);
                    mgr.BufferedSourceReadyCallback(this._src);
                    AL.SourcePlay(this._src);
                }
            }
            else
            {
                AL.GetSource(this._src, ALGetSourcei.BuffersProcessed, out int i);
                if (i > 0 && this._operationalBuffer == -1)
                {
                    int buf = AL.SourceUnqueueBuffer(this._src);
                    this._operationalBuffer = buf;
                }

                if (this._operationalBuffer != -1)
                {
                    if (this._lastChunkFetched + 1 < this._maxChunks)
                    {
                        if (this.QueueBuffer(this._operationalBuffer, this._lastChunkFetched + 1))
                        {
                            AL.SourceQueueBuffer(this._src, this._operationalBuffer);
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

            AL.DeleteSource(this._src);
            foreach (KeyValuePair<int, IntPtr> a in this._ptrs)
            {
                Marshal.FreeHGlobal(a.Value);
            }
        }
    }
}
