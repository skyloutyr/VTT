namespace VTT.Render.Chat
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using VTT.Asset;
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
        public static uint SelectDiceColor(uint diceSetColor, ChatDiceColorMode colodMode, uint senderColor)
        {
            return colodMode switch
            {
                ChatDiceColorMode.SetColor => diceSetColor,
                ChatDiceColorMode.SenderColor => senderColor,
                ChatDiceColorMode.OwnColor => Client.Instance.Settings.Color.ArgbToAbgr(),
                _ => diceSetColor
            };
        }

        public enum ImageBlockImageType
        {
            AssetRef,
            URL,
            EmbeddedBase64,
            Invalid
        }

        private static readonly Regex Base64CheckerRegex = new Regex(@"(?:[A-Z]|[a-z]|[0-9]|[+\/=])+", RegexOptions.Compiled);
        public static AssetStatus ResolveImageBlock(string reference, out ImageBlockImageType imgType, out Asset a, out AssetPreview ap)
        {
            Guid imgAssetId = Guid.Empty;

            // While we don't explicitly support registry formatted GUIDs, they are supported implicitly, so we check for length here to not break backwards compat
            bool isAssetRef = reference.Length is >= 36 and <= 38 && Guid.TryParse(reference, out imgAssetId);
            // Here we don't bother with base64 encoding that is too small in length (smallest possible valid png file is 67 bytes), and we check the first 38 characters for b64 encoding just in case
            bool isB64 = reference.Length >= 89 && Base64CheckerRegex.IsMatch(reference[..38]);
            // URL checking is more costly, so only do so if we fail all other checks. Limited to a common 2083 length limit (IE/Edge/Chromium).
            bool isUrl = reference.Length <= 2083 && !isAssetRef && !isB64 && Uri.IsWellFormedUriString(reference, UriKind.Absolute);
            imgType = 
                isB64 ? ImageBlockImageType.EmbeddedBase64 :
                isAssetRef ? ImageBlockImageType.AssetRef :
                isUrl ? ImageBlockImageType.URL :
                ImageBlockImageType.Invalid;

            ap = null;
            a = null;

            return imgType switch 
            {
                ImageBlockImageType.AssetRef => Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(imgAssetId, AssetType.Texture, out a),
                ImageBlockImageType.URL => Client.Instance.AssetManager.ClientAssetLibrary.WebPictures.Get(reference, AssetType.Texture, out ap),
                ImageBlockImageType.EmbeddedBase64 => Client.Instance.AssetManager.ClientAssetLibrary.Base64Pictures.Get(reference, AssetType.Texture, out ap),
                _ => AssetStatus.Error
            };
        }

        public void AddTextAt(ImDrawListPtr drawList, Vector2 location, uint color, string text, bool dropShadow)
        {
            if (dropShadow)
            {
                if (Client.Instance.Settings.TextThickDropShadow)
                {
                    for (int i = 0; i < 9; ++i)
                    {
                        int kx = (i % 3) - 1;
                        int ky = (i / 3) - 1;
                        if (kx != 0 || ky != 0)
                        {
                            drawList.AddText(location + new Vector2(kx, ky), 0xff000000, text);
                        }
                    }
                }
                else
                {
                    drawList.AddText(location + new Vector2(1, 1), 0xff000000, text);
                    drawList.AddText(location + new Vector2(-1, 1), 0xff000000, text);
                    drawList.AddText(location + new Vector2(1, -1), 0xff000000, text);
                    drawList.AddText(location + new Vector2(-1, -1), 0xff000000, text);
                }
            }

            drawList.AddText(location, color, text);
        }

        private const uint cellColor = 0xff161616;
        private const uint cellOutlineColor = 0xff808080;
        private const uint cellOutlineHoverColor = 0xffe16941;

        // This method looks imposing for something that runs once every frame per die image per roll in the chat
        // However, the main performance question comes from dice image rendering, and that is somewhat optimized by a switch/case and evil gotos
        public void AddTooltipBlock(ImDrawListPtr drawList, RectangleF location, string text, Vector2 knownTextSize, string tt, ChatBlockExpressionRollContents rollContents, uint textColor, uint senderColor, bool renderTextNow = true, IList<(Vector2, uint, string, bool)> textRenderAccumulator = null)
        {
            float l = location.Left;
            float r = location.Right;
            float t = location.Top;
            float b = location.Bottom;

            const float rounding = 5f;
            bool hover = ImGui.IsMouseHoveringRect(new Vector2(l, t), new Vector2(r, b));
            bool needShadow = false;
            (Vector2, Vector2)? iconPrimary = null;
            (Vector2, Vector2)? iconSecondary = null;
            uint clrPrimary = 0xffffffff;
            uint clrSecondary = 0xffffffff;
            bool multipleDiceMode = false;
            uint cellOutline = cellOutlineColor;
            IntPtr atlas = Client.Instance.Frontend.Renderer.GuiRenderer.DiceIconAtlas;

            if (rollContents == ChatBlockExpressionRollContents.None || !Client.Instance.Settings.ChatDiceEnabled)
            {
                drawList.AddRectFilled(new Vector2(l, t), new Vector2(r, b), cellColor, rounding);

                if (hover)
                {
                    cellOutline = cellOutlineHoverColor;
                }

                drawList.AddRect(new Vector2(l, t), new Vector2(r, b), cellOutline, rounding);
            }
            else
            {
                (Vector2, Vector2)? highlightPrimary = null;
                (Vector2, Vector2)? highlightSecondary = null;
                int rci = (int)rollContents;
                bool havePick = false;

                switch (rollContents)
                {
                    // First test if we have the most basic singular die roll
                    case ChatBlockExpressionRollContents.SingleD2:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD2.BoundsSingularTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD2.BoundsSingularHighlightTuple;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD2, Client.Instance.Settings.ColorModeD2, senderColor);
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.SingleD4:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4.BoundsSingularTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4.BoundsSingularHighlightTuple;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD4, Client.Instance.Settings.ColorModeD4, senderColor);
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.SingleD6:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6.BoundsSingularTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6.BoundsSingularHighlightTuple;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD6, Client.Instance.Settings.ColorModeD6, senderColor);
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.SingleD8:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8.BoundsSingularTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8.BoundsSingularHighlightTuple;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD8, Client.Instance.Settings.ColorModeD8, senderColor);
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.SingleD10:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10.BoundsSingularTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10.BoundsSingularHighlightTuple;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD10, Client.Instance.Settings.ColorModeD10, senderColor);
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.SingleD12:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12.BoundsSingularTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12.BoundsSingularHighlightTuple;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD12, Client.Instance.Settings.ColorModeD12, senderColor);
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.SingleD20:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20.BoundsSingularTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20.BoundsSingularHighlightTuple;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD20, Client.Instance.Settings.ColorModeD20, senderColor);
                        havePick = true;
                        break;
                    }

                    // If basic SINGLE fails, test for basic MULTIPLE
                    case ChatBlockExpressionRollContents.MultipleD2:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD2.BoundsPrimaryTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD2.BoundsPrimaryHighlightTuple;
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD2.BoundsSecondaryTuple;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD2.BoundsSecondaryHighlightTuple;
                        clrPrimary = clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD2, Client.Instance.Settings.ColorModeD2, senderColor);
                        multipleDiceMode = true;
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.MultipleD4:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4.BoundsPrimaryTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4.BoundsPrimaryHighlightTuple;
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4.BoundsSecondaryTuple;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4.BoundsSecondaryHighlightTuple;
                        clrPrimary = clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD4, Client.Instance.Settings.ColorModeD4, senderColor);
                        multipleDiceMode = true;
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.MultipleD6:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6.BoundsPrimaryTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6.BoundsPrimaryHighlightTuple;
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6.BoundsSecondaryTuple;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6.BoundsSecondaryHighlightTuple;
                        clrPrimary = clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD6, Client.Instance.Settings.ColorModeD6, senderColor);
                        multipleDiceMode = true;
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.MultipleD8:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8.BoundsPrimaryTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8.BoundsPrimaryHighlightTuple;
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8.BoundsSecondaryTuple;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8.BoundsSecondaryHighlightTuple;
                        clrPrimary = clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD8, Client.Instance.Settings.ColorModeD8, senderColor);
                        multipleDiceMode = true;
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.SingleD100:
                    case ChatBlockExpressionRollContents.MultipleD100:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10.BoundsPrimaryTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10.BoundsPrimaryHighlightTuple;
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10.BoundsSecondaryTuple;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10.BoundsSecondaryHighlightTuple;
                        clrPrimary = clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD100, Client.Instance.Settings.ColorModeD100, senderColor);
                        multipleDiceMode = true;
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.MultipleD10:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10.BoundsPrimaryTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10.BoundsPrimaryHighlightTuple;
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10.BoundsSecondaryTuple;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10.BoundsSecondaryHighlightTuple;
                        clrPrimary = clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD10, Client.Instance.Settings.ColorModeD10, senderColor);
                        multipleDiceMode = true;
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.MultipleD12:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12.BoundsPrimaryTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12.BoundsPrimaryHighlightTuple;
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12.BoundsSecondaryTuple;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12.BoundsSecondaryHighlightTuple;
                        clrPrimary = clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD12, Client.Instance.Settings.ColorModeD12, senderColor);
                        multipleDiceMode = true;
                        havePick = true;
                        break;
                    }

                    case ChatBlockExpressionRollContents.MultipleD20:
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20.BoundsPrimaryTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20.BoundsPrimaryHighlightTuple;
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20.BoundsSecondaryTuple;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20.BoundsSecondaryHighlightTuple;
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
                    if ((rci & (int)ChatBlockExpressionRollContents.AnyD20) != 0 || (rci & (int)ChatBlockExpressionRollContents.AnyD2) != 0)
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20.BoundsPrimaryTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD20.BoundsPrimaryHighlightTuple;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD20, Client.Instance.Settings.ColorModeD20, senderColor);
                        diePrimary = 20;
                        goto lFoundPrimary;
                    }

                    if ((rci & (int)ChatBlockExpressionRollContents.AnyD12) != 0)
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12.BoundsPrimaryTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12.BoundsPrimaryHighlightTuple;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD12, Client.Instance.Settings.ColorModeD12, senderColor);
                        diePrimary = 12;
                        goto lFoundPrimary;
                    }

                    // I don't know what to do with a D100 + Something else combo, will treat it like a D10 for now...
                    // HACK - figure out a better way to handle a D100 + Other die combo
                    bool isD100 = (rci & (int)ChatBlockExpressionRollContents.AnyD100) != 0;
                    if ((rci & (int)ChatBlockExpressionRollContents.AnyD10) != 0 || isD100)
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10.BoundsPrimaryTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10.BoundsPrimaryHighlightTuple;
                        clrPrimary = isD100 ? SelectDiceColor(Client.Instance.Settings.ColorD100, Client.Instance.Settings.ColorModeD100, senderColor) : SelectDiceColor(Client.Instance.Settings.ColorD10, Client.Instance.Settings.ColorModeD10, senderColor);
                        diePrimary = 10;
                        goto lFoundPrimary;
                    }

                    if ((rci & (int)ChatBlockExpressionRollContents.AnyD8) != 0)
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8.BoundsPrimaryTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8.BoundsPrimaryHighlightTuple;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD8, Client.Instance.Settings.ColorModeD8, senderColor);
                        diePrimary = 8;
                        goto lFoundPrimary;
                    }

                    if ((rci & (int)ChatBlockExpressionRollContents.AnyD6) != 0)
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6.BoundsPrimaryTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6.BoundsPrimaryHighlightTuple;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD6, Client.Instance.Settings.ColorModeD6, senderColor);
                        diePrimary = 6;
                        goto lFoundPrimary;
                    }

                    if ((rci & (int)ChatBlockExpressionRollContents.AnyD4) != 0)
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4.BoundsPrimaryTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4.BoundsPrimaryHighlightTuple;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD4, Client.Instance.Settings.ColorModeD4, senderColor);
                        diePrimary = 4;
                        goto lFoundPrimary;
                    }

                    if ((rci & (int)ChatBlockExpressionRollContents.AnyD2) != 0)
                    {
                        iconPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD2.BoundsPrimaryTuple;
                        highlightPrimary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD2.BoundsPrimaryHighlightTuple;
                        clrPrimary = SelectDiceColor(Client.Instance.Settings.ColorD2, Client.Instance.Settings.ColorModeD2, senderColor);
                        diePrimary = 2;
                    }

                // The EVIL goto label!
                lFoundPrimary:;
                    if (diePrimary == 0) // Well, something's broken
                    {
                        goto lEndIconSelection;
                    }

                    bool haveSecondary = false;

                    // Now we test for a secondary, but we need to be careful to not assign the same secondary as a primary!
                    // Note that we don't test for a 20 primary here as a D20 secondary is impossible to get - if available, will always be picked as primary

                    if (diePrimary != 12 && (rci & (int)ChatBlockExpressionRollContents.AnyD12) != 0)
                    {
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12.BoundsSecondaryTuple;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD12.BoundsSecondaryHighlightTuple;
                        clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD12, Client.Instance.Settings.ColorModeD12, senderColor);
                        haveSecondary = true;
                        goto lFoundSecondary;
                    }

                    // HACK - figure out a better way to handle a Die + D100
                    isD100 = (rci & (int)ChatBlockExpressionRollContents.AnyD100) != 0;
                    if (diePrimary != 10 && ((rci & (int)ChatBlockExpressionRollContents.AnyD10) != 0 || isD100))
                    {
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10.BoundsSecondaryTuple;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD10.BoundsSecondaryHighlightTuple;
                        clrSecondary = isD100 ? SelectDiceColor(Client.Instance.Settings.ColorD100, Client.Instance.Settings.ColorModeD100, senderColor) : SelectDiceColor(Client.Instance.Settings.ColorD10, Client.Instance.Settings.ColorModeD10, senderColor);
                        haveSecondary = true;
                        goto lFoundSecondary;
                    }

                    if (diePrimary != 8 && (rci & (int)ChatBlockExpressionRollContents.AnyD8) != 0)
                    {
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8.BoundsSecondaryTuple;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD8.BoundsSecondaryHighlightTuple;
                        clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD8, Client.Instance.Settings.ColorModeD8, senderColor);
                        haveSecondary = true;
                        goto lFoundSecondary;
                    }

                    if (diePrimary != 6 && (rci & (int)ChatBlockExpressionRollContents.AnyD6) != 0)
                    {
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6.BoundsSecondaryTuple;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD6.BoundsSecondaryHighlightTuple;
                        clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD6, Client.Instance.Settings.ColorModeD6, senderColor);
                        haveSecondary = true;
                        goto lFoundSecondary;
                    }

                    if (diePrimary != 4 && (rci & (int)ChatBlockExpressionRollContents.AnyD4) != 0)
                    {
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4.BoundsSecondaryTuple;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD4.BoundsSecondaryHighlightTuple;
                        clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD4, Client.Instance.Settings.ColorModeD4, senderColor);
                        haveSecondary = true;
                        goto lFoundSecondary;
                    }

                    if (diePrimary != 2 && (rci & (int)ChatBlockExpressionRollContents.AnyD2) != 0)
                    {
                        iconSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD2.BoundsSecondaryTuple;
                        highlightSecondary = Client.Instance.Frontend.Renderer.GuiRenderer.ChatIconD2.BoundsSecondaryHighlightTuple;
                        clrSecondary = SelectDiceColor(Client.Instance.Settings.ColorD2, Client.Instance.Settings.ColorModeD2, senderColor);
                        haveSecondary = true;
                    }

                lFoundSecondary:;
                    if (haveSecondary) 
                    {
                        multipleDiceMode = true;
                    }

                    // If we have no secondary here things are broken
                }

            lEndIconSelection:;
                if (iconPrimary.HasValue && (!multipleDiceMode || iconSecondary.HasValue))
                {
                    if (hover)
                    {
                        drawList.AddImage(atlas, new Vector2(l, t), new Vector2(r, b), highlightPrimary.Value.Item1, highlightPrimary.Value.Item2, ColorAbgr.RoyalBlue);
                        if (multipleDiceMode)
                        {
                            drawList.AddImage(atlas, new Vector2(l, t), new Vector2(r, b), highlightSecondary.Value.Item1, highlightSecondary.Value.Item2, ColorAbgr.RoyalBlue);
                        }
                    }

                    drawList.AddImage(atlas, new Vector2(l, t), new Vector2(r, b), iconPrimary.Value.Item1, iconPrimary.Value.Item2, clrPrimary);
                    if (multipleDiceMode)
                    {
                        drawList.AddImage(atlas, new Vector2(l, t), new Vector2(r, b), iconSecondary.Value.Item1, iconSecondary.Value.Item2, clrSecondary);
                    }

                    needShadow = true;
                }
                else
                {
                    // Fallback
                    drawList.AddRectFilled(new Vector2(l, t), new Vector2(r, b), cellColor, rounding);

                    if (hover)
                    {
                        cellOutline = ColorAbgr.RoyalBlue;
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
                if (renderTextNow)
                {
                    this.AddTextAt(drawList, new Vector2(tx, ty), textColor, text, needShadow);
                }
                else
                {
                    textRenderAccumulator?.Add((new Vector2(tx, ty), textColor, text, needShadow));
                }
            }

            if (hover && !string.IsNullOrEmpty(tt))
            {
                ImGui.BeginTooltip();
                if (iconPrimary.HasValue)
                {
                    Vector2 here = ImGui.GetCursorPos();
                    ImGui.Image(atlas, new Vector2(32, 32), iconPrimary.Value.Item1, iconPrimary.Value.Item2, Extensions.Vec4FromAbgr(clrPrimary));
                    if (multipleDiceMode && iconSecondary.HasValue)
                    {
                        ImGui.SetCursorPos(here);
                        ImGui.Image(atlas, new Vector2(32, 32), iconSecondary.Value.Item1, iconSecondary.Value.Item2, Extensions.Vec4FromAbgr(clrSecondary));
                    }

                    if (tt.Contains('+') || tt.Contains('-') || tt.Contains('*') || tt.Contains('/'))
                    {
                        ImGui.SetCursorPos(here + new Vector2(32, 16));
                        Client.Instance.Frontend.Renderer.GuiRenderer.AddIcon.ImImage(new Vector2(16, 16));
                    }
                }

                ImGui.TextUnformatted(tt);
                ImGui.EndTooltip();
            }
        }
    }
}
