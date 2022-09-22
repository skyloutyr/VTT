namespace VTT.Render.Chat
{
    using VTT.Control;
    using VTT.Util;

    public class ChatRendererRollExpression : ChatRendererBase
    {
        private readonly ChatRendererLine _lineRenderer;

        public ChatRendererRollExpression(ChatLine container) : base(container) => this._lineRenderer = new ChatRendererLine(container);

        public override void Cache(out float width, out float height)
        {
            string fullText = this.Container.GetFullText();
            ChatBlock answer = ChatParser.ParseExpression(fullText);
            ChatBlock separator = this.Container.CreateContextBlock(" = ");
            answer.DoNotPersist = separator.DoNotPersist = true;
            this.Container.Blocks.Add(separator);
            this.Container.Blocks.Add(answer);
            this._lineRenderer.Cache(out width, out height);
        }

        public override void ClearCache()
        {
            this._lineRenderer.ClearCache();
            this.Container.PopChatBlock(true);
            this.Container.PopChatBlock(true);
        }

        public override void Render() => this._lineRenderer.Render();
    }
}
