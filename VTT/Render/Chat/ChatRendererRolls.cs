namespace VTT.Render.Chat
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using VTT.Control;
    using VTT.Util;

    public class ChatRendererRolls : ChatRendererBase
    {
        private readonly List<RollContainer[]> _lines = new List<RollContainer[]>();

        public ChatRendererRolls(ChatLine container) : base(container)
        {
        }

        public override void Cache(Vector2 windowSize, out float width, out float height)
        {
            Vector2 imPadding = ImGui.GetStyle().CellPadding;
            Vector2 imSeparatorSize = ImGuiHelper.CalcTextSize(" + ");
            float cX = 8;
            float cY = 8;
            float maxX = ImGui.GetContentRegionAvail().X - imPadding.X - 24;
            float mWidth = 0;
            List<RollContainer> rollContainers = new List<RollContainer>();
            int result = 0;
            string resString = "";
            ChatBlockExpressionRollContents cumulativeContents = ChatBlockExpressionRollContents.None;
            for (int i = 0; i < this.Container.Blocks.Count; i++)
            {
                ChatBlock block = this.Container.Blocks[i];
                Vector2 imTextSize = ImGuiHelper.CalcTextSize(block.Text) + (imPadding * 2);
                if (cX + imTextSize.X > maxX && rollContainers.Count > 0)
                {
                    this._lines.Add(rollContainers.ToArray());
                    mWidth = MathF.Max(mWidth, cX);
                    cX = 8;
                    cY += rollContainers.Max(rc => rc.h) + imPadding.Y;
                    rollContainers.Clear();
                }

                RollContainer rc = new RollContainer(cX, imTextSize.X, cY, imTextSize.Y, block.Text, block.Tooltip, (Vector4)block.Color, block.RollContents);
                rollContainers.Add(rc);
                if (int.TryParse(rc.text, out int res))
                {
                    result += res;
                    if (!string.IsNullOrEmpty(resString))
                    {
                        resString += " + " + res;
                    }
                    else
                    {
                        resString += res.ToString();
                    }
                }

                if (block.RollContents <= ChatBlockExpressionRollContents.SingleDUnknown)
                {
                    ChatBlockExpressionRollContents multiples = (ChatBlockExpressionRollContents)((int)block.RollContents << 8);
                    if (!cumulativeContents.HasFlag(multiples))
                    {
                        if (cumulativeContents.HasFlag(block.RollContents))
                        {
                            cumulativeContents &= ~block.RollContents; // Clear single
                            cumulativeContents |= multiples; // Set multiple
                        }
                        else
                        {
                            cumulativeContents |= block.RollContents;
                        }
                    }
                }
                else
                {
                    cumulativeContents |= block.RollContents;
                }

                cX += rc.w + imSeparatorSize.X;
            }

            resString += " = " + result;
            Vector2 imResultSize = ImGuiHelper.CalcTextSize(result.ToString()) + (imPadding * 2);
            if (cX + imResultSize.X > maxX && rollContainers.Count > 0)
            {
                this._lines.Add(rollContainers.ToArray());
                mWidth = MathF.Max(mWidth, cX);
                cX = 8;
                cY += rollContainers.Max(rc => rc.h) + imPadding.Y;
                rollContainers.Clear();
            }

            RollContainer rrc = new RollContainer(cX, imResultSize.X, cY, imResultSize.Y, result.ToString(), resString, (Vector4)Color.White, cumulativeContents);
            rollContainers.Add(rrc);

            this._lines.Add(rollContainers.ToArray());
            mWidth = MathF.Max(mWidth, cX);
            cX = 8;
            cY += rollContainers.Max(rc => rc.h) + imPadding.Y;
            rollContainers.Clear();

            width = mWidth;
            height = cY + 8;
        }

        public override void ClearCache() => this._lines.Clear();
        public override string ProvideTextForClipboard(DateTime dateTime, string senderName, SimpleLanguage lang)
        {
            string ret = string.Empty;

            for (int i = 0; i < this._lines.Count; i++)
            {
                RollContainer[] line = this._lines[i];
                for (int i1 = 0; i1 < line.Length; i1++)
                {
                    RollContainer block = line[i1];
                    ret += $"{block.text}({RollSyntaxRegex.Replace(block.tooltip, x => $"{x.Groups[1].Value}d{x.Groups[2].Value}[")})";
                    if (i != this._lines.Count - 1 || (i1 != line.Length - 2 && i1 != line.Length - 1))
                    {
                        ret += " + ";
                    }
                    else
                    {
                        if (i1 != line.Length - 1)
                        {
                            ret += " = ";
                        }
                    }

                }
            }

            return ret;
        }

        public override void Render(Guid senderId, uint senderColorAbgr)
        {
            Vector2 imCursor = ImGui.GetCursorScreenPos();
            Vector2 imPadding = ImGui.GetStyle().CellPadding;
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            float ocX = ImGui.GetCursorPosX();
            float ocY = ImGui.GetCursorPosY();
            float ccY = ImGui.GetCursorPosY();
            float cH = 0;
            float mW = 0;
            for (int i = 0; i < this._lines.Count; i++)
            {
                RollContainer[] line = this._lines[i];
                float mh = 0;
                float ccX = ImGui.GetCursorPosX();
                float aW = 0;

                for (int i1 = 0; i1 < line.Length; i1++)
                {
                    RollContainer block = line[i1];
                    float cX = imCursor.X + block.x;
                    float cY = imCursor.Y + block.y;

                    mh = MathF.Max(mh, block.h);
                    float w = block.w;
                    float h = block.h;

                    this.AddTooltipBlock(drawList, new RectangleF(cX, cY, w, h), block.text, default, block.tooltip, block.rollContents, block.color.Abgr(), senderColorAbgr);
                    ImGui.Dummy(new Vector2(w, h));

                    ImGui.SetCursorPos(new(ccX + block.x + block.w, ccY + block.y));
                    if (i != this._lines.Count - 1 || (i1 != line.Length - 2 && i1 != line.Length - 1))
                    {
                        ImGui.TextColored(Vector4.One, " + ");
                    }
                    else
                    {
                        if (i1 != line.Length - 1)
                        {
                            ImGui.TextColored(Vector4.One, " = ");
                        }
                    }

                    aW = block.x + block.w + ImGuiHelper.CalcTextSize(" + ").X;
                }

                cH += mh;
                ocY += mh;
                mW = MathF.Max(aW, mW);
                ImGui.SetCursorPos(new(ocX, ocY));
            }

            RollContainer[] cons = this._lines[^1];
            uint outline = Color.Gray.Abgr();
            uint color = Color.White.Abgr();

            drawList.AddLine(new(imCursor.X + mW - 94, imCursor.Y), new(imCursor.X + mW, imCursor.Y), outline);
            drawList.AddLine(new(imCursor.X + mW, imCursor.Y), new(imCursor.X + mW, imCursor.Y + 28), outline);

            drawList.AddTriangleFilled(new(imCursor.X + mW - 101, imCursor.Y + 1), new(imCursor.X + mW - 1, imCursor.Y + 1), new(imCursor.X + mW - 1, imCursor.Y + 3), color);
            drawList.AddTriangleFilled(new(imCursor.X + mW, imCursor.Y + 1), new(imCursor.X + mW, imCursor.Y + 24), new(imCursor.X + mW - 2, imCursor.Y + 1), color);

            float aH = cons[0].y + cons[0].h + imPadding.Y + 8 - 4;

            drawList.AddLine(new(imCursor.X + 94, imCursor.Y + aH), new(imCursor.X, imCursor.Y + aH), outline);
            drawList.AddLine(new(imCursor.X, imCursor.Y + aH - 24), new(imCursor.X, imCursor.Y + aH - 1), outline);

            drawList.AddTriangleFilled(new(imCursor.X + 1, imCursor.Y + aH - 2), new(imCursor.X + 101, imCursor.Y + aH), new(imCursor.X + 1, imCursor.Y + aH), color);
            drawList.AddTriangleFilled(new(imCursor.X + 2, imCursor.Y + aH - 1), new(imCursor.X + 1.5f, imCursor.Y + aH - 16), new(imCursor.X + 2, imCursor.Y + aH - 1), color);

            ImGui.SetCursorPos(new(ocX, ccY + aH + 4));
        }

        protected readonly struct RollContainer
        {
            public readonly float x;
            public readonly float w;
            public readonly float y;
            public readonly float h;

            public readonly string text;
            public readonly string tooltip;
            public readonly Vector4 color;
            public readonly ChatBlockExpressionRollContents rollContents;

            public RollContainer(float x, float w, float y, float h, string text, string tooltip, Vector4 color, ChatBlockExpressionRollContents contents)
            {
                this.x = x;
                this.w = w;
                this.y = y;
                this.h = h;
                this.text = text;
                this.tooltip = tooltip;
                this.color = color;
                this.rollContents = contents;
            }
        }
    }
}
