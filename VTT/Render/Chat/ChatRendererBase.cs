namespace VTT.Render.Chat
{
    using VTT.Control;

    public abstract class ChatRendererBase
    {
        public ChatLine Container { get; }

        public ChatRendererBase(ChatLine container) => this.Container = container;

        public abstract void Render();
        public abstract void Cache(out float width, out float height);
        public abstract void ClearCache();
        public string TextOrEmpty(string sIn) => string.IsNullOrEmpty(sIn) ? " " : sIn;
    }
}
