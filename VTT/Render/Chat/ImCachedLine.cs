namespace VTT.Render.Chat
{
    using ImGuiNET;
    using System.Linq;

    public readonly struct ImCachedLine
    {
        public ImCachedWord[] Words { get; }
        public float Height { get; }

        public ImCachedLine(ImCachedWord[] words)
        {
            this.Words = words;
            this.Height = this.Words.Length == 0 ? ImGui.GetFontSize() : this.Words.Max(w => w.Height);
        }
    }
}