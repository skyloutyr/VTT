namespace VTT.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class Promise<T>
    {
        private bool _done;
        private T _val;
        public bool Done => this._done;

        public Promise()
        {
        }

        public Promise(T val)
        {
            this._val = val;
            this._done = true;
        }

        public void SetValue(T val)
        {
            this._val = val;
            this._done = true;
        }


        public static explicit operator T(Promise<T> self) => self._val;
    }
}
