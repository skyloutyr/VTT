namespace VTT.Render.Shaders
{
    using VTT.Network;

    public class UniformBlockTime
    {
        [UniformReference("frame")]
        public UniformState<uint> Frame { get; set; }

        [UniformReference("update")]
        public UniformState<uint> Update { get; set; }

        public void SetFromCurrent()
        {
            this.Frame.Set((uint)Client.Instance.Frontend.FramesExisted);
            this.Update.Set((uint)Client.Instance.Frontend.UpdatesExisted);
        }
    }
}
