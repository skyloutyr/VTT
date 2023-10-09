namespace VTT.Util
{
    using System;

    public class SmoothedDouble
    {
        public int SmoothSteps { get; init; }

        private double[] _steps;
        private int _index = 0;
        private bool _everInserted = false;

        public SmoothedDouble(int steps)
        {
            this.SmoothSteps = steps;
            this._steps = new double[steps];
        }

        public double GetAndInsert(double newVal)
        {
            if (!this._everInserted)
            {
                for (int i = 0; i < this._steps.Length; ++i)
                {
                    this._steps[i] = newVal;
                }

                this._everInserted = true;
            }

            this._steps[this._index] = newVal;
            double inf = 1d / this.SmoothSteps;
            double final = 0;
            int nfmod(int l, int m) => (int)(l - m * MathF.Floor((float)l / m));
            for (int i = 0; i < this.SmoothSteps; ++i)
            {
                final += this._steps[nfmod(this._index - i, this.SmoothSteps)] * inf;
            }

            this._index += 1;
            if (this._index == this._steps.Length)
            {
                this._index = 0;
            }

            return final;
        }
    }
}
