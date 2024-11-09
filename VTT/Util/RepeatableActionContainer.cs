namespace VTT.Util
{
    using System;

    public class RepeatableActionContainer
    {
        private readonly Action _procGeneric;
        private readonly Func<bool> _procSpecialized;

        public RepeatableActionContainer(Action a) => this._procGeneric = a;
        public RepeatableActionContainer(Func<bool> a) => this._procSpecialized = a;

        public bool Invoke()
        {
            if (this._procSpecialized != null)
            {
                return this._procSpecialized();
            }
            else
            {
                this._procGeneric?.Invoke();
                return true;
            }
        }

        public static explicit operator RepeatableActionContainer(Action a) => new RepeatableActionContainer(a);
    }
}
