namespace VTT.Render.Chat
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using VTT.Control;
    using VTT.Util;

    public class ChatRendererRollAccumulated : ChatRendererBase
    {
        public ChatRendererRollAccumulated(ChatLine container) : base(container)
        {
        }

        public override void Cache(out float width, out float height)
        {
            width = 100;
            height = 32;
        }

        public override void ClearCache() // No cache
        {
        }

        public override void Render()
        {
            if (this.Container.Blocks.Count == 0)
            {
                return;
            }

            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            uint outline = Color.Gray.Abgr();
            uint color = this.Container.Blocks[0].Color.Abgr();
            uint cell = Extensions.FromHex("202020").Abgr();
            uint cellOutline = Color.Gray.Abgr();

            float cX = ImGui.GetCursorScreenPos().X;
            float w = 100;
            float cY = ImGui.GetCursorScreenPos().Y;
            float ccY = ImGui.GetCursorPosY();

            drawList.AddLine(new(cX + w - 94, cY), new(cX + w, cY), outline);
            drawList.AddLine(new(cX + w, cY), new(cX + w, cY + 28), outline);

            drawList.AddTriangleFilled(new(cX + w - 101, cY + 1), new(cX + w - 1, cY + 1), new(cX + w - 1, cY + 3), color);
            drawList.AddTriangleFilled(new(cX + w, cY + 1), new(cX + w, cY + 24), new(cX + w - 2, cY + 1), color);


            drawList.AddLine(new(cX + 94, cY + 32), new(cX, cY + 32), outline);
            drawList.AddLine(new(cX, cY + 6), new(cX, cY + 31), outline);

            drawList.AddTriangleFilled(new(cX + 1, cY + 30), new(cX + 101, cY + 32), new(cX + 1, cY + 32), color);
            drawList.AddTriangleFilled(new(cX + 2, cY + 31), new(cX + 1.5f, cY + 16), new(cX + 2, cY + 31), color);

            System.Numerics.Vector2 padding = ImGui.GetStyle().CellPadding;
            System.Numerics.Vector2 tLen = ImGuiHelper.CalcTextSize(this.Container.Blocks[0].Text) + (padding * 2);

            bool overRect = ImGui.IsMouseHoveringRect(new(cX + (w / 2) - (tLen.X / 2), cY + 16 - (tLen.Y / 2)), new(cX + (w / 2) + (tLen.X / 2), cY + 16 + (tLen.Y / 2)));
            if (overRect)
            {
                cellOutline = Color.DarkGoldenrod.Abgr();
            }

            drawList.AddQuadFilled(
                new(cX + (w / 2) - (tLen.X / 2), cY + 16 - (tLen.Y / 2)),
                new(cX + (w / 2) + (tLen.X / 2), cY + 16 - (tLen.Y / 2)),
                new(cX + (w / 2) + (tLen.X / 2), cY + 16 + (tLen.Y / 2)),
                new(cX + (w / 2) - (tLen.X / 2), cY + 16 + (tLen.Y / 2)),
                cell
            );

            drawList.AddQuad(
               new(cX + (w / 2) - (tLen.X / 2), cY + 16 - (tLen.Y / 2)),
               new(cX + (w / 2) + (tLen.X / 2), cY + 16 - (tLen.Y / 2)),
               new(cX + (w / 2) + (tLen.X / 2), cY + 16 + (tLen.Y / 2)),
               new(cX + (w / 2) - (tLen.X / 2), cY + 16 + (tLen.Y / 2)),
               cellOutline
           );

            ImGui.SetCursorPos(new(ImGui.GetCursorPosX() + (w / 2) - (tLen.X / 2) + padding.X + 1, ccY + 16 - (tLen.Y / 2) + padding.Y));
            ImGui.PushStyleColor(ImGuiCol.Text, this.Container.Blocks[0].Color.Abgr());
            ImGui.TextUnformatted(this.Container.Blocks[0].Text);
            ImGui.PopStyleColor();
            if (!string.IsNullOrEmpty(this.Container.Blocks[0].Tooltip) && overRect)
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(this.Container.Blocks[0].Tooltip);
                ImGui.EndTooltip();
            }

            ImGui.SetCursorPosX(0);
            ImGui.SetCursorPosY(ccY + 32);
        }
    }
}
