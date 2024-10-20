namespace VTT.Util
{
    using System;

    public class ObservableValue<T>
    {
        private T _value;
        public T Value
        {
            get => this._value;
            set
            {
                bool bchange = !value.Equals(this._value);
                this._value = value;
                if (bchange)
                {
                    this.ValueChanged = true;
                    this.OnValueChanged?.Invoke(this);
                }
            }
        }

        public ObservableValue(T val, bool initialState = false)
        {
            this._value = val;
            this.ValueChanged = initialState;
        }

        public void ChangeWithoutNotify(T val) => this._value = val;

        public bool ValueChanged { get; set; }

        public event Action<ObservableValue<T>> OnValueChanged;

        public static implicit operator T(ObservableValue<T> self) => self._value;
    }
}
