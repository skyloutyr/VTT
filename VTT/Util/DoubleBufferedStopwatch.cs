namespace VTT.Util
{
    using System.Diagnostics;

    public class DoubleBufferedStopwatch
    {
        private readonly Stopwatch _sw1;
        private readonly Stopwatch _sw2;
        private Stopwatch _current;

        public DoubleBufferedStopwatch()
        {
            this._sw1 = new Stopwatch();
            this._sw2 = new Stopwatch();
            this._current = this._sw1;
        }

        public void Restart() => this._current.Restart();
        public void Stop()
        {
            this._current.Stop();
            this._current = this._sw1 == this._current ? this._sw2 : this._sw1;
        }

        public Stopwatch Current => this._current;
        public Stopwatch Buffer => this._current == this._sw2 ? this._sw1 : this._sw2;
    }
}
