namespace VTT.Render.Shaders
{
    using System.Numerics;
    using VTT.GL;

    public class Vector2UniformWrapper
    {
        private readonly UniformWrapper _uniform;
        private Vector2 _state;

        public Vector2UniformWrapper(UniformWrapper uniform)
        {
            this._uniform = uniform;
            this._state = Vector2.Zero;
        }

        public void Set(Vector2 v)
        {
            if (v != this._state)
            {
                this._state = v;
                this._uniform.Set(v);
            }
        }
    }
}
