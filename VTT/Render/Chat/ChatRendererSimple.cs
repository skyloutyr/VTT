namespace VTT.Render.Chat
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System.Numerics;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public class ChatRendererSimple : ChatRendererBase
    {
        public ChatRendererSimple(ChatLine container) : base(container)
        {
        }

        public override void Cache(out float width, out float height)
        {
            width = 340;
            height = 88;
        }
        public override void ClearCache()
        {
        }

        public override void Render()
        {
            if (this.Container.Blocks.Count == 4)
            {
                ChatBlock rnameAndMod = this.Container.Blocks[0];
                ChatBlock rcharname = this.Container.Blocks[1];
                ChatBlock r1 = this.Container.Blocks[2];
                ChatBlock r2 = this.Container.Blocks[3];
                ImDrawListPtr drawList = ImGui.GetWindowDrawList();
                Vector2 cursorScreen = ImGui.GetCursorScreenPos();
                Vector2 cursorLocal = ImGui.GetCursorPos();
                drawList.AddQuad(
                    cursorScreen,
                    cursorScreen + new Vector2(340, 0),
                    cursorScreen + new Vector2(340, 78),
                    cursorScreen + new Vector2(0, 78),
                    this.Container.SenderColor.Abgr()
                );

                Texture tex = Client.Instance.Frontend.Renderer.GuiRenderer.ChatSimpleRollImage;
                drawList.AddImage(tex, cursorScreen + new Vector2(4, 4), cursorScreen + new Vector2(36, 36));

                float cXL = 340 / 4f;
                float cXR = cXL * 3;

                string tR1 = r1.Text;
                string tR2 = r2.Text;
                Vector2 sR1 = ImGuiHelper.CalcTextSize(tR1);
                Vector2 sR2 = ImGuiHelper.CalcTextSize(tR2);

                uint cell = Extensions.FromHex("202020").Abgr();
                uint cellOutline = Color.Gray.Abgr();

                drawList.AddQuadFilled(
                    cursorScreen + new Vector2(cXL, 24) - (new Vector2(sR1.X, sR1.Y) / 2) - new Vector2(4, 4),
                    cursorScreen + new Vector2(cXL, 24) - (new Vector2(-sR1.X, sR1.Y) / 2) - new Vector2(-4, 4),
                    cursorScreen + new Vector2(cXL, 24) - (new Vector2(-sR1.X, -sR1.Y) / 2) - new Vector2(-4, -4),
                    cursorScreen + new Vector2(cXL, 24) - (new Vector2(sR1.X, -sR1.Y) / 2) - new Vector2(4, -4),
                    cell
                );

                bool overRect = ImGui.IsMouseHoveringRect(cursorScreen + new Vector2(cXL, 24) - (new Vector2(sR1.X, sR1.Y) / 2) - new Vector2(4, 4), cursorScreen + new Vector2(cXL, 24) - (new Vector2(-sR1.X, -sR1.Y) / 2) - new Vector2(-4, -4));
                if (overRect)
                {
                    cellOutline = Color.DarkGoldenrod.Abgr();
                }

                drawList.AddQuad(
                    cursorScreen + new Vector2(cXL, 24) - (new Vector2(sR1.X, sR1.Y) / 2) - new Vector2(4, 4),
                    cursorScreen + new Vector2(cXL, 24) - (new Vector2(-sR1.X, sR1.Y) / 2) - new Vector2(-4, 4),
                    cursorScreen + new Vector2(cXL, 24) - (new Vector2(-sR1.X, -sR1.Y) / 2) - new Vector2(-4, -4),
                    cursorScreen + new Vector2(cXL, 24) - (new Vector2(sR1.X, -sR1.Y) / 2) - new Vector2(4, -4),
                    cellOutline
                );

                ImGui.SetCursorPos(cursorLocal + new Vector2(cXL, 24) - (sR1 / 2));
                ImGui.PushStyleColor(ImGuiCol.Text, r1.Color.Abgr());
                ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(tR1));
                ImGui.PopStyleColor();
                if (!string.IsNullOrEmpty(r1.Tooltip) && overRect)
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(r1.Tooltip));
                    ImGui.EndTooltip();
                }

                drawList.AddQuadFilled(
                    cursorScreen + new Vector2(cXR, 24) - (new Vector2(sR2.X, sR2.Y) / 2) - new Vector2(4, 4),
                    cursorScreen + new Vector2(cXR, 24) - (new Vector2(-sR2.X, sR2.Y) / 2) - new Vector2(-4, 4),
                    cursorScreen + new Vector2(cXR, 24) - (new Vector2(-sR2.X, -sR2.Y) / 2) - new Vector2(-4, -4),
                    cursorScreen + new Vector2(cXR, 24) - (new Vector2(sR2.X, -sR2.Y) / 2) - new Vector2(4, -4),
                    cell
                );

                overRect = ImGui.IsMouseHoveringRect(cursorScreen + new Vector2(cXR, 24) - (new Vector2(sR2.X, sR2.Y) / 2) - new Vector2(4, 4), cursorScreen + new Vector2(cXR, 24) - (new Vector2(-sR2.X, -sR2.Y) / 2) - new Vector2(-4, -4));
                cellOutline = Color.Gray.Abgr();
                if (overRect)
                {
                    cellOutline = Color.DarkGoldenrod.Abgr();
                }

                drawList.AddQuad(
                    cursorScreen + new Vector2(cXR, 24) - (new Vector2(sR2.X, sR2.Y) / 2) - new Vector2(4, 4),
                    cursorScreen + new Vector2(cXR, 24) - (new Vector2(-sR2.X, sR2.Y) / 2) - new Vector2(-4, 4),
                    cursorScreen + new Vector2(cXR, 24) - (new Vector2(-sR2.X, -sR2.Y) / 2) - new Vector2(-4, -4),
                    cursorScreen + new Vector2(cXR, 24) - (new Vector2(sR2.X, -sR2.Y) / 2) - new Vector2(4, -4),
                    cellOutline
                );

                ImGui.SetCursorPos(cursorLocal + new Vector2(cXR, 24) - (sR2 / 2));
                ImGui.PushStyleColor(ImGuiCol.Text, r2.Color.Abgr());
                ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(tR2));
                ImGui.PopStyleColor();
                if (!string.IsNullOrEmpty(r2.Tooltip) && overRect)
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(r2.Tooltip));
                    ImGui.EndTooltip();
                }

                Vector2 sRn = ImGuiHelper.CalcTextSize(rnameAndMod.Text);
                ImGui.SetCursorPos(cursorLocal + new Vector2(340 / 2f, 48) - (sRn / 2));
                ImGui.PushStyleColor(ImGuiCol.Text, rnameAndMod.Color.Abgr());
                ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(rnameAndMod.Text));
                ImGui.PopStyleColor();
                sRn = ImGuiHelper.CalcTextSize(rcharname.Text);
                ImGui.SetCursorPos(cursorLocal + new Vector2(340 / 2f, 60) - (sRn / 2));
                ImGui.PushStyleColor(ImGuiCol.Text, rcharname.Color.Abgr());
                ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(rcharname.Text));
                ImGui.PopStyleColor();
                ImGui.SetCursorPosY(cursorLocal.Y + 88);
            }
            else // Uh-oh, wrong format!
            {
                ImGui.TextColored((Vector4)Color.Red, "Incorrect message format!");
                string s = string.Empty;
                foreach (ChatBlock cb in this.Container.Blocks)
                {
                    s += cb.Text;
                }

                ImGui.TextWrapped(ImGuiHelper.TextOrEmpty(s));
            }
        }
    }
}
