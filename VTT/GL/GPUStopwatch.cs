namespace VTT.GL
{
    using OpenTK.Graphics.OpenGL;
    using System;
    using System.Threading;

    public class GPUStopwatch
    {
        private uint _id;
        private long _lastTimeNanos;
        private bool _wasEverStarted;
        private bool _started = false;

        public long TimeElapsedNanos
        {
            get
            {
                return this._lastTimeNanos;
            }
        }

        public double ElapsedMillis => this.TimeElapsedNanos / 1000000d;

        public bool IsQueryAvailable
        {
            get
            {
                GL.GetQueryObject(this._id, GetQueryObjectParam.QueryResultAvailable, out ulong i);
                return i == (int)All.True;
            }
        }

        public GPUStopwatch() => GL.GenQueries(1, out this._id);
        public void Restart()
        {
            if (this._wasEverStarted)
            {
                if (this.IsQueryAvailable)
                {
                    GL.GetQueryObject(this._id, GetQueryObjectParam.QueryTarget, out this._lastTimeNanos);
                    GL.BeginQuery(QueryTarget.TimeElapsed, this._id);
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

        public void Dispose() => GL.DeleteQuery(this._id);
    }
}
