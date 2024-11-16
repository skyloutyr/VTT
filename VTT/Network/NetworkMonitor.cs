namespace VTT.Network
{
    using System;
    using VTT.Util;

    public class NetworkMonitor
    {
        public ulong LastValue { get; private set; }

        private const int CaptureFrequency = 1000;
        private readonly object _lock = new object();
        private ulong _collected;
        private ulong _lastFrame;

        private UnsafeArray<ulong> _collectedValues;
        private int _valuesLength;

        public NetworkMonitor(int backbufferLength = 20) => this.AllocateValues(backbufferLength);

        public void AllocateValues(int amt)
        {
            this._collectedValues = new UnsafeArray<ulong>(amt);
            this._valuesLength = amt;
            for (int i = 0; i < this._valuesLength; ++i)
            {
                this._collectedValues[i] = 0ul;
            }
        }

        public unsafe void GetUnderlyingDataArray(out nint ptr, out int length)
        {
            ptr = (nint)this._collectedValues.GetPointer();
            length = this._valuesLength;
        }

        public void Free() => this._collectedValues?.Free();

        public unsafe void AppendValue(ulong value)
        {
            for (int i = 0; i < this._valuesLength - 1; ++i)
            {
                this._collectedValues[i] = this._collectedValues[i + 1];
            }

            this._collectedValues[this._valuesLength - 1] = value;
        }

        public void Tick()
        {
            lock (this._lock)
            {
                this.LastValue = this._collected;
                this._collected = 0;
                this.AppendValue(this.LastValue);
            }
        }

        public void Increment(long by)
        {
            lock (this._lock)
            {
                this._collected += (ulong)by;
            }
        }

        public void TickTimeframe()
        {
            ulong now = (ulong)DateTimeOffset.Now.ToUnixTimeMilliseconds();
            ulong last = this._lastFrame;
            if (now - last >= CaptureFrequency)
            {
                this._lastFrame = now;
                this.Tick();
            }
        }
    }
}
