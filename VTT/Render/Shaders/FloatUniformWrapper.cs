namespace VTT.Render.Shaders
{
    using VTT.GL;

    public class FloatUniformWrapper
    {
        private readonly UniformWrapper _uniform;
        private float _state;

        public FloatUniformWrapper(UniformWrapper uniform)
        {
            this._uniform = uniform;
            this._state = 0.0f;
        }

        public void Set(float f)
        {
            if (f != this._state)
            {
                this._state = f;
                this._uniform.Set(f);
            }
        }
    }
}
