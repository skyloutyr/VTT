namespace VTT.Render.Chat
{
    using System;
    using System.Numerics;
    using VTT.Control;

    public readonly struct ImCachedWord
    {
        public ChatBlock Owner { get; }
        public string Text { get; }
        public float Width { get; }
        public float Height { get; }
        public Vector2 TextSize { get; }
        public bool IsExpression { get; }

        public ImCachedWord(ChatBlock owner, string text, float minWidth = 0, float minHeight = 0)
        {
            this.Owner = owner;
            this.Text = text;
            Vector2 systemSize = this.TextSize = ImGuiHelper.CalcTextSize(this.Text);
            this.IsExpression = owner.Type.HasFlag(ChatBlockType.Expression);
            this.Width =  MathF.Max(minWidth, systemSize.X + (this.IsExpression ? 8 : 0));
            this.Height = MathF.Max(minHeight, systemSize.Y + (this.IsExpression ? 8 : 0));
        }
    }
}