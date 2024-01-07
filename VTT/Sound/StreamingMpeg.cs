namespace VTT.Sound
{
    using NLayer;
    using System;
    using System.IO;
    using VTT.Network;
    using VTT.Util;

    public class StreamingMpeg
    {
        private MpegFile _mpeg;
        private MemoryStream _ms;
        private int _lastReadIndex;
        private int _totalData;
        private readonly float[] _buf;
        private readonly UnsafeResizeableArray<byte> _rArray;
        private int _maxBufferStep;

        public StreamingMpeg()
        {
            this._lastReadIndex = this._totalData = 0;
            this._buf = new float[1152];
            this._rArray = new UnsafeResizeableArray<byte>(1152 * 4);
        }

        // Add data to the underlying ms and reset its position to what it was before writing to preserve coherency
        // Non-thread safe, but writing and reading ops happen on the same thread in a strict order, so maybe fine
        public void AddData(byte[] dataBuffer) 
        {
            if (this._mpeg == null)
            {
                this._ms = new MemoryStream();
            }

            this._ms.Position = this._totalData; // Need to set the stream to end first, write afterwards
            this._ms.Write(dataBuffer, 0, dataBuffer.Length);
            this._totalData += dataBuffer.Length;
            this._ms.Position = this._lastReadIndex;
            if (this._mpeg == null)
            {
                this._mpeg = new MpegFile(this._ms);
            }
        }

        // This method needs to be set up in a very particular way due to the MpegFile internals
        // The lib was designed to read a file in an entirety, without streaming
        // And will close the underlying readers and buffers when it reaches EOF
        // The stream itself will remain open until we call dispose on MpegFile or stream itself though, so no exception catching
        // And due to mpeg frame reading the first 2711 samples (no clue why the number is odd, maybe 1 byte of padding per layer 2/3 spec? But the number was revealed by analyzing each decoded frame precisely) will be discarded as headers+nfo (presumably)
        // As such the idea is to cautiously read some frames until we are about to reach end of stream, and abort unless we actually want to read to EOF (expect more parameter controls that)
        // While underlying readers are outside of our control, they will still advance the stream, and thus we know when to stop
        // Also this unfortunately keeps the entire MS in memory for streaming. 
        // TODO figure out a buffered memory stream implementation which will discard earlier data no longer in use
        public byte[] ReadSamples(bool expectMore, out bool readToEnd)
        {
            int lastMsPosition = (int)this._ms.Position;
            while (true)
            {
                int p = (int)this._ms.Position;
                // From analysis the internal reader always advances the stream by 4032 at a time, but still worth being cautious
                if (p + Math.Max(4096, this._maxBufferStep) >= this._ms.Length && expectMore) 
                {
                    // Can't read more as we could reach EOF (unlikely THIS read, but may at a later read still)
                    readToEnd = false;
                    break;
                }

                // Only read 1 layer 3 frame just in case
                // Although based on analysis we will obtain 576 bytes, and we should ALWAYS obtain 576 bytes due to layer III encoding (and encoding will ALWAYS be layer III) it is still worth reading up to maximum allowed by spec just in case
                // The case being if Layer II somehow is encoded (bad ffmpeg?) we will fail the read entirely if buffer is not large enough, read 0, reach EOF and die
                // Using a f32 due to clipping issues (library blissfuly unaware of clipping and causes bad audio output)
                int r = this._mpeg.ReadSamples(this._buf, 0, 1152); 
                if (r <= 0)
                {
                    // Reached eof
                    readToEnd = true;
                    break;
                }

                for (int i = 0; i < r; ++i)
                {
                    float f = this._buf[i];
                    int hr_i = (int)(float.IsNegative(f) ? f * 0x8000 : f * 0x7fff);

                    // Clipping has to be done 1 byte under for both min and max for unknown reason.
                    // If done to usual min/max (which is spec) causes audible crackling sounds in playback
                    // If clipped 1 byte under all crackling sounds are eliminated
                    // Interesting that this ONLY has to be done here, no such issue exists in WaveAudio's clipping implementation
                    // Maybe bad ffmpeg encoding/scaling?
                    short s = (short)Math.Clamp(hr_i, short.MinValue + 1, short.MaxValue - 1);
                    unsafe
                    {
                        ushort us = *(ushort*)&s;
                        this._rArray.AddRange(BitConverter.GetBytes(us), 0, sizeof(ushort));
                    }
                }

                if (lastMsPosition != this._ms.Position)
                {
                    int delta = (int)(this._ms.Position - lastMsPosition);
                    if (delta > 4096 && delta > this._maxBufferStep)
                    {
                        Client.Instance.Logger.Log(LogLevel.Warn, $"Abrubpt sound delta change (to {delta} bytes)!");
                    }

                    this._maxBufferStep = (int)Math.Max(this._maxBufferStep, delta);
                    lastMsPosition = (int)this._ms.Position;
                }
            }

            byte[] ret = this._rArray.ToBytes();
            this._rArray.Reset();
            this._lastReadIndex = (int)this._ms.Position;
            return ret;
        }

        public void Free()
        {
            try
            {
                this._mpeg.Dispose();
            }
            catch
            {
                // NOOP
            }

            try
            {
                this._ms.Dispose();
            }
            catch
            {
                // NOOP
            }

            try
            {
                this._rArray.Free();
            }
            catch
            {
                // NOOP
            }
        }
    }
}
