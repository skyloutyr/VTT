// License: zlib
// Copyright (c) 2019 Juliette Foucaut & Doug Binks
// 
// This software is provided 'as-is', without any express or implied
// warranty. In no event will the authors be held liable for any damages
// arising from the use of this software.
// 
// Permission is granted to anyone to use this software for any purpose,
// including commercial applications, and to alter it and redistribute it
// freely, subject to the following restrictions:
// 
// 1. The origin of this software must not be misrepresented; you must not
//    claim that you wrote the original software. If you use this software
//    in a product, an acknowledgment in the product documentation would be
//    appreciated but is not required.
// 2. Altered source versions must be plainly marked as such, and must not be
//    misrepresented as being the original software.
// 3. This notice may not be removed or altered from any source distribution.

// THIS FILE IS A VERSION OF IMGUI_MARKDOWN (https://github.com/juliettef/imgui_markdown)
// SLIGHTLY MODIFIED AND PORTED TO C#! THIS IS NOT THE ORIGINAL VERSION!
// ORIGINAL CODE CAN BE FOUND HERE https://github.com/juliettef/imgui_markdown/blob/main/imgui_markdown.h
namespace VTT.Render
{
    using ImGuiNET;
    using System;
    using System.Numerics;
    using VTT.Asset;
    using VTT.Network;
    using VTT.Render.MainMenu;

    public static class ImGuiMarkdown
    {
        public class MarkdownConfig
        {
            public static MarkdownConfig Default { get; set; } = new MarkdownConfig() { 
                LinkCallback = x => { 
                    if (!x.IsImage)
                    {
                        MainMenuRenderer.OpenUrl(x.Link);
                    }
                },

                ImageCallback = x => {
                    AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.WebPictures.Get(x.Link, AssetType.Texture, out AssetPreview ap);
                    if (status == AssetStatus.Return && ap != null && ap.GLTex != null)
                    {
                        if (ap.IsAnimated)
                        {
                            Vector2 imPos = ImGui.GetCursorPos();
                            float tW = ap.GLTex.Size.Width;
                            float tH = ap.GLTex.Size.Height;
                            AssetPreview.FrameData frame = ap.GetCurrentFrame((int)((int)Client.Instance.Frontend.UpdatesExisted * (100f / 60f)));
                            float progress = (float)frame.TotalDurationToHere / ap.FramesTotalDelay;
                            float sS = frame.X / tW;
                            float sE = sS + (frame.Width / tW);
                            float tS = frame.Y / tH;
                            float tE = tS + (frame.Height / tH);

                            return new MarkdownImageData()
                            {
                                IsValid = true,
                                Size = CalcImageToImGuiContentRegion(frame.Width, frame.Height),
                                UserTextureID = ap.GLTex,
                                UV0 = new Vector2(sS, tS),
                                UV1 = new Vector2(sE, tE),
                                UseLinkCallback = false
                            };
                        }
                        else
                        {
                            return new MarkdownImageData()
                            {
                                IsValid = true,
                                Size = CalcImageToImGuiContentRegion(240, 240),
                                UserTextureID = ap.GLTex,
                                UseLinkCallback = false
                            };
                        }
                    }

                    if (status == AssetStatus.Error)
                    {
                        MarkdownImageData ret = Client.Instance.Frontend.Renderer.GuiRenderer.NoImageIcon.ToMarkdownImageData();
                        ret.IsValid = true;
                        ret.Size = CalcImageToImGuiContentRegion(240, 240);
                        ret.UseLinkCallback = false;
                    }

                    if (status == AssetStatus.Await)
                    {
                        int frame =
                            (int)((int)Client.Instance.Frontend.UpdatesExisted % 90 / 90.0f * Client.Instance.Frontend.Renderer.GuiRenderer.LoadingSpinnerFrames);
                        float texelIndexStart = (float)frame / Client.Instance.Frontend.Renderer.GuiRenderer.LoadingSpinnerFrames;
                        float texelSize = 1f / Client.Instance.Frontend.Renderer.GuiRenderer.LoadingSpinnerFrames;
                        return new MarkdownImageData()
                        {
                            IsValid = true,
                            Size = CalcImageToImGuiContentRegion(240, 240),
                            UserTextureID = Client.Instance.Frontend.Renderer.GuiRenderer.LoadingSpinner,
                            UV0 = new Vector2(texelIndexStart, 0),
                            UV1 = new Vector2(texelIndexStart + texelSize, 1),
                            UseLinkCallback = false
                        };
                    }

                    return new MarkdownImageData()
                    {
                        IsValid = false
                    };
                }
            };

            public const int NumHeadings = 3;

            public MarkdownLinkCallback LinkCallback { get; set; }
            public MarkdownTooltipCallback TooltipCallback { get; set; }
            public MarkdownImageCallback ImageCallback { get; set; }
            public string LinkIcon { get; set; } = string.Empty;
            public MarkdownHeadingFormat[] HeadingFormats { get; set; } = new MarkdownHeadingFormat[]
            {
                new MarkdownHeadingFormat(){ Font = null, Separator = true },
                new MarkdownHeadingFormat(){ Font = null, Separator = true },
                new MarkdownHeadingFormat(){ Font = null, Separator = true }
            };

            public IntPtr UserData { get; set; }
            public MarkdownFormalCallback FormatCallback { get; set; } = DefaultMarkdownFormatCallback;
        }

        public class MarkdownLinkCallbackData
        {
            public string Text { get; set; }
            public string Link { get; set; }
            public bool IsImage { get; set; }
        }

        public class MarkdownTooltipCallbackData
        {
            public MarkdownLinkCallbackData LinkData { get; set; }
            public string LinkIcon { get; set; }
        }

        public class MarkdownImageData
        {
            public bool IsValid { get; set; }
            public bool UseLinkCallback { get; set; }
            public IntPtr UserTextureID { get; set; }
            public Vector2 Size { get; set; } = new Vector2(100, 100);
            public Vector2 UV0 { get; set; } = Vector2.Zero;
            public Vector2 UV1 { get; set; } = Vector2.One;
            public Vector4 TintColor { get; set; } = Vector4.One;
            public Vector4 BorderColor { get; set; } = Vector4.Zero;
        }

        public enum MarkdownFormatType
        {
            NormalText,
            Heading,
            UnorderedList,
            Link,
            Emphasis
        }

        public class MarkdownFormatInfo
        {
            public MarkdownFormatType Type { get; set; }
            public int Level { get; set; }
            public bool ItemHovered { get; set; }
            public MarkdownConfig Config { get; set; }
        }

        public delegate void MarkdownLinkCallback(MarkdownLinkCallbackData data);
        public delegate void MarkdownTooltipCallback(MarkdownTooltipCallbackData data);

        private static void DefaultMarkdownTooltipCallback(MarkdownTooltipCallbackData data)
        {
            if (data.LinkData.IsImage)
            {
                ImGui.SetTooltip(data.LinkData.Link);
            }
            else
            {
                ImGui.SetTooltip($"Open in browser");
            }
        }

        public delegate MarkdownImageData MarkdownImageCallback(MarkdownLinkCallbackData data);
        public delegate void MarkdownFormalCallback(MarkdownFormatInfo formatInfo, bool start);

        public unsafe class MarkdownHeadingFormat
        {
            public ImFontPtr? Font { get; set; }
            public bool Separator { get; set; }
        }

        public static void Markdown(string markdown, MarkdownConfig mdConfig)
        {
            int linkHoverStart = -1; // we need to preserve status of link hovering between frames
            ImGuiStylePtr style = ImGui.GetStyle();
            Line line = new Line();
            Link link = new Link();
            Emphasis em = new Emphasis();
            TextRegion textRegion = new TextRegion();

            char c;
            for (int i = 0; i < markdown.Length; ++i)
            {
                c = markdown[i];               // get the character at index
                if (c == 0) 
                { 
                    break; // shouldn't happen but don't go beyond 0.
                }         

                // If we're at the beginning of the line, count any spaces
                if (line.isLeadingSpace)
                {
                    if (c == ' ')
                    {
                        ++line.leadSpaceCount;
                        continue;
                    }
                    else
                    {
                        line.isLeadingSpace = false;
                        line.lastRenderPosition = i - 1;
                        if ((c == '*') && (line.leadSpaceCount >= 2))
                        {
                            if ((markdown.Length > i + 1) && (markdown[i + 1] == ' '))    // space after '*'
                            {
                                line.isUnorderedListStart = true;
                                ++i;
                                ++line.lastRenderPosition;
                            }
                            // carry on processing as could be emphasis
                        }
                        else if (c == '#')
                        {
                            line.headingCount++;
                            bool bContinueChecking = true;
                            int j = i;
                            while (++j < markdown.Length && bContinueChecking)
                            {
                                c = markdown[j];
                                switch (c)
                                {
                                    case '#':
                                    {
                                        line.headingCount++;
                                        break;
                                    }
                                    case ' ':
                                    {
                                        line.lastRenderPosition = j - 1;
                                        i = j;
                                        line.isHeading = true;
                                        bContinueChecking = false;
                                        break;
                                    }
                                    default:
                                    {
                                        line.isHeading = false;
                                        bContinueChecking = false;
                                        break;
                                    }
                                }
                            }
                            if (line.isHeading)
                            {
                                // reset emphasis status, we do not support emphasis around headers for now
                                em = new Emphasis();
                                continue;
                            }
                        }
                    }
                }

                // Test to see if we have a link
                switch (link.state)
                {
                    case Link.LinkState.NoLink:
                    {
                        if (c == '[' && !line.isHeading) // we do not support headings with links for now
                        {
                            link.state = Link.LinkState.HasSquareBracketOpen;
                            link.text.start = i + 1;
                            if (i > 0 && markdown[i - 1] == '!')
                            {
                                link.isImage = true;
                            }
                        }

                        break;
                    }

                    case Link.LinkState.HasSquareBracketOpen:
                    {
                        if (c == ']')
                        {
                            link.state = Link.LinkState.HasSquareBrackets;
                            link.text.stop = i;
                        }

                        break;
                    }

                    case Link.LinkState.HasSquareBrackets:
                    {
                        if (c == '(')
                        {
                            link.state = Link.LinkState.HasSquareBracketsRoundBracketOpen;
                            link.url.start = i + 1;
                            link.numBracketsOpen = 1;
                        }
                        break;
                    }

                    case Link.LinkState.HasSquareBracketsRoundBracketOpen:
                    {
                        if (c == '(')
                        {
                            ++link.numBracketsOpen;
                        }
                        else if (c == ')')
                        {
                            --link.numBracketsOpen;
                        }

                        if (link.numBracketsOpen == 0)
                        {
                            // reset emphasis status, we do not support emphasis around links for now
                            em = new Emphasis();
                            // render previous line content
                            line.lineEnd = link.text.start - (link.isImage ? 2 : 1);
                            RenderLine(markdown, line, textRegion, mdConfig);
                            line.leadSpaceCount = 0;
                            link.url.stop = i;
                            line.isUnorderedListStart = false;    // the following text shouldn't have bullets
                            ImGui.SameLine(0.0f, 0.0f);
                            if (link.isImage)   // it's an image, render it.
                            {
                                bool drawnImage = false;
                                bool useLinkCallback = false;
                                if (mdConfig.ImageCallback != null)
                                {
                                    MarkdownImageData imageData = mdConfig.ImageCallback?.Invoke(new MarkdownLinkCallbackData()
                                    {
                                        Text = markdown.Substring(link.text.start, link.text.Size),
                                        Link = markdown.Substring(link.url.start, link.url.Size),
                                        IsImage = true
                                    });

                                    useLinkCallback = imageData.UseLinkCallback;
                                    if (imageData.IsValid)
                                    {
                                        ImGui.Image(imageData.UserTextureID, imageData.Size, imageData.UV0, imageData.UV1, imageData.TintColor, imageData.BorderColor);
                                        drawnImage = true;
                                    }
                                }

                                if (!drawnImage)
                                {
                                    ImGui.Text($"Image {markdown.Substring(link.url.start, link.url.Size)} not loaded");
                                }

                                if (ImGui.IsItemHovered())
                                {
                                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) && mdConfig.LinkCallback != null && useLinkCallback)
                                    {
                                        mdConfig.LinkCallback?.Invoke(new MarkdownLinkCallbackData()
                                        {
                                            Text = markdown.Substring(link.text.start, link.text.Size),
                                            Link = markdown.Substring(link.url.start, link.url.Size),
                                            IsImage = true
                                        });
                                    }
                                    if (link.text.Size > 0 && mdConfig.TooltipCallback != null)
                                    {
                                        mdConfig.TooltipCallback?.Invoke(new MarkdownTooltipCallbackData()
                                        {
                                            LinkData = new MarkdownLinkCallbackData()
                                            {
                                                Text = markdown.Substring(link.text.start, link.text.Size),
                                                Link = markdown.Substring(link.url.start, link.url.Size),
                                                IsImage = true
                                            },

                                            LinkIcon = mdConfig.LinkIcon
                                        });
                                    }
                                }
                            }
                            else                 // it's a link, render it.
                            {
                                textRegion.RenderLinkTextWrapped(markdown.Substring(link.text.start, link.text.Size), link, markdown, mdConfig, ref linkHoverStart, false);
                            }

                            ImGui.SameLine(0.0f, 0.0f);
                            link = new Link();
                            line.lastRenderPosition = i;
                        }

                        break;
                    }

                }

                // Test to see if we have emphasis styling
			    switch (em.state)
                {
                    case Emphasis.EmphasisState.None:
                    {
                        if (link.state == Link.LinkState.NoLink && !line.isHeading)
                        {
                            int next = i + 1;
                            int prev = i - 1;
                            if ((c == '*' || c == '_')
                                && (i == line.lineStart
                                    || markdown[prev] == ' '
                                    || markdown[prev] == '\t') // empasis must be preceded by whitespace or line start
                                && markdown.Length > next // emphasis must precede non-whitespace
                                && markdown[next] != ' '
                                && markdown[next] != '\n'
                                && markdown[next] != '\t')
                            {
                                em.state = Emphasis.EmphasisState.Left;
                                em.sym = c;
                                em.text.start = i;
                                line.emphasisCount = 1;
                                continue;
                            }
                        }

                        break;
                    }

                    case Emphasis.EmphasisState.Left:
                    {
                        if (em.sym == c)
                        {
                            ++line.emphasisCount;
                            continue;
                        }
                        else
                        {
                            em.text.start = i;
                            em.state = Emphasis.EmphasisState.Middle;
                        }

                        break;
                    }

                    case Emphasis.EmphasisState.Middle:
                    {
                        if (em.sym == c)
                        {
                            em.state = Emphasis.EmphasisState.Right;
                            em.text.stop = i;
                            goto case Emphasis.EmphasisState.Right;
                        }
                        else
                        {
                            break;
                        }
                    }

                    case Emphasis.EmphasisState.Right:
                    {
                        if (em.sym == c)
                        {
                            if (line.emphasisCount < 3 && (i - em.text.stop + 1 == line.emphasisCount))
                            {
                                // render text up to emphasis
                                int lineEnd = em.text.start - line.emphasisCount;
                                if (lineEnd > line.lineStart)
                                {
                                    line.lineEnd = lineEnd;
                                    RenderLine(markdown, line, textRegion, mdConfig);
                                    ImGui.SameLine(0.0f, 0.0f);
                                    line.isUnorderedListStart = false;
                                    line.leadSpaceCount = 0;
                                }
                                line.isEmphasis = true;
                                line.lastRenderPosition = em.text.start - 1;
                                line.lineStart = em.text.start;
                                line.lineEnd = em.text.stop;
                                RenderLine(markdown, line, textRegion, mdConfig);
                                ImGui.SameLine(0.0f, 0.0f);
                                line.isEmphasis = false;
                                line.lastRenderPosition = i;
                                em = new Emphasis();
                            }
                            continue;
                        }
                        else
                        {
                            em.state = Emphasis.EmphasisState.None;
                            // render text up to here
                            int start = em.text.start - line.emphasisCount;
                            if (start < line.lineStart)
                            {
                                line.lineEnd = line.lineStart;
                                line.lineStart = start;
                                line.lastRenderPosition = start - 1;
                                RenderLine(markdown, line, textRegion, mdConfig);
                                line.lineStart = line.lineEnd;
                                line.lastRenderPosition = line.lineStart - 1;
                            }
                        }

                        break;
                    }
                }

                // handle end of line (render)
                if (c == '\n')
                {
                    // first check if the line is a horizontal rule
                    line.lineEnd = i;
                    if (em.state == Emphasis.EmphasisState.Middle && line.emphasisCount >= 3 &&
                        (line.lineStart + line.emphasisCount) == i)
                    {
                        ImGui.Separator();
                    }
                    else
                    {
                        // render the line: multiline emphasis requires a complex implementation so not supporting
                        RenderLine(markdown, line, textRegion, mdConfig);
                    }

                    // reset the line and emphasis state
                    line = new Line();
                    em = new Emphasis();

                    line.lineStart = i + 1;
                    line.lastRenderPosition = i;

                    textRegion.ResetIndent();

                    // reset the link
                    link = new Link();
                }
            }

            if (em.state == Emphasis.EmphasisState.Left && line.emphasisCount >= 3)
            {
                ImGui.Separator();
            }
            else
            {
                // render any remaining text if last char wasn't 0
                if (markdown.Length > 0 && line.lineStart < (int)markdown.Length && markdown[line.lineStart] != 0)
                {
                    // handle both null terminated and non null terminated strings
                    line.lineEnd = markdown.Length;
                    if (0 == markdown[line.lineEnd - 1])
                    {
                        --line.lineEnd;
                    }

                    RenderLine(markdown, line, textRegion, mdConfig);
                }
            }

            textRegion?.ResetIndent();
        }

        private class TextRegion
        {
            public float indentX;

            public void Free() => this.ResetIndent();

            // ImGui::TextWrapped will wrap at the starting position
            // so to work around this we render using our own wrapping for the first line
            public void RenderTextWrapped(string text, bool bIndentToHere = false )
            {
                float scale = ImGui.GetIO().FontGlobalScale;
                float widthLeft = ImGui.GetContentRegionAvail().X;
                int indexTo = TextWrapFromFont(ImGui.GetFont(), scale, text, widthLeft, out string leftovers);
                if (indexTo == -1)
                {
                    indexTo = text.Length;
                }

                ImGui.TextUnformatted(text[..indexTo]);
                if(bIndentToHere)
                {
                    float indentNeeded = ImGui.GetContentRegionAvail().X - widthLeft;
                    if(indentNeeded > 0)
                    {
                        ImGui.Indent(indentNeeded);
                        indentX += indentNeeded;
                    }
                }

                widthLeft = ImGui.GetContentRegionAvail().X;
                while(indexTo > 0)
                {
                    text = leftovers;
                    if (text.Length != 0 && text[0] == ' ' ) 
                    {
                        text = text[1..]; // skip a space at start of line
                    }

                    leftovers = ImGui.GetFont().CalcWordWrapPositionA(scale, text, widthLeft);
                    indexTo = text.Length - leftovers.Length;
                    if (indexTo > 0)
                    {
                        ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(text[..indexTo]));
                    }
                    else
                    {
                        ImGui.TextUnformatted(text);
                    }
                }
            }

            public void RenderListTextWrapped(string text)
            {
                ImGui.Bullet();
                ImGui.SameLine();
                this.RenderTextWrapped(text, true);
            }

            public bool RenderLinkText(string text, Link link, string markdown, MarkdownConfig mdConfig, ref int linkHoverStart)
            {
                MarkdownFormatInfo formatInfo = new MarkdownFormatInfo();
                formatInfo.Config = mdConfig;
                formatInfo.Type = MarkdownFormatType.Link;
                mdConfig.FormatCallback?.Invoke(formatInfo, true);
                ImGui.PushTextWrapPos(-1.0f);
                ImGui.TextUnformatted(text);
                ImGui.PopTextWrapPos();

                bool bThisItemHovered = ImGui.IsItemHovered();
                if (bThisItemHovered)
                {
                    linkHoverStart = link.text.start;
                }

                bool bHovered = bThisItemHovered && linkHoverStart != -1 && linkHoverStart == link.text.start;
                formatInfo.ItemHovered = bHovered;
                mdConfig.FormatCallback(formatInfo, false);

                if (bHovered)
                {
                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) && mdConfig.LinkCallback != null)
                    {
                        mdConfig.LinkCallback.Invoke(new MarkdownLinkCallbackData() 
                        {
                            Text = markdown.Substring(link.text.start, link.text.Size),
                            Link = markdown.Substring(link.url.start, link.url.Size),
                            IsImage = false
                        });
                    }

                    mdConfig.TooltipCallback?.Invoke(new MarkdownTooltipCallbackData() 
                    {
                        LinkData = new MarkdownLinkCallbackData()
                        {
                            Text = markdown.Substring(link.text.start, link.text.Size),
                            Link = markdown.Substring(link.url.start, link.url.Size),
                            IsImage = false
                        },

                        LinkIcon = mdConfig.LinkIcon
                    });
                }

                return bThisItemHovered;
            }

            public void RenderLinkTextWrapped(string text, Link link, string markdown, MarkdownConfig mdConfig, ref int linkHoverStart, bool bIndentToHere = false)
            {
                float scale = ImGui.GetIO().FontGlobalScale;
                float widthLeft = ImGui.GetContentRegionAvail().X;
                string endLine = text;
                int endLineIndex = text.Length;
                if (widthLeft > 0.0f)
                {
                    endLineIndex = TextWrapFromFont(ImGui.GetFont(), scale, text, widthLeft, out endLine);
                }

                if (endLineIndex >= 0 && endLineIndex < text.Length)
                {
                    if (IsCharInsideWord(text[endLineIndex]))
                    {
                        // see if we can do a better cut.
                        float widthNextLine = ImGui.GetContentRegionAvail().X;
                        int endNextLineIndex = TextWrapFromFont(ImGui.GetFont(), scale, text, widthNextLine, out _);
                        if (endNextLineIndex >= text.Length - 1 || (endNextLineIndex <= text.Length - 1 && !IsCharInsideWord(text[endNextLineIndex])))
                        {
                            // can possibly do better if go to next line
                            endLine = text;
                        }
                    }
                }

                if (endLineIndex == -1)
                {
                    endLineIndex = text.Length;
                }

                bool bHovered = RenderLinkText(text[..endLineIndex], link, markdown, mdConfig, ref linkHoverStart);
                if (bIndentToHere)
                {
                    float indentNeeded = ImGui.GetContentRegionAvail().X - widthLeft;
                    if (indentNeeded > 0)
                    {
                        ImGui.Indent(indentNeeded);
                        indentX += indentNeeded;
                    }
                }

                widthLeft = ImGui.GetContentRegionAvail().X;
                while (endLineIndex < text.Length)
                {
                    text = endLine;
                    if (text[0] == ' ') 
                    { 
                        text = text[1..];  // skip a space at start of line
                    }

                    endLineIndex = TextWrapFromFont(ImGui.GetFont(), scale, text, widthLeft, out endLine);
                    if (text.Equals(endLine))
                    {
                        ++endLineIndex;
                    }

                    bool bThisLineHovered = this.RenderLinkText(text[..endLineIndex], link, markdown, mdConfig, ref linkHoverStart);
                    bHovered = bHovered || bThisLineHovered;
                }

                if (!bHovered && linkHoverStart != -1 && linkHoverStart == link.text.start)
                {
                    linkHoverStart = -1;
                }
            }

            public void ResetIndent()
            {
                if (indentX > 0.0f)
                {
                    ImGui.Unindent(indentX);
                }

                indentX = 0.0f;
            }
        }

        private class Line
        {
            public bool isHeading = false;
            public bool isEmphasis = false;
            public bool isUnorderedListStart = false;
            public bool isLeadingSpace = true;     // spaces at start of line
            public int leadSpaceCount = 0;
            public int headingCount = 0;
            public int emphasisCount = 0;
            public int lineStart = 0;
            public int lineEnd = 0;
            public int lastRenderPosition = 0;     // lines may get rendered in multiple pieces
        }

        private class TextBlock
        {
            public int start = 0;
            public int stop = 0;
            public int Size => this.stop - this.start;
        }

        private class Link
        {
            public enum LinkState
            {
                NoLink,
                HasSquareBracketOpen,
                HasSquareBrackets,
                HasSquareBracketsRoundBracketOpen
            }

            public LinkState state = LinkState.NoLink;
            public TextBlock text = new TextBlock();
            public TextBlock url = new TextBlock();
            public bool isImage;
            public int numBracketsOpen = 0;
        }

        private class Emphasis
        {
            public enum EmphasisState
            {
                None,
                Left,
                Middle,
                Right
            }

            public EmphasisState state = EmphasisState.None;
            public TextBlock text = new TextBlock();
            public char sym;
        }

        private static void UnderLine(uint clr)
        {
            Vector2 min = ImGui.GetItemRectMin();
            Vector2 max = ImGui.GetItemRectMax();
            min.Y = max.Y;
            ImGui.GetWindowDrawList().AddLine(min, max, clr, 1.0f);
        }

        private static void RenderLine(string markdown, Line line, TextRegion textRegion, MarkdownConfig mdConfig)
        {
            // indent
            int indentStart = 0;
            if(line.isUnorderedListStart)    // ImGui unordered list render always adds one indent
            { 
                indentStart = 1; 
            }

            for(int j = indentStart; j < line.leadSpaceCount / 2; ++j)    // add indents
            {
                ImGui.Indent();
            }

            // render
            MarkdownFormatInfo formatInfo = new MarkdownFormatInfo();
            formatInfo.Config = mdConfig;
            int textStart = line.lastRenderPosition + 1;
            int textSize = line.lineEnd - textStart;
            if (line.isUnorderedListStart)    // render unordered list
            {
                formatInfo.Type = MarkdownFormatType.UnorderedList;
                mdConfig.FormatCallback?.Invoke(formatInfo, true);
                textRegion.RenderListTextWrapped(markdown.Substring(textStart + 1, textSize - 1));
            }
            else if (line.isHeading)          // render heading
            {
                formatInfo.Level = line.headingCount;
                formatInfo.Type = MarkdownFormatType.Heading;
                mdConfig.FormatCallback?.Invoke(formatInfo, true);
                textRegion.RenderTextWrapped(markdown.Substring(textStart + 1, textSize - 1));
            }
            else if (line.isEmphasis)         // render emphasis
            {
                formatInfo.Level = line.emphasisCount;
                formatInfo.Type = MarkdownFormatType.Emphasis;
                mdConfig.FormatCallback?.Invoke(formatInfo, true);
                textRegion.RenderTextWrapped(markdown.Substring(textStart, textSize));
            }
            else                                // render a normal paragraph chunk
            {
                formatInfo.Type = MarkdownFormatType.NormalText;
                mdConfig.FormatCallback?.Invoke(formatInfo, true);
                textRegion.RenderTextWrapped(markdown.Substring(textStart, textSize));
            }

            mdConfig.FormatCallback?.Invoke(formatInfo, false);

            // unindent
            for (int j = indentStart; j < line.leadSpaceCount / 2; ++j)
            {
                ImGui.Unindent();
            }
        }

        private static bool IsCharInsideWord(char c) => c is not ' ' and not '.' and not ',' and not ';' and not '!' and not '?' and not '\"';
        private static int TextWrapFromFont(ImFontPtr font, float scale, string text, float wrapWidth, out string leftovers)
        {
            leftovers = font.CalcWordWrapPositionA(scale, text, wrapWidth);
            return string.IsNullOrEmpty(leftovers) ? -1 : text.Length - leftovers.Length;
        }

        private static void DefaultMarkdownFormatCallback(MarkdownFormatInfo markdownFormatInfo, bool start)
        {
            switch (markdownFormatInfo.Type)
            {
                case MarkdownFormatType.NormalText:
                {
                    break;
                }

                case MarkdownFormatType.Emphasis:
                {
                    MarkdownHeadingFormat fmt;
                    // default styling for emphasis uses last headingFormats - for your own styling
                    // implement EMPHASIS in your formatCallback
                    if (markdownFormatInfo.Level == 1)
                    {
                        // normal emphasis
                        if (start)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
                        }
                        else
                        {
                            ImGui.PopStyleColor();
                        }
                    }
                    else
                    {
                        // strong emphasis
                        fmt = markdownFormatInfo.Config.HeadingFormats[MarkdownConfig.NumHeadings - 1];
                        if (start)
                        {
                            if (fmt.Font != null)
                            {
                                ImGui.PushFont(fmt.Font.Value);
                            }
                        }
                        else
                        {
                            if (fmt.Font != null)
                            {
                                ImGui.PopFont();
                            }
                        }
                    }

                    break;
                }

                case MarkdownFormatType.Heading:
                {
                    MarkdownHeadingFormat fmt = markdownFormatInfo.Level > MarkdownConfig.NumHeadings
                        ? markdownFormatInfo.Config.HeadingFormats[MarkdownConfig.NumHeadings - 1]
                        : markdownFormatInfo.Config.HeadingFormats[markdownFormatInfo.Level - 1];

                    if (start)
                    {
                        if (fmt.Font != null)
                        {
                            ImGui.PushFont(fmt.Font.Value);
                        }

                        ImGui.NewLine();
                    }
                    else
                    {
                        if (fmt.Separator)
                        {
                            ImGui.Separator();
                            ImGui.NewLine();
                        }
                        else
                        {
                            ImGui.NewLine();
                        }
                        if (fmt.Font != null)
                        {
                            ImGui.PopFont();
                        }
                    }

                    break;
                }

                case MarkdownFormatType.UnorderedList:
                {
                    break;
                }

                case MarkdownFormatType.Link:
                {
                    if (start)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.ButtonHovered));
                    }
                    else
                    {
                        ImGui.PopStyleColor();
                        if (markdownFormatInfo.ItemHovered)
                        {
                            UnderLine(ImGui.GetColorU32(ImGuiCol.ButtonHovered));
                        }
                        else
                        {
                            UnderLine(ImGui.GetColorU32(ImGuiCol.Button));
                        }
                    }

                    break;
                }
            }
        }

        public static Vector2 CalcImageToImGuiContentRegion(float imgW, float imgH)
        {
            Vector2 contentAvail = ImGui.GetContentRegionAvail();
            if (imgW > contentAvail.X)
            {
                float ar = imgH / imgW;
                return new Vector2(contentAvail.X, contentAvail.X * ar);
            }
            else
            {
                return new Vector2(imgW, imgH);
            }
        }
    }
}
