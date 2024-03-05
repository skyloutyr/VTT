namespace VTT.Util
{
    using System;

    public class ObservableValue<T>
    {
        private T value;
        public T Value
        {
            get => this.value;
            set
            {
                bool bchange = !value.Equals(this.value);
                this.value = value;
                if (bchange)
                {
                    this.ValueChanged = true;
                    this.OnValueChanged?.Invoke(this);
                }
            }
        }

        public ObservableValue(T val, bool initialState = false)
        {
            this.value = val;
            this.ValueChanged = initialState;
        }

        public void ChangeWithoutNotify(T val) => this.value = val;

        public bool ValueChanged { get; set; }

        public event Action<ObservableValue<T>> OnValueChanged;

        public static implicit operator T(ObservableValue<T> self) => self.value;
    }
}
