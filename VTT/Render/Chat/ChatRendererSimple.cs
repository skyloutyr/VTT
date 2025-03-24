namespace VTT.Render.Chat
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Numerics;
    using VTT.Control;
    using VTT.Network;
    using VTT.Render.Gui;
    using VTT.Util;

    public class ChatRendererSimple : ChatRendererBase
    {
        public ChatRendererSimple(ChatLine container) : base(container)
        {
        }

        public override void Cache(Vector2 windowSize, out float width, out float height)
        {
            width = 340;
            height = 88;
        }
        public override void ClearCache()
        {
        }

        public override string ProvideTextForClipboard(DateTime dateTime, string senderName, SimpleLanguage lang)
        {
            ChatBlock rnameAndMod = this.Container.Blocks[0];
            ChatBlock rcharname = this.Container.Blocks[1];
            ChatBlock r1 = this.Container.Blocks[2];
            ChatBlock r2 = this.Container.Blocks[3];

            bool hasRolls = !string.IsNullOrEmpty(r1.Text.Trim());
            uint cArgb = Color.LightGreen.Argb();
            uint cArgb2 = Color.LightBlue.Argb();

            static string removeCLRF(string s) => s.Replace("\n", "").Replace("\r", "");

            string rText = hasRolls ? $"{removeCLRF(r1.Text)}({r1.Tooltip}) {lang.Translate("generic.or")} {removeCLRF(r2.Text)}({r2.Tooltip})," : string.Empty;
            rText = RollSyntaxRegex.Replace(rText, x => $"{x.Groups[1].Value}d{x.Groups[2].Value}[");
            string rollText = $"{TextOrAlternative(rcharname.Text, lang.Translate("generic.character"))} {lang.Translate("generic.rolls")} {rText} ({TextOrAlternative(rnameAndMod.Text, lang.Translate("generic.unknown"))})";
            return rollText;
        }

        public override void Render(Guid senderId, uint senderColorAbgr)
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

                GuiRenderer uiRoot = Client.Instance.Frontend.Renderer.GuiRenderer;

                // Not a bug - color intentionally set to sender's!
                drawList.AddImage(uiRoot.DiceIconAtlas, cursorScreen + new Vector2(4, 4), cursorScreen + new Vector2(36, 36), uiRoot.ChatIconD20.BoundsPrimaryStart, uiRoot.ChatIconD20.BoundsPrimaryEnd, senderColorAbgr);
                drawList.AddImage(uiRoot.DiceIconAtlas, cursorScreen + new Vector2(4, 4), cursorScreen + new Vector2(36, 36), uiRoot.ChatIconD20.BoundsSecondaryStart, uiRoot.ChatIconD20.BoundsSecondaryEnd, senderColorAbgr);

                float cXL = 340 / 4f;
                float cXR = cXL * 3;

                string tR1 = r1.Text;
                string tR2 = r2.Text;
                Vector2 sR1 = ImGuiHelper.CalcTextSize(tR1);
                Vector2 sR2 = ImGuiHelper.CalcTextSize(tR2);

                this.AddTooltipBlock(drawList, new RectangleF(cursorScreen.X + cXL - 4 - sR1.X / 2, cursorScreen.Y + 24 - 4 - sR1.Y / 2, sR1.X + 8, sR1.Y + 8), tR1, sR1, r1.Tooltip, r1.RollContents, r1.Color.Abgr(), senderColorAbgr);
                ImGui.Dummy(new Vector2(sR1.X + 8, sR1.Y + 8));
                this.AddTooltipBlock(drawList, new RectangleF(cursorScreen.X + cXR - 4 - sR2.X / 2, cursorScreen.Y + 24 - 4 - sR2.Y / 2, sR2.X + 8, sR2.Y + 8), tR2, sR2, r2.Tooltip, r2.RollContents, r2.Color.Abgr(), senderColorAbgr);
                ImGui.Dummy(new Vector2(sR2.X + 8, sR2.Y + 8));

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
