namespace VTT.Render.Chat
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Processing.Processors.Normalization;
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using VTT.Control;
    using VTT.Network;
    using VTT.Util;
    using static VTT.Network.ClientSettings;

    public abstract class ChatRendererBase
    {
        public static Regex RollSyntaxRegex { get; } = new Regex("roll\\(([0-9]+), ([0-9]+)\\)\\[=", RegexOptions.Compiled);

        public ChatLine Container { get; }

        public ChatRendererBase(ChatLine container) => this.Container = container;

        public abstract void Render(Guid senderId, uint senderColorAbgr);
        public abstract void Cache(Vector2 windowSize, out float width, out float height);
        public abstract void ClearCache();
        public abstract string ProvideTextForClipboard(DateTime dateTime, string senderName, SimpleLanguage lang);

        public static string TextOrAlternative(string text, string alt) => string.IsNullOrEmpty(text) ? alt : text;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint SelectDiceColor(uint diceSetColor, ChatDiceColorMode colodMode, uint senderColor)
        {
            return colodMode switch
            {
                ChatDiceColorMode.SetColor => diceSetColor,
                ChatDiceColorMode.SenderColor => senderColor,
                ChatDiceColorMode.OwnColor => Client.Instance.Settings.Color.ArgbToAbgr(),
                _ => diceSetColor
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSingleDie(int rci, ChatBlockExpressionRollContents checkAgainst)
        {
            int rcai = (int)checkAgainst;
            return (rci & ~rcai) == 0;
        }

        // This method looks imposing for something that runs once every frame per die image per roll in the chat
        // However, the main performance question comes from dice image rendering, and that is somewhat optimized by a switch/case and evil gotos
        public void AddTooltipBlock(ImDrawListPtr drawList, RectangleF location, string text, Vector2 knownTextSize, string tt, ChatBlockExpressionRollContents rollContents, uint textColor, uint senderColor)
        {
            float l = location.Left;
            float r = location.Right;
            float t = location.Top;
            float b = location.Bottom;

            uint cell = Extensions.FromHex("161616").Abgr();
            uint cellOutline = Color.Gray.Abgr();
            const float rounding = 5f;
            bool hover = ImGui.IsMouseHoveringRect(new Vector2(l, t), new Vector2(r, b));
            bool needShadow = false;
            IntPtr iconPrimary = IntPtr.Zero;
            IntPtr iconSecondary = IntPtr.Zero;
            uint clrPrimary = 0xffffffff;
            uint clrSecondary = 0xffffffff;
            bool multipleDiceMode = false;

            if (rollContents == ChatBlockExpressionRollContents.None || !Client.Instance.Settings.ChatDiceEnabled)
            {
                drawList.AddRectFilled(new Vector2(l, t), new Vector2(r, b), cell, rounding);

                if (hover)
                {
                    cellOutline = Color.RoyalBlue.Abgr();
                }

                drawList.AddRect(new Vector2(l, t), new Vector2(r, b), cellOutline, rounding);
            }
            else
            {
                IntPtr highlightPrimary = IntPtr.Zero;
                IntPtr highlightSecondary = IntPtr.Zero;
                int rci = (int)rollContents;
                bool havePick = false;

                switch (rollContents)
                {
                    // First test if we have the most basic singular die roll
                    case ChatBlockExpressionRollContents.SingleD4:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4Highlight;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD4, Client.Instance.Settings.ColorModeD4, senderColor);
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.SingleD6:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6Highlight;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD6, Client.Instance.Settings.ColorModeD6, senderColor);
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.SingleD8:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8Highlight;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD8, Client.Instance.Settings.ColorModeD8, senderColor);
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.SingleD10:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10Highlight;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD10, Client.Instance.Settings.ColorModeD10, senderColor);
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.SingleD12:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12Highlight;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD12, Client.Instance.Settings.ColorModeD12, senderColor);
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.SingleDUnknown:
                    case ChatBlockExpressionRollContents.SingleD20:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20Highlight;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD20, Client.Instance.Settings.ColorModeD20, senderColor);
                        havePick = true;
                        break;
                    }

                    // If basic SINGLE fails, test for basic MULTIPLE
                    case ChatBlockExpressionRollContents.MultipleD4:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4Primary;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4PrimaryHighlight;
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4Secondary;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4SecondaryHighlight;
                        clrPrimary = clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD4, Client.Instance.Settings.ColorModeD4, senderColor);
                        multipleDiceMode = true;
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.MultipleD6:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6Primary;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6PrimaryHighlight;
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6Secondary;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6SecondaryHighlight;
                        clrPrimary = clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD6, Client.Instance.Settings.ColorModeD6, senderColor);
                        multipleDiceMode = true;
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.MultipleD8:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8Primary;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8PrimaryHighlight;
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8Secondary;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8SecondaryHighlight;
                        clrPrimary = clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD8, Client.Instance.Settings.ColorModeD8, senderColor);
                        multipleDiceMode = true;
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.SingleD100:
                    case ChatBlockExpressionRollContents.MultipleD100:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10Primary;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10PrimaryHighlight;
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10Secondary;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10SecondaryHighlight;
                        clrPrimary = clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD100, Client.Instance.Settings.ColorModeD100, senderColor);
                        multipleDiceMode = true;
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.MultipleD10:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10Primary;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10PrimaryHighlight;
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10Secondary;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10SecondaryHighlight;
                        clrPrimary = clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD10, Client.Instance.Settings.ColorModeD10, senderColor);
                        multipleDiceMode = true;
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.MultipleD12:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12Primary;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12PrimaryHighlight;
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12Secondary;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12SecondaryHighlight;
                        clrPrimary = clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD12, Client.Instance.Settings.ColorModeD12, senderColor);
                        multipleDiceMode = true;
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.MultipleDUnknown:
                    case ChatBlockExpressionRollContents.MultipleD20:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20Primary;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20PrimaryHighlight;
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20Secondary;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20SecondaryHighlight;
                        clrPrimary = clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD20, Client.Instance.Settings.ColorModeD10, senderColor);
                        multipleDiceMode = true;
                        havePick = true;
                        break;
                    }
                }

                // If we are here the combination is a compound one - need to decode it somehow
                if (!havePick)
                {
                    multipleDiceMode = true;
                    int diePrimary = 0;

                    // If we are here we 100% have AT LEAST TWO DIFFERENT dice
                    // So it is safe to assign primaries/secondaries here

                    // First we test for a primary in the DESCENDING order of die side magnitude
                    if ((rci & (int)ChatBlockExpressionRollContents.AnyD20) != 0 || (rci & (int)ChatBlockExpressionRollContents.AnyDUnknown) != 0)
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20Primary;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20PrimaryHighlight;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD20, Client.Instance.Settings.ColorModeD20, senderColor);
                        diePrimary = 20;
                        goto lFoundPrimary;
                    }

                    if ((rci & (int)ChatBlockExpressionRollContents.AnyD12) != 0)
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12Primary;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12PrimaryHighlight;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD12, Client.Instance.Settings.ColorModeD12, senderColor);
                        diePrimary = 12;
                        goto lFoundPrimary;
                    }

                    // I don't know what to do with a D100 + Something else combo, will treat it like a D10 for now...
                    // HACK - figure out a better way to handle a D100 + Other die combo
                    bool isD100 = (rci & (int)ChatBlockExpressionRollContents.AnyD100) != 0;
                    if ((rci & (int)ChatBlockExpressionRollContents.AnyD10) != 0 || isD100)
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10Primary;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10PrimaryHighlight;
                        clrPrimary = isD100 ? SelectDiceColor(Client.Instance.Settings.ColorD100, Client.Instance.Settings.ColorModeD100, senderColor) : SelectDiceColor(Client.Instance.Settings.ColorD10, Client.Instance.Settings.ColorModeD10, senderColor);
                        diePrimary = 10;
                        goto lFoundPrimary;
                    }

                    if ((rci & (int)ChatBlockExpressionRollContents.AnyD8) != 0)
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8Primary;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8PrimaryHighlight;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD8, Client.Instance.Settings.ColorModeD8, senderColor);
                        diePrimary = 8;
                        goto lFoundPrimary;
                    }

                    if ((rci & (int)ChatBlockExpressionRollContents.AnyD6) != 0)
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6Primary;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6PrimaryHighlight;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD6, Client.Instance.Settings.ColorModeD6, senderColor);
                        diePrimary = 6;
                        goto lFoundPrimary;
                    }

                    if ((rci & (int)ChatBlockExpressionRollContents.AnyD4) != 0)
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4Primary;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4PrimaryHighlight;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD4, Client.Instance.Settings.ColorModeD4, senderColor);
                        diePrimary = 4;
                    }

                // The EVIL goto label!
                lFoundPrimary:;
                    if (diePrimary == 0) // Well, something's broken
                    {
                        goto lEndIconSelectionProc;
                    }

                    bool haveSecondary = false;

                    // Now we test for a secondary, but we need to be careful to not assign the same secondary as a primary!
                    // Note that we don't test for a 20 primary here as a D20 secondary is impossible to get - if available, will always be picked as primary

                    if (diePrimary != 12 && (rci & (int)ChatBlockExpressionRollContents.AnyD12) != 0)
                    {
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12Secondary;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12SecondaryHighlight;
                        clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD12, Client.Instance.Settings.ColorModeD12, senderColor);
                        haveSecondary = true;
                        goto lFoundSecondary;
                    }

                    // HACK - figure out a better way to handle a Die + D100
                    isD100 = (rci & (int)ChatBlockExpressionRollContents.AnyD100) != 0;
                    if (diePrimary != 10 && ((rci & (int)ChatBlockExpressionRollContents.AnyD10) != 0 || isD100))
                    {
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10Secondary;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10SecondaryHighlight;
                        clrSecondary = isD100 ? SelectDiceColor(Client.Instance.Settings.ColorD100, Client.Instance.Settings.ColorModeD100, senderColor) : SelectDiceColor(Client.Instance.Settings.ColorD10, Client.Instance.Settings.ColorModeD10, senderColor);
                        haveSecondary = true;
                        goto lFoundSecondary;
                    }

                    if (diePrimary != 8 && (rci & (int)ChatBlockExpressionRollContents.AnyD8) != 0)
                    {
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8Secondary;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8SecondaryHighlight;
                        clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD8, Client.Instance.Settings.ColorModeD8, senderColor);
                        haveSecondary = true;
                        goto lFoundSecondary;
                    }

                    if (diePrimary != 6 && (rci & (int)ChatBlockExpressionRollContents.AnyD6) != 0)
                    {
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6Secondary;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6SecondaryHighlight;
                        clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD6, Client.Instance.Settings.ColorModeD6, senderColor);
                        haveSecondary = true;
                        goto lFoundSecondary;
                    }

                    if (diePrimary != 4 && (rci & (int)ChatBlockExpressionRollContents.AnyD4) != 0)
                    {
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4Secondary;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4SecondaryHighlight;
                        clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD4, Client.Instance.Settings.ColorModeD4, senderColor);
                        haveSecondary = true;
                    }

                lFoundSecondary:;
                    if (!haveSecondary) // Well, something is horrendously broken
                    {
                    }
                    else
                    {
                        multipleDiceMode = true;
                    }
                }

            lEndIconSelectionProc:;
                if (iconPrimary != IntPtr.Zero && (!multipleDiceMode || iconSecondary != IntPtr.Zero))
                {
                    if (hover)
                    {
                        drawList.AddImage(highlightPrimary, new Vector2(l, t), new Vector2(r, b), Vector2.Zero, Vector2.One, Color.RoyalBlue.Abgr());
                        if (multipleDiceMode)
                        {
                            drawList.AddImage(highlightSecondary, new Vector2(l, t), new Vector2(r, b), Vector2.Zero, Vector2.One, Color.RoyalBlue.Abgr());
                        }
                    }

                    drawList.AddImage(iconPrimary, new Vector2(l, t), new Vector2(r, b), Vector2.Zero, Vector2.One, clrPrimary);
                    if (multipleDiceMode)
                    {
                        drawList.AddImage(iconSecondary, new Vector2(l, t), new Vector2(r, b), Vector2.Zero, Vector2.One, clrSecondary);
                    }

                    needShadow = true;
                }
                else
                {
                    // Fallback
                    drawList.AddRectFilled(new Vector2(l, t), new Vector2(r, b), cell, rounding);

                    if (hover)
                    {
                        cellOutline = Color.RoyalBlue.Abgr();
                    }

                    drawList.AddRect(new Vector2(l, t), new Vector2(r, b), cellOutline, rounding);
                }
            }

            if (!string.IsNullOrEmpty(text))
            {
                float w = location.Width;
                float h = location.Height;
                Vector2 tSize = knownTextSize.Equals(default) ? ImGui.CalcTextSize(text) : knownTextSize;
                float tx = l + (w * 0.5f) - (tSize.X * 0.5f);
                float ty = t + (h * 0.5f) - (tSize.Y * 0.5f);
                if (needShadow)
                {
                    if (Client.Instance.Settings.TextThickDropShadow)
                    {
                        for (int i = 0; i < 9; ++i)
                        {
                            int kx = (i % 3) - 1;
                            int ky = (i / 3) - 1;
                            if (kx != 0 || ky != 0)
                            {
                                drawList.AddText(new Vector2(tx + kx, ty + ky), 0xff000000, text);
                            }
                        }
                    }
                    else
                    {
                        drawList.AddText(new Vector2(tx + 1, ty + 1), 0xff000000, text);
                        drawList.AddText(new Vector2(tx - 1, ty + 1), 0xff000000, text);
                        drawList.AddText(new Vector2(tx + 1, ty - 1), 0xff000000, text);
                        drawList.AddText(new Vector2(tx - 1, ty - 1), 0xff000000, text);
                    }
                }

                drawList.AddText(new Vector2(l + (w * 0.5f) - (tSize.X * 0.5f), t + (h * 0.5f) - (tSize.Y * 0.5f)), textColor, text);
            }

            if (hover && !string.IsNullOrEmpty(tt))
            {
                ImGui.BeginTooltip();
                if (iconPrimary != IntPtr.Zero)
                {
                    Vector2 here = ImGui.GetCursorPos();
                    ImGui.Image(iconPrimary, new Vector2(32, 32), Vector2.Zero, Vector2.One, Extensions.Vec4FromAbgr(clrPrimary));
                    if (multipleDiceMode && iconSecondary != IntPtr.Zero)
                    {
                        ImGui.SetCursorPos(here);
                        ImGui.Image(iconSecondary, new Vector2(32, 32), Vector2.Zero, Vector2.One, Extensions.Vec4FromAbgr(clrSecondary));
                    }

                    if (tt.Contains('+') || tt.Contains('-') || tt.Contains('*') || tt.Contains('/'))
                    {
                        ImGui.SetCursorPos(here + new Vector2(32, 16));
                        ImGui.Image(Client.Instance.Frontend.Renderer.GuiRenderer.AddIcon, new Vector2(16, 16), Vector2.Zero, Vector2.One, Vector4.One);
                    }
                }

                ImGui.TextUnformatted(tt);
                ImGui.EndTooltip();
            }
        }
    }
}
