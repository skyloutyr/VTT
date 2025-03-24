namespace VTT.Render.Chat
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Text;
    using VTT.Asset;
    using VTT.Control;
    using VTT.GL;
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
                if (cb.Type == ChatBlockType.Image && icw.Height > 24) // Images can try to go inline, but if they span past the line height limit of 24 they need to go into a newline
                {
                    if (words.Count != 0) // First force eject all current words into a new line
                    {
                        ImCachedLine icl = new ImCachedLine(words.ToArray());
                        lines.Add(icl);
                        words.Clear();
                    }

                    // Then add a line of only our words
                    lines.Add(new ImCachedLine(new ImCachedWord[] { icw }));
                    cW = 0;
                    return; // Do NOT proceed further, all done
                }

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
                if (cb.Type == ChatBlockType.Image)
                {
                    AddWord(string.Empty, cb); // Just insert a blank if we are image, AddWord will take care of images for us
                    continue;
                }

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
                    if (icw.Owner.Type == ChatBlockType.Image)
                    {
                        ret += icw.Owner.Text;
                    }
                    else
                    {
                        ret += icw.Text;
                        if (icw.IsExpression)
                        {
                            ret += $"({RollSyntaxRegex.Replace(icw.Owner.Tooltip, x => $"{x.Groups[1].Value}d{x.Groups[2].Value}[")})";
                        }
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
            bool needReCache = false;

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
                        if (icw.Owner.Type == ChatBlockType.Image)
                        {
                            Asset a = null;
                            AssetPreview ap = null;
                            bool isAssetRef = Guid.TryParse(icw.Owner.Text, out Guid assetId);
                            AssetStatus imgStatus = isAssetRef 
                                ? Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(assetId, AssetType.Texture, out a) 
                                : Client.Instance.AssetManager.ClientAssetLibrary.WebPictures.Get(icw.Owner.Text, AssetType.Texture, out ap);

                            bool haveTextureReadyHere = false;
                            if (isAssetRef && a != null && a.Texture != null && a.Texture.glReady)
                            {
                                Texture t = a.Texture.GetOrCreateGLTexture(true, out VTT.Asset.Glb.TextureAnimation animationData);
                                haveTextureReadyHere = t.IsAsyncReady;
                            }

                            // Check for non return or the flags in case of us simply lagging behind 1 frame
                            if (imgStatus != icw.ImgStatusOnConstruct && (imgStatus != AssetStatus.Return || (isAssetRef ? haveTextureReadyHere : ap != null && ap.GLTex != null && ap.GLTex.IsAsyncReady)))
                            {
                                // If we are here some result has arrived to us from the web. Must invalidate cache. This frame just dummy and flag for cache invalidation
                                ImGui.Dummy(new Vector2(24, 24));
                                needReCache = true;
                            }
                            else
                            {
                                IntPtr imgTexture;
                                Vector2 imgSize;
                                Vector2 imgSt;
                                Vector2 imgUv;
                                switch (icw.ImgStatusOnConstruct)
                                {
                                    case AssetStatus.Return:
                                    {
                                        if (isAssetRef 
                                            ? a == null || a.Texture == null || !a.Texture.glReady
                                            : ap == null || ap.GLTex == null || !ap.GLTex.IsAsyncReady) // Impossible but sanity check in case of race condidion
                                        {
                                            goto case AssetStatus.Await; 
                                        }

                                        if (isAssetRef)
                                        {
                                            Texture t = a.Texture.GetOrCreateGLTexture(true, out VTT.Asset.Glb.TextureAnimation animationData);
                                            imgTexture = t;
                                            if (animationData != null && animationData.Frames.Length > 1)
                                            {
                                                VTT.Asset.Glb.TextureAnimation.Frame frame = animationData.FindFrameForIndex(double.NaN);
                                                Vector2 tSzV2 = new Vector2(t.Size.Width, t.Size.Height);
                                                imgSize = new Vector2(frame.Location.Width, frame.Location.Height) * tSzV2;
                                                imgSt = new Vector2(frame.Location.Left, frame.Location.Top);
                                                imgUv = new Vector2(frame.Location.Right, frame.Location.Bottom);
                                            }
                                            else
                                            {
                                                imgSize = new Vector2(t.Size.Width, t.Size.Height);
                                                imgSt = Vector2.Zero;
                                                imgUv = Vector2.One;
                                            }
                                        }
                                        else
                                        {
                                            imgTexture = ap.GLTex;
                                            if (ap.IsAnimated && ap.FramesTotalDelay > 0)
                                            {
                                                float tW = ap.GLTex.Size.Width;
                                                float tH = ap.GLTex.Size.Height;
                                                imgSize = new Vector2(tW, tH);
                                                AssetPreview.FrameData frame = ap.GetCurrentFrame((int)(((Client.Instance.Frontend.UpdatesExisted) & int.MaxValue) * (100f / 60f)));
                                                float progress = (float)frame.TotalDurationToHere / ap.FramesTotalDelay;
                                                float sS = frame.X / tW;
                                                float sE = sS + (frame.Width / tW);
                                                float tS = frame.Y / tH;
                                                float tE = tS + (frame.Height / tH);
                                                imgSt = new Vector2(sS, tS);
                                                imgUv = new Vector2(sE, tE);
                                            }
                                            else
                                            {
                                                imgSize = new Vector2(ap.GLTex.Size.Width, ap.GLTex.Size.Height);
                                                imgSt = Vector2.Zero;
                                                imgUv = Vector2.One;
                                            }
                                        }

                                        break;
                                    }

                                    case AssetStatus.NoAsset:
                                    case AssetStatus.Error:
                                    {
                                        imgTexture = Client.Instance.Frontend.Renderer.GuiRenderer.NoImageIcon;
                                        imgSize = new Vector2(24, 24); // Blank for errors, have no idea of the image's size (will be 24/24 either way)
                                        imgSt = Vector2.Zero;
                                        imgUv = Vector2.One;
                                        break;
                                    }

                                    case AssetStatus.Await:
                                    default:
                                    {
                                        int frame = (int)((int)Client.Instance.Frontend.UpdatesExisted % 90 / 90.0f * Client.Instance.Frontend.Renderer.GuiRenderer.LoadingSpinnerFrames);
                                        float texelIndexStart = (float)frame / Client.Instance.Frontend.Renderer.GuiRenderer.LoadingSpinnerFrames;
                                        float texelSize = 1f / Client.Instance.Frontend.Renderer.GuiRenderer.LoadingSpinnerFrames;
                                        imgTexture = Client.Instance.Frontend.Renderer.GuiRenderer.LoadingSpinner;
                                        imgSize = new Vector2(24, 24);
                                        imgSt = new Vector2(texelIndexStart, 0);
                                        imgUv = new Vector2(texelIndexStart + texelSize, 1);
                                        break;
                                    }
                                }

                                imgSize.X = MathF.Min(320, imgSize.X);
                                imgSize.Y = MathF.Min(320, imgSize.Y);

                                ImGui.Image(imgTexture, imgSize, imgSt, imgUv);
                            }
                        }
                        else
                        {
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

            if (needReCache)
            {
                this.Container.InvalidateCache();
            }
        }
    }
}
