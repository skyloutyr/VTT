namespace VTT.Control
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Text;
    using VTT.Asset;
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Render;
    using VTT.Render.Chat;
    using VTT.Render.Gui;
    using VTT.Util;

    public class ChatLine
    {
        public string Sender { get; set; }

        public string SenderDisplayName { get; set; }
        public string DestDisplayName { get; set; }
        public Guid SenderID { get; set; }
        public Guid DestID { get; set; } = Guid.Empty;
        public Guid PortraitID { get; set; } = Guid.Empty;
        public DateTime SendTime { get; set; } = DateTime.UnixEpoch;

        public Color SenderColor { get => senderColor.Argb() == 0 ? Extensions.FromAbgr(ImGui.GetColorU32(ImGuiCol.Text)) : senderColor; set => senderColor = value; }
        public Color DestColor { get => destColor.Argb() == 0 ? Extensions.FromAbgr(ImGui.GetColorU32(ImGuiCol.Text)) : destColor; set => destColor = value; }
        public List<ChatBlock> Blocks { get; set; } = new List<ChatBlock>();
        public EmojiReactions Reactions { get; set; } = new EmojiReactions();

        public ChatRendererBase Renderer { get; set; }
        public RenderType Type { get; set; }

        public int Index { get; set; }

        private bool _cached;
        private float _cachedHeight;
        private AssetPreviewReference _portraitTex;
        private Color senderColor;
        private Color destColor;

        public bool CanSee(Guid id) => this.DestID.Equals(Guid.Empty) || this.SenderID.Equals(id) || this.DestID.Equals(id);

        public string GetFullText()
        {
            StringBuilder sb = new StringBuilder();
            foreach (ChatBlock cb in this.Blocks)
            {
                sb.Append(cb.Text);
            }

            return sb.ToString();
        }

        public ChatBlock PopChatBlock(bool notPersistOnly)
        {
            if (this.Blocks.Count > 0)
            {
                for (int i = this.Blocks.Count - 1; i >= 0; --i)
                {
                    ChatBlock cb = this.Blocks[i];
                    if (notPersistOnly)
                    {
                        if (cb.DoNotPersist)
                        {
                            this.Blocks.RemoveAt(i);
                            return cb;
                        }
                    }
                    else
                    {
                        this.Blocks.RemoveAt(i);
                        return cb;
                    }

                }
            }

            return null;
        }

        public ChatBlock CreateContextBlock(string text, string tt = "", ChatBlockType type = ChatBlockType.Text, ChatBlockExpressionRollContents rollConents = ChatBlockExpressionRollContents.None) => new ChatBlock() { Color = this.Blocks.Count > 0 ? this.Blocks[^1].Color : 0, Text = text, Tooltip = tt, Type = type, RollContents = rollConents };

        public bool TryGetBlockAt(int index, out ChatBlock block)
        {
            block = index < this.Blocks.Count ? this.Blocks[index] : null;
            return block != null;
        }

        public string GetBlockTextOrEmpty(int index)
        {
            if (this.TryGetBlockAt(index, out ChatBlock cb))
            {
                return cb.Text;
            }

            return " "; // Can't be string.Empty due to ImGui not accepting empty strings
        }

        public uint GetBlockColorOr(int index, uint defaultValue) => this.TryGetBlockAt(index, out ChatBlock cb) ? cb.Color : defaultValue;

        public bool ImRender(Vector2 winSize, float h, int idx, SimpleLanguage lang)
        {
            if (!this._cached)
            {
                this.BuildCache(winSize);
            }

            if (Client.Instance.IsAdmin || this.CanSee(Client.Instance.ID))
            {
                float scrollMin = ImGui.GetScrollY();
                float scrollMax = scrollMin + h;
                bool haveReactions = this.Reactions.Total > 0;
                int spacing = haveReactions ? 54 : 30;
                if (ImGui.GetCursorPosY() + this._cachedHeight + spacing < scrollMin)
                {
                    ImGui.Dummy(new Vector2(1, this._cachedHeight + spacing));
                    return true;
                }

                if (scrollMax > float.Epsilon && ImGui.GetCursorPosY() > scrollMax)
                {
                    ImGui.Dummy(new Vector2(1, this._cachedHeight + spacing));
                    return true;
                }

                Vector2 cNow = ImGui.GetCursorPos();
                unsafe
                {
                    ImGui.Image(Client.Instance.Frontend.Renderer.White, new Vector2(winSize.X, 24), Vector2.Zero, Vector2.One, *ImGui.GetStyleColorVec4(ImGuiCol.WindowBg) * new Vector4(0.85f, 0.85f, 0.85f, 1f));
                }

                ImGui.SetCursorPos(cNow);

                Vector2 sV = ImGui.GetCursorScreenPos();
                Vector2 avatarSize = new Vector2(24, 24);
                if (!this.PortraitID.Equals(Guid.Empty))
                {
                    if (this._portraitTex == null)
                    {
                        this._portraitTex = new AssetPreviewReference(null, false);
                        Map m = Client.Instance.CurrentMap;
                        if (m != null)
                        {
                            if (m.GetObject(this.PortraitID, out MapObject mo))
                            {
                                AssetStatus a = Client.Instance.AssetManager.ClientAssetLibrary.Portraits.Get(mo.AssetID, AssetType.Model, out AssetPreview ap);
                                if (a == AssetStatus.Return)
                                {
                                    this._portraitTex.preview = ap;
                                    this._portraitTex.ready = true;
                                }
                            }
                        }
                    }

                    if (this._portraitTex != null && this._portraitTex.ready)
                    {
                        Texture glTex = this._portraitTex.preview.GetGLTexture();
                        if (glTex != null && glTex.IsAsyncReady)
                        {
                            if (this._portraitTex.preview.IsAnimated)
                            {
                                float tW = glTex.Size.Width;
                                float tH = glTex.Size.Height;
                                AssetPreview.FrameData frame = this._portraitTex.preview.GetCurrentFrame((int)Client.Instance.Frontend.UpdatesExisted);
                                float sS = frame.X / tW;
                                float sE = sS + (frame.Width / tW);
                                float tS = frame.Y / tH;
                                float tE = tS + (frame.Height / tH);
                                ImGui.Image(glTex, avatarSize, new Vector2(sS, tS), new Vector2(sE, tE));
                            }
                            else
                            {
                                ImGui.Image(glTex, avatarSize);
                            }

                            ImGui.SameLine();
                        }
                    }
                }
                else
                {
                    AddAvatar(ImGui.GetWindowDrawList(), this.SenderID);
                    ImGui.SameLine();
                }

                if (!this.SendTime.Equals(DateTime.UnixEpoch))
                {
                    string time = this.SendTime.ToString("HH:mm:ss");
                    float ocpx = ImGui.GetCursorPosX();
                    ImGui.SetCursorPosX(winSize.X - 12 - ImGui.CalcTextSize(time).X);
                    ImGui.Text(time);
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(this.SendTime.ToLongDateString() + "\n" + this.SendTime.ToLongTimeString());
                    }

                    ImGui.SameLine();
                    ImGui.SetCursorPosX(ocpx);
                }

                cNow = ImGui.GetCursorPos();
                ImGui.Dummy(new Vector2(1, 1));
                ImGui.SetCursorPos(cNow + Vector2.One);
                ImGui.PushStyleColor(ImGuiCol.Text, ColorAbgr.Black);
                ImGui.TextUnformatted(this.SenderDisplayName);
                ImGui.PopStyleColor();
                ImGui.SetCursorPos(cNow);
                ImGui.PushStyleColor(ImGuiCol.Text, this.SenderColor.Abgr());
                ImGui.TextUnformatted(this.SenderDisplayName);
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.Text("->");
                ImGui.SameLine();
                if (!this.DestID.IsEmpty())
                {
                    AddAvatar(ImGui.GetWindowDrawList(), this.DestID);
                    ImGui.SameLine();
                }

                cNow = ImGui.GetCursorPos();
                ImGui.Dummy(new Vector2(1, 1));
                ImGui.SetCursorPos(cNow + Vector2.One);
                ImGui.PushStyleColor(ImGuiCol.Text, ColorAbgr.Black);
                ImGui.TextUnformatted(string.IsNullOrEmpty(this.DestDisplayName) ? Client.Instance.Lang.Translate("chat.all") : this.DestDisplayName);
                ImGui.PopStyleColor();
                ImGui.SetCursorPos(cNow);
                ImGui.PushStyleColor(ImGuiCol.Text, this.DestColor.Abgr());
                ImGui.TextUnformatted(string.IsNullOrEmpty(this.DestDisplayName) ? Client.Instance.Lang.Translate("chat.all") : this.DestDisplayName);
                ImGui.PopStyleColor();
                float eY = ImGui.GetCursorScreenPos().Y;
                ImGui.SameLine();
                Vector2 eV = new Vector2(ImGui.GetCursorScreenPos().X, eY);
                ImGui.Dummy(new Vector2(0, 24));
                if (ImGui.IsMouseHoveringRect(sV, eV))
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted(this.Sender);
                    if (Client.Instance.Frontend.Renderer.AvatarLibrary.ClientImages.TryGetValue(this.SenderID, out (Texture, bool) val) && val.Item2)
                    {
                        ImGui.Image(val.Item1, new Vector2(32, 32));
                    }

                    ImGui.EndTooltip();
                }

                this.Renderer?.Render(this.SenderID, this.SenderColor.Abgr()); 
                
                if (ImGui.BeginPopupContextItem("chat_line_popup_" + idx))
                {
                    if (ImGui.MenuItem(lang.Translate("ui.chat.copy")))
                    {
                        ImGui.SetClipboardText($"{this.SendTime} {this.SenderDisplayName}: " + this.Renderer?.ProvideTextForClipboard(this.SendTime, this.SenderDisplayName, lang) ?? " ");
                    }

                    ImGui.EndPopup();
                }

                if (ImGui.IsMouseHoveringRect(sV, new Vector2(ImGui.GetCursorScreenPos().X + 350, ImGui.GetCursorScreenPos().Y)) && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    ImGui.OpenPopup("chat_line_popup_" + idx);
                }

                GuiRenderer uiRoot = GuiRenderer.Instance;
                PingRenderer pr = Client.Instance.Frontend.Renderer.PingRenderer;

                ImDrawListPtr drawList = ImGui.GetWindowDrawList();
                if (haveReactions)
                {
                    cNow = ImGui.GetCursorScreenPos() + new Vector2(0, 4);
                    ImGui.Dummy(new Vector2(1, 26));
                    int penX = 0;
                    for (int i = 0; i < 12; ++i)
                    {
                        Vector2 locNow = cNow + new Vector2(penX, 0);
                        int cnt = this.Reactions.GetReactions(i, out List<Guid> reacters);
                        if (cnt > 0)
                        {
                            string txt = cnt.ToString();
                            Vector2 tSize = ImGui.CalcTextSize(txt);
                            Vector2 sSize = new Vector2(20 + 4 + tSize.X + 4, 20);
                            bool hover = ImGui.IsMouseHoveringRect(locNow, locNow + sSize);
                            drawList.AddRect(locNow, locNow + sSize, ImGui.GetColorU32(hover ? ImGuiCol.ButtonHovered : ImGuiCol.Border), 16f);
                            drawList.AddImageRounded(pr.EmojiTextures[i], locNow, locNow + new Vector2(20, 20), Vector2.Zero, Vector2.One, 0xffffffff, 16f);
                            drawList.AddText(locNow + new Vector2(24, 0), ImGui.GetColorU32(ImGuiCol.Text), txt);
                            if (hover)
                            {
                                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                                {
                                    bool isReacter = reacters.Contains(Client.Instance.ID);
                                    new PacketChatReaction() { Reacter = Client.Instance.ID, EmojiIndex = i, CLIndex = idx, IsAddition = !isReacter }.Send();
                                }

                                ImGui.BeginTooltip();
                                int reactCnt = 0;
                                foreach (Guid id in reacters)
                                { 
                                    if (Client.Instance.ClientInfos.TryGetValue(id, out ClientInfo ci))
                                    {
                                        ImGui.PushStyleColor(ImGuiCol.TextDisabled, ci.Color.Abgr());
                                        ImGui.TextDisabled(ci.Name);
                                        ImGui.PopStyleColor();
                                        reactCnt += 1;
                                    }

                                    if (reactCnt >= 12)
                                    {
                                        ImGui.TextDisabled(lang.Translate("ui.chat.reacters.more", reacters.Count - 12));
                                        break;
                                    }
                                }

                                ImGui.EndTooltip();
                            }

                            penX += (int)sSize.X + 4;
                        }
                    }
                }

                cNow = ImGui.GetCursorScreenPos() + new Vector2(winSize.X - 40, -20);
                bool mouseHoveringReactPopupRect = false;
                string reactPopupId = "chat_line_react_popup_" + idx;
                if (mouseHoveringReactPopupRect = ImGui.IsMouseHoveringRect(cNow, cNow + new Vector2(20, 20)))
                {
                    uint dotColor = ImGui.GetColorU32(ImGuiCol.Border);
                    ImGui.GetWindowDrawList().AddCircleFilled(cNow + new Vector2(4, 15), 3, dotColor);
                    ImGui.GetWindowDrawList().AddCircleFilled(cNow + new Vector2(12, 15), 3, dotColor);
                    ImGui.GetWindowDrawList().AddCircleFilled(cNow + new Vector2(20, 15), 3, dotColor);
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        ImGui.OpenPopup(reactPopupId);
                    }
                }

                if ((mouseHoveringReactPopupRect || ImGui.IsPopupOpen(reactPopupId)) && ImGui.BeginPopupContextItem(reactPopupId, ImGuiPopupFlags.MouseButtonLeft))
                {
                    for (int i = 0; i < 12; ++i)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Border, 0x00000000);
                        ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000);
                        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0x00000000);
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0x00000000);
                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
                        if (ImGui.ImageButton($"chat_line_react_popup_{idx}_react_{i}", pr.EmojiTextures[i], new Vector2(18, 18)))
                        {
                            new PacketChatReaction() { Reacter = Client.Instance.ID, EmojiIndex = i, CLIndex = idx, IsAddition = true }.Send();
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.PopStyleVar();
                        ImGui.PopStyleColor();
                        ImGui.PopStyleColor();
                        ImGui.PopStyleColor();
                        ImGui.PopStyleColor();

                        ImGui.SameLine();
                    }

                    ImGui.EndPopup();
                }

                return true;
            }

            return false;
        }

        public void InvalidateCache()
        {
            this._cached = false;
            this.Renderer?.ClearCache();
        }

        private const uint abgrAdmin = 0xff00007d;
        private const uint abgrObserver = 0xffffab73;

        private static readonly Vector2 avatarSize = new Vector2(24, 24);
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value", Justification = "VS 2022 Community bug - erroneous warning generated")]
        public static bool AddAvatar(ImDrawListPtr drawList, Guid clientId, bool injectDummy = true, bool drawMissing = true)
        {
            IntPtr avatarImageIndex = IntPtr.Zero;
            bool ret = true;
            uint avatarOverrideColor = 0xffffffff;
            if (Client.Instance.Frontend.Renderer.AvatarLibrary.ClientImages.TryGetValue(clientId, out (Texture, bool) val) && val.Item2)
            {
                avatarImageIndex = val.Item1;
            }
            else
            {
                if (!drawMissing)
                {
                    return false;
                }
                else
                {
                    avatarImageIndex = Client.Instance.Frontend.Renderer.GuiRenderer.ChatMissingAvatar;
                    ret = false;
                }
            }

            Vector2 cHere = ImGui.GetCursorScreenPos();
            bool online = false;
            bool admin = false;
            bool observer = false;
            if (Client.Instance.ClientInfos.TryGetValue(clientId, out ClientInfo localCI))
            {
                online = localCI.IsLoggedOn;
                if (!ret)
                {
                    avatarOverrideColor = localCI.Color.Abgr();
                }

                admin = localCI.IsAdmin;
                observer = localCI.IsObserver;
            }

            if (admin || observer)
            {
                uint clr = admin ? abgrAdmin : abgrObserver;
                drawList.AddRectFilled(cHere - new Vector2(2, 2), cHere + new Vector2(6, 6), clr, 4f);
            }

            drawList.AddImageRounded(avatarImageIndex, cHere, cHere + avatarSize, Vector2.Zero, Vector2.One, avatarOverrideColor, 15f);
            if (online)
            {
                drawList.AddRect(cHere, cHere + avatarSize, ColorAbgr.RoyalBlue, 15f);
            }

            if (injectDummy)
            {
                ImGui.Dummy(avatarSize);
            }

            return ret;
        }

        public void BuildCache(Vector2 windowSize)
        {
            if (this.Renderer == null)
            {
                this.Renderer = this.Type switch
                {
                    RenderType.Line => new ChatRendererLine(this),
                    RenderType.DiceRoll => new ChatRendererRollAccumulated(this),
                    RenderType.DiceRolls => new ChatRendererRolls(this),
                    RenderType.Default => new ChatRendererDefault(this),
                    RenderType.Simple => new ChatRendererSimple(this),
                    RenderType.SessionMarker => new ChatRendererSession(this),
                    RenderType.Image => new ChatRendererImage(this),
                    RenderType.RollExpression => new ChatRendererRollExpression(this),
                    RenderType.AtkDmg => new ChatRendererAtkDmg(this),
                    RenderType.Spell => new ChatRendererSpell(this),
                    RenderType.Sound => new ChatRendererAudio(this),
                    RenderType.ObjectSnapshot => new ChatRendererMapObject(this),
                    _ => default
                };
            }

            this.Renderer.Cache(windowSize, out _, out this._cachedHeight);
            this._cached = true;
        }

        public void WriteNetwork(BinaryWriter bw)
        {
            this.WriteStorage(bw);
            this.Reactions.Write(bw);
        }

        public void ReadNetwork(BinaryReader br)
        {
            this.ReadStorage(br);
            this.Reactions.Read(br);
        }

        public void WriteStorage(BinaryWriter bw)
        {
            bw.Write((byte)3);
            bw.Write(this.SenderID.ToByteArray());
            bw.Write(this.DestID.ToByteArray());
            bw.Write(this.PortraitID.ToByteArray());
            bw.Write(this.Sender);
            bw.Write(this.SenderDisplayName);
            bw.Write(this.DestDisplayName);
            bw.Write((byte)this.Type);
            bw.Write(this.senderColor.Argb());
            bw.Write(this.destColor.Argb());
            bw.Write(this.SendTime.ToBinary());
            bw.Write(this.Blocks.Select(b => !b.DoNotPersist).Count());
            foreach (ChatBlock cb in this.Blocks)
            {
                if (!cb.DoNotPersist)
                {
                    cb.WriteV2(bw);
                }
            }
        }

        public void ReadStorage(BinaryReader br)
        {
            byte version = br.ReadByte(); // Version
            this.SenderID = new Guid(br.ReadBytes(16));
            this.DestID = new Guid(br.ReadBytes(16));
            this.PortraitID = new Guid(br.ReadBytes(16));
            this.Sender = br.ReadString();
            this.SenderDisplayName = br.ReadString();
            this.DestDisplayName = br.ReadString();
            this.Type = (RenderType)br.ReadByte();
            this.SenderColor = Extensions.FromArgb(br.ReadUInt32());
            this.DestColor = Extensions.FromArgb(br.ReadUInt32());
            if (version >= 2) // have time data
            {
                this.SendTime = DateTime.FromBinary(br.ReadInt64());
            }

            this.Blocks.Clear();
            int c = br.ReadInt32();
            for (int i = 0; i < c; ++i)
            {
                ChatBlock chatBlock = new ChatBlock();
                this.Blocks.Add(chatBlock);
                switch (version)
                {
                    case 3:
                    {
                        chatBlock.ReadV2(br);
                        break;
                    }

                    case 1:
                    case 2:
                    default:
                    {
                        chatBlock.ReadV1(br);
                        chatBlock.TryGuessExpressionRollContents();
                        break;
                    }
                }
            }
        }

        public enum RenderType
        {
            Line,

            DiceRoll,
            DiceRolls,

            // R20 integrations
            Default,
            Simple,
            Atk,
            Dmg,
            AtkDmg,
            Spell,
            Traits,

            // Other
            SessionMarker,
            Image,
            RollExpression,
            Sound,
            ObjectSnapshot
        }

        private class AssetPreviewReference
        {
            public AssetPreview preview;
            public bool ready;

            public AssetPreviewReference(AssetPreview preview, bool ready)
            {
                this.preview = preview;
                this.ready = ready;
            }
        }

        public class EmojiReactions
        {
            private Dictionary<int, List<Guid>> _reactions = null;
            private int[] _numIndividualReactions = null;
            private int _numTotalReactions = 0;

            public int Total => this._numTotalReactions;

            public int GetReactions(int index, out List<Guid> reacters)
            {
                if (this._numTotalReactions == 0)
                {
                    reacters = null;
                    return 0;
                }

                int r = this._numIndividualReactions[index];
                reacters = r > 0 ? this._reactions[index] : null;
                return r;
            }

            public EmojiReactions Clone()
            {
                EmojiReactions ret = new EmojiReactions();
                if ((ret._numTotalReactions = this._numTotalReactions) > 0)
                {
                    ret._numIndividualReactions = new int[12];
                    ret._reactions = new Dictionary<int, List<Guid>>();
                    for (int i = 0; i < 12; ++i)
                    {
                        int r = ret._numIndividualReactions[i] = this._numIndividualReactions[i];
                        if (r > 0)
                        {
                            ret._reactions[i] = new List<Guid>(1);
                            ret._reactions[i].AddRange(this._reactions[i]);
                        }
                    }
                }

                return ret;
            }

            public void CopyFrom(EmojiReactions reactions)
            {
                if ((this._numTotalReactions = reactions._numTotalReactions) > 0)
                {
                    if (this._numIndividualReactions == null)
                    {
                        this._numIndividualReactions = new int[12];
                        this._reactions = new Dictionary<int, List<Guid>>();
                    }

                    for (int i = 0; i < 12; ++i)
                    {
                        int dn = this._numIndividualReactions[i];
                        int sn = this._numIndividualReactions[i] = reactions._numIndividualReactions[i];
                        if (dn > 0)
                        {
                            if (sn == 0)
                            {
                                this._reactions.Remove(i);
                            }
                            else
                            {
                                this._reactions[i].Clear();
                            }
                        }

                        if (sn > 0)
                        {
                            if (dn <= 0)
                            {
                                this._reactions[i] = new List<Guid>(1);
                            }
                            else
                            {
                                this._reactions[i].Clear();
                            }

                            this._reactions[i].AddRange(reactions._reactions[i]);
                        }
                    }
                }
                else
                {
                    this._reactions?.Clear();
                    this._reactions = null;
                    this._numIndividualReactions = null;
                }
            }

            public void Write(BinaryWriter bw)
            {
                bw.Write((byte)0); // Version;
                bw.Write(this._numTotalReactions);
                if (this._numTotalReactions > 0)
                {
                    int n;
                    for (int i = 0; i < 12; ++i)
                    {
                        bw.Write(n = this._numIndividualReactions[i]);
                        if (n > 0)
                        {
                            foreach (Guid id in this._reactions[i])
                            {
                                bw.Write(id);
                            }
                        }
                    }
                }
            }

            public void Read(BinaryReader br)
            {
                byte version = br.ReadByte();
                if ((this._numTotalReactions = br.ReadInt32()) > 0)
                {
                    this._numIndividualReactions = new int[12];
                    this._reactions = new Dictionary<int, List<Guid>>();
                    for (int i = 0; i < 12; ++i)
                    {
                        int n = this._numIndividualReactions[i] = br.ReadInt32();
                        if (this._reactions.TryGetValue(i, out List<Guid> l))
                        {
                            if (n > 0)
                            {
                                l.Clear();
                            }
                            else
                            {
                                this._reactions.Remove(i);
                            }
                        }
                        else
                        {
                            if (n > 0)
                            {
                                this._reactions[i] = l = new List<Guid>(1);
                            }
                        }

                        if (n > 0)
                        {
                            for (int j = 0; j < this._numIndividualReactions[i]; ++j)
                            {
                                l.Add(br.ReadGuid());
                            }
                        }
                    }
                }
            }

            public void AddReaction(Guid sender, int reactionIndex)
            {
                if (this._numTotalReactions == 0)
                {
                    this._numIndividualReactions = new int[12];
                    this._reactions = new Dictionary<int, List<Guid>>();
                }

                if (!this._reactions.TryGetValue(reactionIndex, out List<Guid> l))
                {
                    this._reactions[reactionIndex] = l = new List<Guid>(1);
                }

                if (!l.Contains(sender))
                {
                    l.Add(sender);
                    this._numIndividualReactions[reactionIndex] += 1;
                    this._numTotalReactions += 1;
                }
            }

            public void RemoveReaction(Guid sender, int reactionIndex)
            {
                if (this._numTotalReactions > 0)
                {
                    if (this._reactions.TryGetValue(reactionIndex, out List<Guid> l))
                    {
                        if (l.Contains(sender))
                        {
                            this._numTotalReactions -= 1;
                            if ((this._numIndividualReactions[reactionIndex] -= 1) <= 0)
                            {
                                this._reactions.Remove(reactionIndex);
                            }
                            else
                            {
                                l.Remove(sender);
                            }
                        }
                    }

                    if (this._numTotalReactions == 0)
                    {
                        this._reactions = null;
                        this._numIndividualReactions = null;
                    }
                }
            }
        }
    }

    public class ChatSearchCollection : ISerializable
    {
        public Guid ID { get; set; }
        public bool IsServer { get; set; }

        public string SearchQuery { get; set; } = string.Empty;
        public Guid SenderQuery { get; set; } = Guid.Empty;
        public Guid RecepientQuery { get; set; } = Guid.Empty;
        public DateTime TimeFromQuery { get; set; } = DateTime.UnixEpoch;
        public DateTime TimeToQuery { get; set; } = DateTime.MaxValue;
        public SearchQueryFlags Flags { get; set; } = SearchQueryFlags.MatchAll;

        public int ServerLastSearchPosition { get; set; }
        public bool ClientInvalidateCache { get; set; }
        public bool ClientServerHadNoMoreToSend { get; set; }

        private readonly List<ChatLine> _lines = new List<ChatLine>();
        public readonly object chatLinesLock = new object();

        public void Clear()
        {
            lock (this.chatLinesLock)
            {
                this._lines.Clear();
            }
        }

        public void ReceiveLineList(List<ChatLine> lines)
        {
            lock (this.chatLinesLock)
            {
                this._lines.AddRange(lines);
            }

            if (!this.IsServer)
            {
                this.ClientServerHadNoMoreToSend = lines.Count == 0;
            }

            this.ClientInvalidateCache = true;
        }

        public IEnumerable<ChatLine> EnumerateLinesUnsafe() => this._lines;

        public void NotifyOfQueryParameterChanged(bool fullInvalidate = false)
        {
            lock (this.chatLinesLock)
            {
                this._lines.Clear();
            }

            if (!this.IsServer)
            {
                this.ClientServerHadNoMoreToSend = false;
                new PacketChatQuery() { QueryID = this.ID, QueryData = this.Serialize(), ConstructNewQuery = fullInvalidate }.Send();
                this.RequestMoreLines();
            }
        }

        public void RequestMoreLines()
        {
            if (!this.IsServer)
            {
                new PacketChatQueryLines() { QueryID = this.ID }.Send();
            }
        }

        public bool Matches(ChatLine cl)
        {
            if (!this.SenderQuery.IsEmpty() && !Guid.Equals(this.SenderQuery, cl.SenderID))
            {
                return false;
            }

            if (!this.RecepientQuery.IsEmpty() && !Guid.Equals(this.RecepientQuery, cl.DestID))
            {
                return false;
            }

            if (cl.SendTime < this.TimeFromQuery || cl.SendTime > this.TimeToQuery)
            {
                return false;
            }

            if (this.Flags == SearchQueryFlags.None)
            {
                return false;
            }

            bool clIsBasicText = cl.Type == ChatLine.RenderType.Line;
            bool clIsSpecialRenderer = !clIsBasicText;
            bool clHasRolls = false;
            bool clHasText = false;
            bool clHasCrits = false;
            bool clHasNat1s = false;
            bool clHasRollsNonCrits = false;
            foreach (ChatBlock block in cl.Blocks)
            {
                if (block.RollContents != ChatBlockExpressionRollContents.None)
                {
                    clHasRolls = true;
                    ColorAbgr c = block.Color;
                    if (c.Equals(ChatParser.CritColor))
                    {
                        clHasCrits = true;
                    }
                    else
                    {
                        if (c.Equals(ChatParser.Nat1Color))
                        {
                            clHasNat1s = true;
                        }
                        else
                        {
                            if (c.Equals(ChatParser.CritAndNat1Color))
                            {
                                clHasCrits = clHasNat1s = true;
                            }
                            else
                            {
                                clHasRollsNonCrits = true;
                            }
                        }
                    }
                }
                else
                {
                    clHasText = true;
                }
            }

            if (clHasRolls && !this.Flags.HasFlag(SearchQueryFlags.HasRolls))
            {
                return false;
            }

            if (clHasText && !this.Flags.HasFlag(SearchQueryFlags.HasText))
            {
                return false;
            }

            if (clIsBasicText && !this.Flags.HasFlag(SearchQueryFlags.IsBasicText))
            {
                return false;
            }

            if (clIsSpecialRenderer && !this.Flags.HasFlag(SearchQueryFlags.IsSpecialRenderer))
            {
                return false;
            }

            if (clHasRolls && !this.Flags.HasFlag(SearchQueryFlags.HasRolls))
            {
                return false;
            }

            if (clHasNat1s && !this.Flags.HasFlag(SearchQueryFlags.HasNat1s))
            {
                return false;
            }

            if (clHasCrits && !this.Flags.HasFlag(SearchQueryFlags.HasCrits))
            {
                return false;
            }

            if (clHasRollsNonCrits && !this.Flags.HasFlag(SearchQueryFlags.HasRollsOutsideOfCritsAndNat1s))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(this.SearchQuery))
            {
                string total = cl.GetFullText();
                if (!total.Contains(this.SearchQuery, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        public DataElement Serialize()
        {
            // Assume ID already sent in packet
            DataElement ret = new DataElement();
            ret.SetString("SearchQuery", this.SearchQuery);
            ret.SetGuid("SenderQuery", this.SenderQuery);
            ret.SetGuid("RecepientQuery", this.RecepientQuery);
            ret.SetDateTime("TimeFromQuery", this.TimeFromQuery);
            ret.SetDateTime("TimeToQuery", this.TimeToQuery);
            ret.SetEnum("Flags", this.Flags);

            return ret;
        }

        public void Deserialize(DataElement e)
        {
            this.SearchQuery = e.GetString("SearchQuery", string.Empty);
            this.SenderQuery = e.GetGuidLegacy("SenderQuery", Guid.Empty);
            this.RecepientQuery = e.GetGuidLegacy("RecepientQuery", Guid.Empty);
            this.TimeFromQuery = e.GetDateTime("TimeFromQuery", DateTime.UnixEpoch);
            this.TimeToQuery = e.GetDateTime("TimeToQuery", DateTime.MaxValue);
            this.Flags = e.GetEnum("Flags", SearchQueryFlags.MatchAll);
        }

        [Flags]
        public enum SearchQueryFlags : uint
        {
            None = 0,
            HasRolls = 1,
            HasText = 2,
            IsBasicText = 4,
            IsSpecialRenderer = 8,
            HasCrits = 16,
            HasNat1s = 32,
            HasRollsOutsideOfCritsAndNat1s = 64,

            OnlyBasicText = HasText | IsBasicText,
            OnlySpecialRenderer = MatchAll & ~IsBasicText,

            MatchAll = HasRolls | HasText | IsBasicText | IsSpecialRenderer | HasCrits | HasNat1s | HasRollsOutsideOfCritsAndNat1s
        }
    }
}