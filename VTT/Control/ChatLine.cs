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

        public ChatBlock CreateContextBlock(string text, string tt = "", ChatBlockType type = ChatBlockType.Text) => new ChatBlock() { Color = this.Blocks.Count > 0 ? this.Blocks[^1].Color : Extensions.FromAbgr(0), Text = text, Tooltip = tt, Type = type };

        public bool ImRender(float h, int idx, SimpleLanguage lang)
        {
            if (!this._cached)
            {
                this.BuildCache();
            }

            if (Client.Instance.IsAdmin || this.CanSee(Client.Instance.ID))
            {
                float scrollMin = ImGui.GetScrollY();
                float scrollMax = scrollMin + h;
                if (ImGui.GetCursorPosY() + this._cachedHeight + 22 < scrollMin)
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + this._cachedHeight + 22);
                    return true;
                }

                if (scrollMax > float.Epsilon && ImGui.GetCursorPosY() > scrollMax)
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + this._cachedHeight + 22);
                    return true;
                }

                Vector2 sV = ImGui.GetCursorScreenPos();
                if (!this.PortraitID.Equals(Guid.Empty))
                {
                    if (this._portraitTex == null)
                    {
                        this._portraitTex = new AssetPreviewReference(null, false);
                        Map m = Client.Instance.CurrentMap;
                        if (m != null)
                        {
                            MapObject mo;
                            if (m.ObjectsByID.ContainsKey(this.PortraitID))
                            {
                                try
                                {
                                    mo = m.ObjectsByID[this.PortraitID];
                                }
                                catch
                                {
                                    mo = null;
                                }

                                if (mo != null)
                                {
                                    AssetStatus a = Client.Instance.AssetManager.ClientAssetLibrary.GetOrCreatePortrait(mo.AssetID, out AssetPreview ap);
                                    if (a == AssetStatus.Return)
                                    {
                                        this._portraitTex.preview = ap;
                                        this._portraitTex.ready = true;
                                    }
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
                                AssetPreview.FrameData frame = this._portraitTex.preview.GetCurrentFrame((int)(Client.Instance.Frontend.UpdatesExisted % (ulong)this._portraitTex.preview.FramesTotalDelay));
                                float sS = frame.X / tW;
                                float sE = sS + (frame.Width / tW);
                                float tS = frame.Y / tH;
                                float tE = tS + (frame.Height / tH);
                                ImGui.Image(glTex, new Vector2(16, 16), new Vector2(sS, tS), new Vector2(sE, tE));
                            }
                            else
                            {
                                ImGui.Image(glTex, new Vector2(16, 16));
                            }

                            ImGui.SameLine();
                        }
                    }
                }
                else
                {
                    if (Client.Instance.Frontend.Renderer.AvatarLibrary.ClientImages.TryGetValue(this.SenderID, out (Texture, bool) val) && val.Item2)
                    {
                        ImGui.Image(val.Item1, new Vector2(16, 16));
                        ImGui.SameLine();
                    }
                }

                if (!this.SendTime.Equals(DateTime.UnixEpoch))
                {
                    string time = this.SendTime.ToString("HH:mm:ss");
                    float ocpx = ImGui.GetCursorPosX();
                    ImGui.SetCursorPosX(350 - ImGui.CalcTextSize(time).X);
                    ImGui.Text(time);
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(this.SendTime.ToLongDateString() + "\n" + this.SendTime.ToLongTimeString());
                    }

                    ImGui.SameLine();
                    ImGui.SetCursorPosX(ocpx);
                }

                ImGui.PushStyleColor(ImGuiCol.Text, this.SenderColor.Abgr());
                ImGui.TextUnformatted(this.SenderDisplayName);
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.Text("->");
                ImGui.SameLine();
                if (!this.DestID.IsEmpty())
                {
                    if (Client.Instance.Frontend.Renderer.AvatarLibrary.ClientImages.TryGetValue(this.DestID, out (Texture, bool) val) && val.Item2)
                    {
                        ImGui.Image(val.Item1, new Vector2(16, 16));
                        ImGui.SameLine();
                    }
                }

                ImGui.PushStyleColor(ImGuiCol.Text, this.DestColor.Abgr());
                ImGui.TextUnformatted(string.IsNullOrEmpty(this.DestDisplayName) ? Client.Instance.Lang.Translate("chat.all") : this.DestDisplayName);
                ImGui.PopStyleColor();
                float eY = ImGui.GetCursorScreenPos().Y;
                ImGui.SameLine();
                Vector2 eV = new Vector2(ImGui.GetCursorScreenPos().X, eY);
                ImGui.NewLine();
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

                this.Renderer?.Render();
                if (ImGui.IsMouseHoveringRect(sV, new Vector2(ImGui.GetCursorScreenPos().X + 350, ImGui.GetCursorScreenPos().Y)))
                {
                    if (ImGui.BeginPopupContextItem("chat_line_popup_" + idx))
                    {
                        if (ImGui.MenuItem(lang.Translate("ui.chat.copy")))
                        {
                            ImGui.SetClipboardText($"{this.SendTime} {this.SenderDisplayName}: " + this.Renderer?.ProvideTextForClipboard(this.SendTime, this.SenderDisplayName, lang) ?? " ");
                        }

                        ImGui.EndPopup();
                    }

                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    {
                        ImGui.OpenPopup("chat_line_popup_" + idx);
                    }
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

        public void BuildCache()
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
                    _ => default
                };
            }

            this.Renderer.Cache(out _, out this._cachedHeight);
            this._cached = true;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write((byte)2);
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
                    cb.Write(bw);
                }
            }
        }

        public void Read(BinaryReader br)
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
            if (version == 2) // have time data
            {
                this.SendTime = DateTime.FromBinary(br.ReadInt64());
            }

            this.Blocks.Clear();
            int c = br.ReadInt32();
            for (int i = 0; i < c; ++i)
            {
                this.Blocks.Add(new ChatBlock());
                this.Blocks[i].Read(br);
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
            RollExpression
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
    }
}