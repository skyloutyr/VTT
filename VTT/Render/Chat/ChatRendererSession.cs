namespace VTT.Render.Chat
{
    using ImGuiNET;
    using System;
    using System.Numerics;
    using VTT.Control;
    using VTT.Util;

    public class ChatRendererSession : ChatRendererBase
    {
        public ChatRendererSession(ChatLine container) : base(container)
        {
        }

        public override void Cache(Vector2 windowSize, out float width, out float height)
        {
            width = 300;
            height = 8 + ImGui.GetTextLineHeightWithSpacing() + 16;
        }

        public override void ClearCache()
        {
        }

        public override string ProvideTextForClipboard(DateTime dateTime, string senderName, SimpleLanguage lang)
        {
            return this.Container.Blocks.Count == 1
                ? this.Container.Blocks[0].Text
                : this.Container.Blocks[1].Text + " " + this.Container.Blocks[0].Text;
        }

        public override unsafe void Render(Guid senderId, uint senderColorAbgr)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 localPos = ImGui.GetCursorPos();
            Vector2 pos = ImGui.GetCursorScreenPos();
            Vector2 size = ImGui.GetContentRegionAvail();
            uint gold = ColorAbgr.Gold;
            uint nothing = ColorAbgr.Transparent;
            float h = 24 + ImGui.GetTextLineHeightWithSpacing();

            drawList.AddRectFilledMultiColor(
                new Vector2(pos.X, pos.Y),
                new Vector2(pos.X + size.X, pos.Y + 4),
                gold,
                nothing,
                nothing,
                gold
            );

            drawList.AddRectFilledMultiColor(
                new Vector2(pos.X, pos.Y + h - 4),
                new Vector2(pos.X + size.X, pos.Y + h),
                nothing,
                gold,
                gold,
                nothing
            );

            if (this.Container.Blocks.Count == 1)
            {
                string text = this.Container.Blocks[0].Text;
                Vector2 center = localPos + (new Vector2(size.X, h) / 2);
                ImGui.SetCursorPos(center - (ImGuiHelper.CalcTextSize(text) / 2));
                ImGui.PushStyleColor(ImGuiCol.Text, this.Container.Blocks[0].Color.Abgr());
                ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(text));
                ImGui.PopStyleColor();
            }
            else
            {
                if (this.Container.Blocks.Count == 2)
                {
                    string text = this.Container.Blocks[1].Text;
                    Vector2 center = localPos + (new Vector2(size.X, h) / 2);
                    ImGui.SetCursorPos(center - (ImGuiHelper.CalcTextSize(text) / 2));
                    ImGui.PushStyleColor(ImGuiCol.Text, this.Container.Blocks[1].Color.Abgr());
                    ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(text));
                    ImGui.PopStyleColor();
                    ImGui.SetCursorPos(localPos);
                    text = this.Container.Blocks[0].Text;
                    ImGui.PushStyleColor(ImGuiCol.Text, this.Container.Blocks[0].Color.Abgr());
                    ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(text));
                    ImGui.PopStyleColor();
                }
            }

            ImGui.SetCursorPosY(localPos.Y + h);
        }
    }
}
