namespace VTT.Network
{
    using System;

    public class NetworkMonitor
    {
        public ulong LastValue { get; private set; }
        private object _lock = new object();
        private ulong _collected;
        private ulong _lastFrame;

        public void Tick()
        {
            lock (this._lock)
            {
                this.LastValue = this._collected;
                this._collected = 0;
            }
        }

        public void Increment(long by)
        {
            lock (this._lock)
            {
                this._collected += (uint)by;
            }
        }

        public void TickTimeframe()
        {
            ulong now = (ulong)DateTimeOffset.Now.ToUnixTimeMilliseconds();
            ulong last = this._lastFrame;
            if (now - last >= 1000)
            {
                this._lastFrame = now;
                this.Tick();
            }
        }
    }
}
