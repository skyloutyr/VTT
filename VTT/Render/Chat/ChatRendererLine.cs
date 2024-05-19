namespace VTT.Render.Chat
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Text;
    using VTT.Control;
    using VTT.Util;

    public class ChatRendererLine : ChatRendererBase
    {
        internal ImCachedLine[] _cachedLines;
        private float _spacebarWidth;
        private float _spacebarHeight;
        private float _w;
        private float _h;

        public ChatRendererLine(ChatLine container) : base(container)
        {
        }

        public override void Cache(Vector2 windowSize, out float width, out float height)
        {
            float maxX = windowSize.X - 24;
            // float startX = ImGui.GetCursorPosX();
            StringBuilder sb = new StringBuilder();
            List<ImCachedWord> words = new List<ImCachedWord>();
            List<ImCachedLine> lines = new List<ImCachedLine>();
            float wMax = 0;
            float cW = 0;
            Vector2 ssize = ImGuiHelper.CalcTextSize(" ");
            this._spacebarWidth = ssize.X;
            this._spacebarHeight = ssize.Y;

            void AddWord(string text, ChatBlock cb)
            {
                bool addedWord = false;
                ImCachedWord icw = new ImCachedWord(cb, text);
                cW += icw.Width;
                if (cW > maxX)
                {
                    if (words.Count == 0) // uh-oh
                    {
                        words.Add(icw);
                        addedWord = true;
                    }

                    ImCachedLine icl = new ImCachedLine(words.ToArray());
                    lines.Add(icl);
                    words.Clear();
                    cW = icw.Width;
                }
                else
                {
                    wMax = MathF.Max(wMax, cW);
                }

                if (!addedWord)
                {
                    words.Add(icw);
                    cW += _spacebarWidth;
                }
            }

            foreach (ChatBlock cb in this.Container.Blocks)
            {
                string text = cb.Text;
                foreach (char c in text)
                {
                    if (c == ' ')
                    {
                        if (sb.Length > 0)
                        {
                            AddWord(sb.ToString(), cb);
                            sb.Clear();
                        }

                        continue;
                    }

                    if (c == '\n')
                    {
                        if (sb.Length > 0)
                        {
                            AddWord(sb.ToString(), cb);
                            sb.Clear();
                        }

                        ImCachedLine icl = new ImCachedLine(words.ToArray());
                        lines.Add(icl);
                        words.Clear();
                        continue;
                    }

                    sb.Append(c);
                }

                if (sb.Length > 0)
                {
                    AddWord(sb.ToString(), cb);
                    sb.Clear();
                }
            }

            if (words.Count > 0)
            {
                ImCachedLine icl2 = new ImCachedLine(words.ToArray());
                lines.Add(icl2);
                words.Clear();
            }

            this._cachedLines = lines.ToArray();
            height = this._h = MathF.Max(lines.Sum(l => l.Height), ssize.Y);
            width = this._w = wMax;
        }

        public override void ClearCache() => this._cachedLines = null;
        public override string ProvideTextForClipboard(DateTime dateTime, string senderName, SimpleLanguage lang)
        {
            string ret = string.Empty;

            foreach (ImCachedLine icl in this._cachedLines)
            {
                for (int i = 0; i < icl.Words.Length; i++)
                {
                    ImCachedWord icw = icl.Words[i];
                    ret += icw.Text;
                    if (icw.IsExpression)
                    {
                        ret += $"({RollSyntaxRegex.Replace(icw.Owner.Tooltip, x => $"{x.Groups[1].Value}d{x.Groups[2].Value}[")})";
                    }

                    ret += " ";
                }
            }

            return ret;
        }

        public override void Render()
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            uint cell = Extensions.FromHex("202020").Abgr();
            uint cellOutline;
            float ipX = ImGui.GetCursorPosX();

            if (this._cachedLines.Length == 0)
            {
                ImGui.SetCursorPos(new(ipX, ImGui.GetCursorPosY() + this._spacebarHeight));
            }
            else
            {
                foreach (ImCachedLine icl in this._cachedLines)
                {
                    float pX = ImGui.GetCursorPosX();
                    float pY = ImGui.GetCursorPosY();
                    for (int i = 0; i < icl.Words.Length; i++)
                    {
                        ImCachedWord icw = icl.Words[i];
                        Vector2 vBase = ImGui.GetCursorScreenPos();
                        if (icw.IsExpression)
                        {
                            ImGui.SetCursorPosX(pX + 4);
                            float cX = vBase.X;
                            float cY = vBase.Y;
                            float w = icw.Width;
                            float h = icw.Height;
                            bool overRect = ImGui.IsMouseHoveringRect(new(cX, cY), new(cX + w, cY + h));
                            cellOutline = overRect ? Color.DarkGoldenrod.Abgr() : Color.Gray.Abgr();
                            drawList.AddQuadFilled(
                                new(cX, cY),
                                new(cX + w, cY),
                                new(cX + w, cY + h),
                                new(cX, cY + h),
                                cell
                            );

                            drawList.AddQuad(
                                new(cX, cY),
                                new(cX + w, cY),
                                new(cX + w, cY + h),
                                new(cX, cY + h),
                                cellOutline
                            );
                        }

                        ImGui.PushStyleColor(ImGuiCol.Text, icw.Owner.Color.Abgr());
                        ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(icw.Text));
                        ImGui.PopStyleColor();
                        pX += icw.Width + (i == icl.Words.Length - 1 ? 0 : this._spacebarWidth);
                        ImGui.SetCursorPos(new(pX, pY));
                        Vector2 vEnd = ImGui.GetCursorScreenPos() + new Vector2(0, icl.Height);
                        if (!string.IsNullOrEmpty(icw.Owner.Tooltip) && ImGui.IsMouseHoveringRect(vBase, vEnd))
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(icw.Owner.Tooltip));
                            ImGui.EndTooltip();
                        }
                    }

                    pY += icl.Height;
                    ImGui.SetCursorPos(new(ipX, pY));
                }
            }
        }
    }
}
