namespace VTT.Network
{
    using System;
    using System.Runtime.InteropServices;

    public class NetworkMonitor
    {
        public ulong LastValue { get; private set; }

        private const int CaptureFrequency = 1000;
        private readonly object _lock = new object();
        private ulong _collected;
        private ulong _lastFrame;

        private nint _collectedValues;
        private int _valuesLength;

        public NetworkMonitor(int backbufferLength = 20) => this.AllocateValues(backbufferLength);

        public void AllocateValues(int amt)
        {
            this._collectedValues = Marshal.AllocHGlobal(amt * sizeof(ulong));
            this._valuesLength = amt;
            unsafe
            {
                ulong* data = (ulong*)this._collectedValues;
                for (int i = 0; i < this._valuesLength; ++i)
                {
                    data[i] = 0ul;
                }
            }
        }

        public void GetUnderlyingDataArray(out nint ptr, out int length)
        {
            ptr = this._collectedValues;
            length = this._valuesLength;
        }

        public void Free() => Marshal.FreeHGlobal(this._collectedValues);

        public unsafe void AppendValue(ulong value)
        {
            ulong* data = (ulong*)this._collectedValues;
            for (int i = 0; i < this._valuesLength - 1; ++i)
            {
                data[i] = data[i + 1];
            }

            data[this._valuesLength - 1] = value;
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
