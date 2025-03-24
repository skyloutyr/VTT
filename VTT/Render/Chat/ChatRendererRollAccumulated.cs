namespace VTT.Render.Chat
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Numerics;
    using VTT.Control;
    using VTT.Util;

    public class ChatRendererRollAccumulated : ChatRendererBase
    {
        public ChatRendererRollAccumulated(ChatLine container) : base(container)
        {
        }

        public override void Cache(Vector2 windowSize, out float width, out float height)
        {
            width = 100;
            height = 32;
        }

        public override void ClearCache() // No cache
        {
        }

        public override string ProvideTextForClipboard(DateTime dateTime, string senderName, SimpleLanguage lang)
        {
            return this.Container.Blocks.Count == 0
                ? string.Empty
                : $"{this.Container.Blocks[0].Text}({RollSyntaxRegex.Replace(this.Container.Blocks[0].Tooltip, x => $"{x.Groups[1].Value}d{x.Groups[2].Value}[")})";
        }

        public override void Render(Guid senderId, uint senderColorAbgr)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            uint outline = Color.Gray.Abgr();
            uint color = this.Container.GetBlockColorOr(0, Color.Black).Abgr();

            float cX = ImGui.GetCursorScreenPos().X;
            float w = 100;
            float cY = ImGui.GetCursorScreenPos().Y;

            drawList.AddLine(new(cX + w - 94, cY), new(cX + w, cY), outline);
            drawList.AddLine(new(cX + w, cY), new(cX + w, cY + 28), outline);

            drawList.AddTriangleFilled(new(cX + w - 101, cY + 1), new(cX + w - 1, cY + 1), new(cX + w - 1, cY + 3), color);
            drawList.AddTriangleFilled(new(cX + w, cY + 1), new(cX + w, cY + 24), new(cX + w - 2, cY + 1), color);

            drawList.AddLine(new(cX + 94, cY + 32), new(cX, cY + 32), outline);
            drawList.AddLine(new(cX, cY + 6), new(cX, cY + 31), outline);

            drawList.AddTriangleFilled(new(cX + 1, cY + 30), new(cX + 101, cY + 32), new(cX + 1, cY + 32), color);
            drawList.AddTriangleFilled(new(cX + 2, cY + 31), new(cX + 1.5f, cY + 16), new(cX + 2, cY + 31), color);

            Vector2 padding = ImGui.GetStyle().CellPadding;

            if (this.Container.TryGetBlockAt(0, out ChatBlock blockRolls))
            {
                Vector2 tSize = ImGuiHelper.CalcTextSize(blockRolls.Text);
                Vector2 tLen = tSize + (padding * 2);
                float tW = Math.Max(24, tLen.X);
                float tH = Math.Max(24, tLen.Y);
                this.AddTooltipBlock(drawList, new RectangleF(cX + (w * 0.5f) - (tW * 0.5f), cY + 16 - (tH * 0.5f), tW, tH), blockRolls.Text, tSize, blockRolls.Tooltip, blockRolls.RollContents, blockRolls.Color.Abgr(), senderColorAbgr);
            }

            ImGui.Dummy(new Vector2(32, 32));
        }
    }
}
