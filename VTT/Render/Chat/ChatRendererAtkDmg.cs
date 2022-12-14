namespace VTT.Render.Chat
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System.Numerics;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public class ChatRendererAtkDmg : ChatRendererBase
    {
        public ChatRendererAtkDmg(ChatLine container) : base(container)
        {
        }

        public override void Cache(out float width, out float height)
        {
            width = 340;
            bool hasRolls = this.Container.Blocks.Count == 11 && !string.IsNullOrEmpty(this.Container.Blocks[4].Text);
            bool hasDmg = this.Container.Blocks.Count == 11 && !string.IsNullOrEmpty(this.Container.Blocks[6].Text.Trim());
            height = hasRolls && hasDmg ? 172 : 172 - 48;
        }
        public override void ClearCache()
        {
        }

        public override void Render()
        {
            if (this.Container.Blocks.Count == 11)
            {
                ImDrawListPtr drawList = ImGui.GetWindowDrawList();
                Vector2 cursorLocal = ImGui.GetCursorPos();
                Vector2 cursorScreen = ImGui.GetCursorScreenPos();
                ChatBlock rname = this.Container.Blocks[0];
                ChatBlock mod = this.Container.Blocks[1];
                ChatBlock rcharname = this.Container.Blocks[2];
                ChatBlock desc = this.Container.Blocks[3];

                ChatBlock r1 = this.Container.Blocks[4];
                ChatBlock r2 = this.Container.Blocks[5];
                ChatBlock dmg1 = this.Container.Blocks[6];
                ChatBlock dmg2 = this.Container.Blocks[7];
                ChatBlock crit1 = this.Container.Blocks[8];
                ChatBlock crit2 = this.Container.Blocks[9];
                ChatBlock dmgtype = this.Container.Blocks[10];

                bool hasRolls = !string.IsNullOrEmpty(r1.Text.Trim());
                bool hasDmg = !string.IsNullOrEmpty(dmg1.Text.Trim());

                if (!hasRolls && !hasDmg)
                {
                    drawList.AddQuad(
                        cursorScreen,
                        cursorScreen + new Vector2(340, 0),
                        cursorScreen + new Vector2(340, 56),
                        cursorScreen + new Vector2(0, 56),
                        this.Container.SenderColor.Abgr()
                    );

                    string rnameandmod1 = $"{rname.Text}";
                    if (!string.IsNullOrEmpty(mod.Text.Trim()))
                    {
                        rnameandmod1 += $" ({mod.Text})";
                    }

                    Vector2 ts1 = ImGui.CalcTextSize(rnameandmod1);
                    drawList.AddText(cursorScreen + new Vector2(170, 4) - new Vector2(ts1.X / 2, 0), rname.Color.Abgr(), rnameandmod1);
                    ts1 = ImGui.CalcTextSize(desc.Text);
                    drawList.AddText(cursorScreen + new Vector2(170, 20) - new Vector2(ts1.X / 2, 0), desc.Color.Abgr(), desc.Text);
                    ts1 = ImGui.CalcTextSize(rcharname.Text);
                    drawList.AddText(cursorScreen + new Vector2(170, 36) - new Vector2(ts1.X / 2, 0), rcharname.Color.Abgr(), rcharname.Text);

                    ImGui.SetCursorPosY(cursorLocal.Y + 56);
                    return;
                }

                float cy = hasRolls ? 0 : -48;
                drawList.AddQuad(
                    cursorScreen,
                    cursorScreen + new Vector2(340, 0),
                    cursorScreen + new Vector2(340, 172 + cy),
                    cursorScreen + new Vector2(0, 172 + cy),
                    this.Container.SenderColor.Abgr()
                );

                Texture tex = Client.Instance.Frontend.Renderer.GuiRenderer.CrossedSwordsIcon;
                drawList.AddImage(tex, cursorScreen + new Vector2(4, 4), cursorScreen + new Vector2(36, 36));
                float cx = 340 * 0.25f;
                if (hasRolls)
                {
                    this.RenderTooltipBlock(drawList, cursorScreen + new Vector2(cx, 24), r1.Text, r1.Tooltip, r1.Color);
                    this.RenderTooltipBlock(drawList, cursorScreen + new Vector2(cx * 3, 24), r2.Text, r2.Tooltip, r2.Color);
                    drawList.AddLine(cursorScreen + new Vector2(170, 0), cursorScreen + new Vector2(170, 48), this.Container.SenderColor.Abgr());
                    drawList.AddLine(cursorScreen + new Vector2(0, 48), cursorScreen + new Vector2(340, 48), this.Container.SenderColor.Abgr());
                }

                string rnameandmod = $"{rname.Text}";
                if (!string.IsNullOrEmpty(mod.Text.Trim()))
                {
                    rnameandmod += $" ({mod.Text})";
                }

                Vector2 ts = ImGui.CalcTextSize(rnameandmod);
                drawList.AddText(cursorScreen + new Vector2(170, 56 + cy) - new Vector2(ts.X / 2, 0), rname.Color.Abgr(), rnameandmod);
                ts = ImGui.CalcTextSize(desc.Text);
                drawList.AddText(cursorScreen + new Vector2(170, 72 + cy) - new Vector2(ts.X / 2, 0), desc.Color.Abgr(), desc.Text);
                ts = ImGui.CalcTextSize(rcharname.Text);
                drawList.AddText(cursorScreen + new Vector2(170, 96 + cy) - new Vector2(ts.X / 2, 0), rcharname.Color.Abgr(), rcharname.Text);
                drawList.AddLine(cursorScreen + new Vector2(0, 120 + cy), cursorScreen + new Vector2(340, 120 + cy), this.Container.SenderColor.Abgr());
                uint cArgb = Color.LightGreen.Argb();
                uint cArgb2 = Color.LightBlue.Argb();

                bool crit = r1.Color.Argb() == cArgb || r1.Color.Argb() == cArgb2 || r2.Color.Argb() == cArgb || r2.Color.Argb() == cArgb2;
                bool hasdmg2 = !string.IsNullOrEmpty(dmg2.Text) && !string.Equals(dmg2.Text, "null");

                if (hasdmg2)
                {
                    float dmgSize = ImGui.CalcTextSize(dmg1.Text).X;
                    float ots;
                    ots = dmgSize + (crit ? 44 + ImGui.CalcTextSize(crit1.Text).X : 0);
                    ots *= 0.5f;
                    ots -= dmgSize * 0.5f;
                    this.RenderTooltipBlock(drawList, cursorScreen + new Vector2(cx - ots, 140 + cy), dmg1.Text, dmg1.Tooltip, dmg1.Color);
                    if (crit)
                    {
                        ts = ImGui.CalcTextSize(dmg1.Text);
                        drawList.AddText(cursorScreen + new Vector2(cx + dmgSize + 8 - ots, 140 - (ts.Y * 0.5f) - 2 + cy), ImGui.GetColorU32(ImGuiCol.Text), "+");
                        this.RenderTooltipBlock(drawList, cursorScreen + new Vector2(cx + ts.X + 36 - ots, 140 + cy), crit1.Text, crit1.Tooltip, crit1.Color);
                    }

                    ts = ImGui.CalcTextSize(dmgtype.Text);
                    drawList.AddText(cursorScreen + new Vector2(cx - (ts.X * 0.5f), 156 + cy), dmgtype.Color.Abgr(), dmgtype.Text);

                    dmgSize = ImGui.CalcTextSize(dmg2.Text).X;
                    ots = dmgSize + (crit ? 44 + ImGui.CalcTextSize(crit2.Text).X : 0);
                    ots *= 0.5f;
                    ots -= dmgSize * 0.5f;
                    this.RenderTooltipBlock(drawList, cursorScreen + new Vector2((cx * 3) + 8 - ots, 140 + cy), dmg2.Text, dmg2.Tooltip, dmg2.Color);
                    if (crit)
                    {
                        ts = ImGui.CalcTextSize(dmg2.Text);
                        drawList.AddText(cursorScreen + new Vector2((cx * 3) + dmgSize + 8 - ots, 140 - (ts.Y * 0.5f) - 2 + cy), ImGui.GetColorU32(ImGuiCol.Text), "+");
                        this.RenderTooltipBlock(drawList, cursorScreen + new Vector2((cx * 3) + ts.X + 44 - ots, 140 + cy), crit2.Text, crit2.Tooltip, crit2.Color);
                    }

                    ts = ImGui.CalcTextSize(dmgtype.Text);
                    drawList.AddText(cursorScreen + new Vector2((cx * 3) - (ts.X * 0.5f), 156 + cy), dmgtype.Color.Abgr(), dmgtype.Text);
                }
                else
                {
                    float dmgSize = ImGui.CalcTextSize(dmg1.Text).X;
                    float ots;
                    ots = dmgSize + (crit ? 44 + ImGui.CalcTextSize(crit1.Text).X : 0);
                    ots *= 0.5f;
                    ots -= dmgSize * 0.5f;
                    this.RenderTooltipBlock(drawList, cursorScreen + new Vector2((cx * 2) - ots, 140 + cy), dmg1.Text, dmg1.Tooltip, dmg1.Color);
                    if (crit)
                    {
                        ts = ImGui.CalcTextSize(dmg1.Text);
                        drawList.AddText(cursorScreen + new Vector2((cx * 2) + dmgSize + 8 - ots, 140 - (ts.Y * 0.5f) - 2 + cy), ImGui.GetColorU32(ImGuiCol.Text), "+");
                        this.RenderTooltipBlock(drawList, cursorScreen + new Vector2((cx * 2) + ts.X + 36 - ots, 140 + cy), crit1.Text, crit1.Tooltip, crit1.Color);
                    }

                    ts = ImGui.CalcTextSize(dmgtype.Text);
                    drawList.AddText(cursorScreen + new Vector2((cx * 2) - (ts.X * 0.5f), 156 + cy), dmgtype.Color.Abgr(), dmgtype.Text);
                }

                ImGui.SetCursorPosY(cursorLocal.Y + 172 + cy);
            }
            else // Uh-oh, wrong format!
            {
                ImGui.TextColored((Vector4)Color.Red, "Incorrect message format!");
                string s = string.Empty;
                foreach (ChatBlock cb in this.Container.Blocks)
                {
                    s += cb.Text;
                }

                ImGui.TextWrapped(s);
            }
        }

        public void RenderTooltipBlock(ImDrawListPtr drawList, Vector2 cursorScreen, string text, string tt, Color clr)
        {
            Vector2 tSize = ImGui.CalcTextSize(text);
            uint cell = Extensions.FromHex("202020").Abgr();
            uint cellOutline = Color.Gray.Abgr();
            float w = tSize.X + 8;
            float h = tSize.Y + 8;
            w *= 0.5f;
            h *= 0.5f;
            drawList.AddQuadFilled(
                cursorScreen - new Vector2(w, h),
                cursorScreen - new Vector2(-w, h),
                cursorScreen - new Vector2(-w, -h),
                cursorScreen - new Vector2(w, -h),
                cell
            );

            bool hover = ImGui.IsMouseHoveringRect(cursorScreen + new Vector2(-w, -h), cursorScreen + new Vector2(w, h));
            if (hover)
            {
                cellOutline = Color.DarkGoldenrod.Abgr();
            }

            drawList.AddQuad(
                cursorScreen - new Vector2(w, h),
                cursorScreen - new Vector2(-w, h),
                cursorScreen - new Vector2(-w, -h),
                cursorScreen - new Vector2(w, -h),
                cellOutline
            );

            drawList.AddText(cursorScreen - new Vector2(w - 4, h - 4), clr.Abgr(), text);

            if (hover && !string.IsNullOrEmpty(tt))
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(tt);
                ImGui.EndTooltip();
            }
        }
    }
}
