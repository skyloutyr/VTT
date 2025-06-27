namespace VTT.Render.Shaders
{
    using System.Numerics;
    using VTT.GL;

    public class Vector3UniformWrapper
    {
        private readonly UniformWrapper _uniform;
        private Vector3 _state;

        public Vector3UniformWrapper(UniformWrapper uniform)
        {
            this._uniform = uniform;
            this._state = Vector3.Zero;
        }

        public void Set(Vector3 v)
        {
            if (v != this._state)
            {
                this._state = v;
                this._uniform.Set(v);
            }
        }
    }
}
