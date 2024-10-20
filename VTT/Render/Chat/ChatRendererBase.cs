namespace VTT.Render.Chat
{
    using System;
    using System.Numerics;
    using System.Text.RegularExpressions;
    using VTT.Control;
    using VTT.Util;

    public abstract class ChatRendererBase
    {
        public static Regex RollSyntaxRegex { get; } = new Regex("roll\\(([0-9]+), ([0-9]+)\\)\\[=", RegexOptions.Compiled);

        public ChatLine Container { get; }

        public ChatRendererBase(ChatLine container) => this.Container = container;

        public abstract void Render();
        public abstract void Cache(Vector2 windowSize, out float width, out float height);
        public abstract void ClearCache();
        public abstract string ProvideTextForClipboard(DateTime dateTime, string senderName, SimpleLanguage lang);

        public static string TextOrAlternative(string text, string alt) => string.IsNullOrEmpty(text) ? alt : text;
    }
}
