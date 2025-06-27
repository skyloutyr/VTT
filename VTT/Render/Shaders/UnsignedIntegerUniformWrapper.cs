namespace VTT.Render.Shaders
{
    using VTT.GL;

    public class UnsignedIntegerUniformWrapper
    {
        private readonly UniformWrapper _uniform;
        private uint _state;

        public UnsignedIntegerUniformWrapper(UniformWrapper uniform)
        {
            this._uniform = uniform;
            this._state = 0;
        }

        public void Set(uint ui)
        {
            if (ui != this._state)
            {
                this._state = ui;
                this._uniform.Set(ui);
            }
        }
    }
}
