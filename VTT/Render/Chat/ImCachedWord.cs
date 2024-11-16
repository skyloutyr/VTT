namespace VTT.Render.Chat
{
    using System.Numerics;
    using VTT.Control;

    public readonly struct ImCachedWord
    {
        public ChatBlock Owner { get; }
        public string Text { get; }
        public float Width { get; }
        public float Height { get; }
        public bool IsExpression { get; }

        public ImCachedWord(ChatBlock owner, string text)
        {
            this.Owner = owner;
            this.Text = text;
            Vector2 systemSize = ImGuiHelper.CalcTextSize(this.Text);
            this.IsExpression = owner.Type.HasFlag(ChatBlockType.Expression);
            this.Width = systemSize.X + (this.IsExpression ? 8 : 0);
            this.Height = systemSize.Y + (this.IsExpression ? 8 : 0);
        }
    }
}