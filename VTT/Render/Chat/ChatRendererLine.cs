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
    using VTT.Network;
    using VTT.Util;

    public class ChatRendererLine : ChatRendererBase
    {
        internal ImCachedLine[] _cachedLines;
        private float _spacebarWidth;
        private float _spacebarHeight;

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
                ImCachedWord icw = new ImCachedWord(cb, text, cb.Type.HasFlag(ChatBlockType.Expression) ? 24 : 0);
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
            height = MathF.Max(lines.Sum(l => l.Height + 2), ssize.Y);
            width = wMax;
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

        // Note the optimization here is to reduce draw calls by reducing the amount of texture switches
        // The reason this approach is used rather than ImGui's own AddCustomRectXXXX is due to ImGui's #8465 - API deprecated
        // So no point in working with it until the new API releases
        private readonly static NonClearingArrayList<(Vector2, uint, string, bool)> textDrawList = new NonClearingArrayList<(Vector2, uint, string, bool)>();
        public override void Render(Guid senderId, uint senderColorAbgr)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            float ipX = ImGui.GetCursorPosX();

            if (this._cachedLines.Length == 0)
            {
                ImGui.SetCursorPos(new(ipX, ImGui.GetCursorPosY() + this._spacebarHeight));
            }
            else
            {
                textDrawList.Clear();
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
                            this.AddTooltipBlock(drawList, new RectangleF(cX, cY - 3, w, h), icw.Text, icw.TextSize, icw.Owner.Tooltip, icw.Owner.RollContents, icw.Owner.Color.Abgr(), senderColorAbgr, Client.Instance.Settings.ChatDiceEnabled && Client.Instance.Settings.UnifyChatDiceRendering, textDrawList);
                            ImGui.Dummy(new Vector2(w, h));
                        }
                        else
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, icw.Owner.Color.Abgr());
                            ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(icw.Text));
                            ImGui.PopStyleColor();
                            Vector2 vEnd = ImGui.GetCursorScreenPos() + new Vector2(0, icl.Height);
                            if (!string.IsNullOrEmpty(icw.Owner.Tooltip) && ImGui.IsMouseHoveringRect(vBase, vEnd))
                            {
                                ImGui.BeginTooltip();
                                ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(icw.Owner.Tooltip));
                                ImGui.EndTooltip();
                            }
                        }

                        pX += icw.Width + (i == icl.Words.Length - 1 ? 0 : this._spacebarWidth);
                        ImGui.SetCursorPos(new(pX, pY));
                    }

                    pY += icl.Height + 2;
                    ImGui.SetCursorPos(new(ipX, pY));
                }

                foreach ((Vector2, uint, string, bool) textDrawCallback in textDrawList)
                {
                    this.AddTextAt(drawList, textDrawCallback.Item1, textDrawCallback.Item2, textDrawCallback.Item3, textDrawCallback.Item4);
                }
            }
        }
    }
}
