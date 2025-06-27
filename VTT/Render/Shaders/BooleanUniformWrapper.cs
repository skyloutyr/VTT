namespace VTT.Render.Shaders
{
    using VTT.GL;

    public class BooleanUniformWrapper
    {
        private readonly UniformWrapper _uniform;
        private bool _state;

        public BooleanUniformWrapper(UniformWrapper uniform)
        {
            this._uniform = uniform;
            this._state = false;
        }

        public void Set(bool b)
        {
            if (b != this._state)
            {
                this._state = b;
                this._uniform.Set(b);
            }
        }
    }
}
