namespace VTT.Render.Shaders
{
    using VTT.GL;

    public class IntegerUniformWrapper
    {
        private readonly UniformWrapper _uniform;
        private int _state;

        public IntegerUniformWrapper(UniformWrapper uniform)
        {
            this._uniform = uniform;
            this._state = 0;
        }

        public void Set(int i)
        {
            if (i != this._state)
            {
                this._state = i;
                this._uniform.Set(i);
            }
        }
    }
}
