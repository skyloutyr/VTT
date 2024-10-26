namespace VTT.Sound
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using VTT.Network;
    using VTT.Sound.Bindings;
    using VTT.Util;

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
            this._front = (int)AL.GenBuffer();
            this._back = (int)AL.GenBuffer();

            src = (int)AL.GenSource();

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
                    unsafe
                    {
                        MemoryHelper.Free((void*)val);
                    }
                }

                unsafe
                {
                    IntPtr ptr = this._ptrs[buffer] = (IntPtr)MemoryHelper.Allocate<ushort>((nuint)data.Length);
                    fixed (ushort* srcPtr = data)
                    {
                        Buffer.MemoryCopy(srcPtr, (void*)ptr, data.Length * sizeof(ushort), data.Length * sizeof(ushort));
                    }

                    AL.BufferData((uint)buffer, this._nChannels == 1 ? SoundDataFormat.Mono16 : SoundDataFormat.Stereo16, ptr, data.Length * sizeof(ushort), this._freq);
                }

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
                    AL.SourceQueueBuffer((uint)container.SourceID, (uint)this._front);
                    AL.SourceQueueBuffer((uint)container.SourceID, (uint)this._back);
                    mgr.BufferedSourceReadyCallback(container.Category, container.SourceID);
                    AL.SourcePlay((uint)container.SourceID);
                }
            }
            else
            {
                int i = AL.GetSourceBuffersProcessed((uint)container.SourceID);
                if (i > 0 && this._operationalBuffer == -1)
                {
                    int buf = (int)AL.SourceUnqueueBuffer((uint)container.SourceID);
                    this._operationalBuffer = buf;
                }

                if (this._operationalBuffer != -1)
                {
                    if (this._lastChunkFetched + 1 < this._maxChunks)
                    {
                        if (this.QueueBuffer(container.ID, this._operationalBuffer, this._lastChunkFetched + 1))
                        {
                            AL.SourceQueueBuffer((uint)container.SourceID, (uint)this._operationalBuffer);
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
                AL.DeleteBuffer((uint)this._front);
                AL.DeleteBuffer((uint)this._back);
            }

            foreach (KeyValuePair<int, IntPtr> a in this._ptrs)
            {
                unsafe
                {
                    MemoryHelper.Free((void*)a.Value);
                }
            }
        }
    }
}
