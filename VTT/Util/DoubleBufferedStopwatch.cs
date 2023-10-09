namespace VTT.Util
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class DoubleBufferedStopwatch
    {
        private Stopwatch _sw1;
        private Stopwatch _sw2;
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
