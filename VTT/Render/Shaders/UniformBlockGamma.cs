namespace VTT.Render.Shaders
{
    public class UniformBlockGamma
    {
        [UniformReference("gamma_correct")]
        public UniformState<bool> EnableCorrection { get; set; }

        [UniformReference("gamma_factor")]
        public UniformState<float> Factor { get; set; }
    }
}
