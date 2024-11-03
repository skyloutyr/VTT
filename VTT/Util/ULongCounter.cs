namespace VTT.Util
{
    public class ULongCounter
    {
        private ulong _val;

        public ULongCounter(ulong v = 0)
        {
            this._val = v;
        }

        public void Increment(ulong by = 1) => this._val += by;
        public void Decrement(ulong by = 1) => this._val -= by;
        public void Set(ulong to) => this._val = to;

        public static implicit operator ulong(ULongCounter v) => v._val;
        public static explicit operator long(ULongCounter v) => (long)(v._val & long.MaxValue);
        public static explicit operator uint(ULongCounter v) => (uint)(v._val & uint.MaxValue);
        public static explicit operator int(ULongCounter v) => (int)(v._val & int.MaxValue);
    }
}
