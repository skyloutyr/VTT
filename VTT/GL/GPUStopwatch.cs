namespace VTT.GL
{
    using VTT.GL.Bindings;
    using OGL = VTT.GL.Bindings.GL;

    public class GPUStopwatch
    {
        private readonly uint _id;
        private long _lastTimeNanos;
        private bool _wasEverStarted;
        private bool _started = false;

        public long TimeElapsedNanos => this._lastTimeNanos;

        public double ElapsedMillis => this.TimeElapsedNanos / 1000000d;

        public bool IsQueryAvailable => OGL.GetQueryObjectUnsignedLong(this._id, QueryProperty.ResultAvailable) == 1;

        public GPUStopwatch() => this._id = OGL.GenQuery();
        public void Restart()
        {
            if (this._wasEverStarted)
            {
                if (this.IsQueryAvailable)
                {
                    this._lastTimeNanos = OGL.GetQueryObjectLong(this._id, QueryProperty.Target);
                    OGL.BeginQuery(QueryTarget.TimeElapsed, this._id);
                    this._started = true;
                }
            }
            else
            {
                GL.BeginQuery(QueryTarget.TimeElapsed, this._id);
                this._started = true;
                this._wasEverStarted = true;
            }
        }

        public void Stop()
        {
            if (this._started)
            {
                GL.EndQuery(QueryTarget.TimeElapsed);
                this._started = false;
            }
        }

        public void Dispose() => OGL.DeleteQuery(this._id);
    }
}
