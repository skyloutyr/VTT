namespace VTT.Render.Chat
{
    using ImGuiNET;
    using System;
    using System.Numerics;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public class ChatRendererSpell : ChatRendererBase
    {
        public ChatRendererSpell(ChatLine container) : base(container)
        {
        }

        public override void Cache(Vector2 windowSize, out float width, out float height)
        {
            width = 340;
            height = 188;
            if (this.Container.Blocks.Count == 13)
            {
                ChatBlock desc = this.Container.Blocks[12];
                Vector2 size = ImGui.GetWindowSize();
                float maxWindowW = MathF.Min(340, size.X - 24);
                Vector2 descs = ImGui.CalcTextSize(desc.Text, maxWindowW - 8);
                height += descs.Y;
            }
        }

        public override void ClearCache()
        {
        }

        public override string ProvideTextForClipboard(DateTime dateTime, string senderName, SimpleLanguage lang)
        {
            ChatBlock name = this.Container.Blocks[0];
            ChatBlock cname = this.Container.Blocks[2];
            ChatBlock desc = this.Container.Blocks[12];

            string cText = TextOrAlternative(cname.Text, lang.Translate("generic.character"));
            return $"{cText} {lang.Translate("generic.casts")} {TextOrAlternative(name.Text, lang.Translate("generic.spell"))}: {desc.Text}";
        }

        public override void Render(Guid senderId, uint senderColorAbgr)
        {
            Vector2 localPos = ImGui.GetCursorPos();
            Vector2 cursorScreen = ImGui.GetCursorScreenPos();
            Vector2 size = ImGui.GetWindowSize();
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            if (this.Container.Blocks.Count == 13)
            {
                ChatBlock name = this.Container.Blocks[0];
                ChatBlock schoollevel = this.Container.Blocks[1];
                ChatBlock cname = this.Container.Blocks[2];
                ChatBlock castingtime = this.Container.Blocks[3];
                ChatBlock range = this.Container.Blocks[4];
                ChatBlock target = this.Container.Blocks[5];
                ChatBlock duration = this.Container.Blocks[6];
                ChatBlock verbal = this.Container.Blocks[7];
                ChatBlock somatic = this.Container.Blocks[8];
                ChatBlock material = this.Container.Blocks[9];
                ChatBlock concentration = this.Container.Blocks[10];
                ChatBlock ritual = this.Container.Blocks[11];
                ChatBlock desc = this.Container.Blocks[12];
                SimpleLanguage lang = Client.Instance.Lang;

                float maxWindowW = MathF.Min(340, size.X - 24);
                Vector2 descs = ImGui.CalcTextSize(desc.Text, maxWindowW);
                float h = 184 + descs.Y;

                drawList.AddQuad(
                    cursorScreen,
                    cursorScreen + new Vector2(340, 0),
                    cursorScreen + new Vector2(340, h),
                    cursorScreen + new Vector2(0, h),
                    this.Container.SenderColor.Abgr()
                );

                Texture tex = Client.Instance.Frontend.Renderer.GuiRenderer.MagicIcon;
                drawList.AddImage(tex, cursorScreen + new Vector2(4, 4), cursorScreen + new Vector2(36, 36));
                drawList.AddText(cursorScreen + new Vector2(50, 12), name.Color.Abgr(), ImGuiHelper.TextOrEmpty(name.Text));
                Vector2 ts = ImGuiHelper.CalcTextSize(ImGuiHelper.TextOrEmpty(cname.Text));
                drawList.AddText(cursorScreen + new Vector2(332, 4) - new Vector2(ts.X, 0), cname.Color.Abgr(), ImGuiHelper.TextOrEmpty(cname.Text));
                drawList.AddText(cursorScreen + new Vector2(4, 32), schoollevel.Color.Abgr(), ImGuiHelper.TextOrEmpty(schoollevel.Text));
                uint bcl = this.Container.SenderColor.Abgr();
                uint fcl = ImGui.GetColorU32(ImGuiCol.Text);
                float maxW = MathF.Max(
                    ImGuiHelper.CalcTextSize(lang.Translate("chat.spell.components")).X,
                    MathF.Max(ImGuiHelper.CalcTextSize(lang.Translate("chat.spell.cast_time")).X,
                    MathF.Max(ImGuiHelper.CalcTextSize(lang.Translate("chat.spell.duration")).X,
                    MathF.Max(ImGuiHelper.CalcTextSize(lang.Translate("chat.spell.range")).X,
                    ImGuiHelper.CalcTextSize(lang.Translate("chat.spell.target")).X)))
                ) + 8;

                string unspecified = lang.Translate("chat.spell.unspecified");
                string TextOrDefault(string s) => s.Trim().Length == 0 ? unspecified : s;

                drawList.AddLine(cursorScreen + new Vector2(0, 52), cursorScreen + new Vector2(340, 52), bcl);
                drawList.AddText(cursorScreen + new Vector2(4, 52), fcl, lang.Translate("chat.spell.components"));
                drawList.AddLine(cursorScreen + new Vector2(0, 74), cursorScreen + new Vector2(340, 74), bcl);
                drawList.AddText(cursorScreen + new Vector2(4, 74), fcl, lang.Translate("chat.spell.cast_time"));
                drawList.AddLine(cursorScreen + new Vector2(0, 96), cursorScreen + new Vector2(340, 96), bcl);
                drawList.AddText(cursorScreen + new Vector2(4, 96), fcl, lang.Translate("chat.spell.duration"));
                drawList.AddLine(cursorScreen + new Vector2(0, 118), cursorScreen + new Vector2(340, 118), bcl);
                drawList.AddText(cursorScreen + new Vector2(4, 118), fcl, lang.Translate("chat.spell.range"));
                drawList.AddLine(cursorScreen + new Vector2(0, 140), cursorScreen + new Vector2(340, 140), bcl);
                drawList.AddText(cursorScreen + new Vector2(4, 140), fcl, lang.Translate("chat.spell.target"));
                drawList.AddLine(cursorScreen + new Vector2(0, 162), cursorScreen + new Vector2(340, 162), bcl);
                drawList.AddLine(cursorScreen + new Vector2(maxW, 52), cursorScreen + new Vector2(maxW, 162), bcl);
                drawList.AddText(cursorScreen + new Vector2(maxW + 4, 74), castingtime.Color.Abgr(), TextOrDefault(castingtime.Text));
                drawList.AddText(cursorScreen + new Vector2(maxW + 4, 96), duration.Color.Abgr(), TextOrDefault(duration.Text));
                drawList.AddText(cursorScreen + new Vector2(maxW + 4, 118), range.Color.Abgr(), TextOrDefault(range.Text));
                drawList.AddText(cursorScreen + new Vector2(maxW + 4, 140), target.Color.Abgr(), TextOrDefault(target.Text));

                float cx = maxW + 4;
                if (int.TryParse(verbal.Text, out int i) && i == 1)
                {
                    tex = Client.Instance.Frontend.Renderer.GuiRenderer.VerbalComponentIcon;
                    drawList.AddImage(tex, cursorScreen + new Vector2(cx, 52), cursorScreen + new Vector2(cx + 20, 72));
                    drawList.AddText(cursorScreen + new Vector2(cx + 12, 58), fcl, "V");
                    if (ImGui.IsMouseHoveringRect(cursorScreen + new Vector2(cx, 52), cursorScreen + new Vector2(cx + 20, 72)))
                    {
                        ImGui.SetTooltip(lang.Translate("chat.spell.verbal"));
                    }

                    cx += 22;
                }

                if (int.TryParse(somatic.Text, out i) && i == 1)
                {
                    tex = Client.Instance.Frontend.Renderer.GuiRenderer.SomaticComponentIcon;
                    drawList.AddImage(tex, cursorScreen + new Vector2(cx, 52), cursorScreen + new Vector2(cx + 20, 72));
                    drawList.AddText(cursorScreen + new Vector2(cx + 12, 58), fcl, "S");
                    if (ImGui.IsMouseHoveringRect(cursorScreen + new Vector2(cx, 52), cursorScreen + new Vector2(cx + 20, 72)))
                    {
                        ImGui.SetTooltip(lang.Translate("chat.spell.somatic"));
                    }

                    cx += 22;
                }

                if (int.TryParse(material.Text, out i) && i == 1)
                {
                    tex = Client.Instance.Frontend.Renderer.GuiRenderer.MaterialComponentIcon;
                    drawList.AddImage(tex, cursorScreen + new Vector2(cx, 52), cursorScreen + new Vector2(cx + 20, 72));
                    drawList.AddText(cursorScreen + new Vector2(cx + 12, 58), fcl, "M");
                    if (ImGui.IsMouseHoveringRect(cursorScreen + new Vector2(cx, 52), cursorScreen + new Vector2(cx + 20, 72)))
                    {
                        ImGui.SetTooltip(lang.Translate("chat.spell.material"));
                    }

                    cx += 22;
                }

                if (int.TryParse(concentration.Text, out i) && i == 1)
                {
                    tex = Client.Instance.Frontend.Renderer.GuiRenderer.ConcentrationComponentIcon;
                    drawList.AddImage(tex, cursorScreen + new Vector2(cx, 52), cursorScreen + new Vector2(cx + 20, 72));
                    drawList.AddText(cursorScreen + new Vector2(cx + 12, 58), fcl, "C");
                    if (ImGui.IsMouseHoveringRect(cursorScreen + new Vector2(cx, 52), cursorScreen + new Vector2(cx + 20, 72)))
                    {
                        ImGui.SetTooltip(lang.Translate("chat.spell.concentration"));
                    }

                    cx += 22;
                }

                if (int.TryParse(ritual.Text, out i) && i == 1)
                {
                    tex = Client.Instance.Frontend.Renderer.GuiRenderer.RitualComponentIcon;
                    drawList.AddImage(tex, cursorScreen + new Vector2(cx, 52), cursorScreen + new Vector2(cx + 20, 72));
                    drawList.AddText(cursorScreen + new Vector2(cx + 12, 58), fcl, "R");
                    if (ImGui.IsMouseHoveringRect(cursorScreen + new Vector2(cx, 52), cursorScreen + new Vector2(cx + 20, 72)))
                    {
                        ImGui.SetTooltip(lang.Translate("chat.spell.ritual"));
                    }
                }

                ImGui.SetCursorPos(localPos + new Vector2(8, 162));
                ImGui.PushTextWrapPos(maxWindowW);
                ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(desc.Text));
                ImGui.PopTextWrapPos();
                ImGui.SetCursorPosY(localPos.Y + h);
            }
            else
            {
                ImGui.SetCursorPos(localPos + new Vector2(0, 188));
            }
        }
    }
}
