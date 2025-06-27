namespace VTT.Render.Shaders
{
    using System.Numerics;
    using VTT.GL;

    public class Vector4UniformWrapper
    {
        private readonly UniformWrapper _uniform;
        private Vector4 _state;

        public Vector4UniformWrapper(UniformWrapper uniform)
        {
            this._uniform = uniform;
            this._state = Vector4.Zero;
        }

        public void Set(Vector4 v)
        {
            if (v != this._state)
            {
                this._state = v;
                this._uniform.Set(v);
            }
        }
    }
}
