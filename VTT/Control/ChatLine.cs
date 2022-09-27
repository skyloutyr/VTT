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
    using VTT.Util;

    public class ChatLine
    {
        public string Sender { get; set; }

        public string SenderDisplayName { get; set; }
        public string DestDisplayName { get; set; }
        public Guid SenderID { get; set; }
        public Guid DestID { get; set; } = Guid.Empty;
        public Guid PortraitID { get; set; } = Guid.Empty;

        public Color SenderColor { get => senderColor.Argb() == 0 ? Extensions.FromAbgr(ImGui.GetColorU32(ImGuiCol.Text)) : senderColor; set => senderColor = value; }
        public Color DestColor { get => destColor.Argb() == 0 ? Extensions.FromAbgr(ImGui.GetColorU32(ImGuiCol.Text)) : destColor; set => destColor = value; }
        public List<ChatBlock> Blocks { get; set; } = new List<ChatBlock>();

        public ChatRendererBase Renderer { get; set; }
        public RenderType Type { get; set; }

        public int Index { get; set; }

        private bool _cached;
        private float _cachedHeight;
        private Texture _portraitTex;
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

        public bool ImRender(float h)
        {
            if (!this._cached)
            {
                this.BuildCache();
            }

            if (Client.Instance.IsAdmin || this.CanSee(Client.Instance.ID))
            {
                float scrollMin = ImGui.GetScrollY();
                float scrollMax = scrollMin + h;
                if (ImGui.GetCursorPosY() + this._cachedHeight + 20 < scrollMin)
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + this._cachedHeight + 20);
                    return true;
                }

                if (scrollMax > float.Epsilon && ImGui.GetCursorPosY() > scrollMax)
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + this._cachedHeight + 20);
                    return true;
                }

                Vector2 sV = ImGui.GetCursorScreenPos();
                if (!this.PortraitID.Equals(Guid.Empty))
                {
                    if (this._portraitTex == null)
                    {
                        this._portraitTex = Client.Instance.Frontend.Renderer.GuiRenderer.TurnTrackerBackgroundNoObject;
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
                                    this._portraitTex = a == AssetStatus.Await ? null : ap.GetGLTexture();
                                }
                            }
                        }
                    }

                    if (this._portraitTex != null && this._portraitTex != Client.Instance.Frontend.Renderer.GuiRenderer.TurnTrackerBackgroundNoObject)
                    {
                        ImGui.Image(this._portraitTex, new Vector2(16, 16));
                        ImGui.SameLine();
                    }
                }

                Vector2 cV = ImGui.GetCursorPos();
                ImGui.PushStyleColor(ImGuiCol.Text, this.SenderColor.Abgr());
                ImGui.TextUnformatted(this.SenderDisplayName);
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.Text("->");
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, this.DestColor.Abgr());
                ImGui.TextUnformatted(string.IsNullOrEmpty(this.DestDisplayName) ? Client.Instance.Lang.Translate("chat.all") : this.DestDisplayName);
                ImGui.PopStyleColor();
                float eY = ImGui.GetCursorScreenPos().Y;
                ImGui.SameLine();
                Vector2 eV = new Vector2(ImGui.GetCursorScreenPos().X, eY);
                ImGui.NewLine();
                if (ImGui.IsMouseHoveringRect(sV, eV))
                {
                    ImGui.SetTooltip(this.Sender);
                }

                this.Renderer?.Render();
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
                switch (this.Type)
                {
                    case RenderType.Line:
                    {
                        this.Renderer = new ChatRendererLine(this);
                        break;
                    }

                    case RenderType.DiceRoll:
                    {
                        this.Renderer = new ChatRendererRollAccumulated(this);
                        break;
                    }

                    case RenderType.DiceRolls:
                    {
                        this.Renderer = new ChatRendererRolls(this);
                        break;
                    }

                    case RenderType.Default:
                    {
                        this.Renderer = new ChatRendererDefault(this);
                        break;
                    }

                    case RenderType.Simple:
                    {
                        this.Renderer = new ChatRendererSimple(this);
                        break;
                    }

                    case RenderType.SessionMarker:
                    {
                        this.Renderer = new ChatRendererSession(this);
                        break;
                    }

                    case RenderType.Image:
                    {
                        this.Renderer = new ChatRendererImage(this);
                        break;
                    }

                    case RenderType.RollExpression:
                    {
                        this.Renderer = new ChatRendererRollExpression(this);
                        break;
                    }

                    case RenderType.AtkDmg:
                    {
                        this.Renderer = new ChatRendererAtkDmg(this);
                        break;
                    }

                    case RenderType.Spell:
                    {
                        this.Renderer = new ChatRendererSpell(this);
                        break;
                    }
                }
            }

            this.Renderer.Cache(out _, out this._cachedHeight);
            this._cached = true;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write((byte)1);
            bw.Write(this.SenderID.ToByteArray());
            bw.Write(this.DestID.ToByteArray());
            bw.Write(this.PortraitID.ToByteArray());
            bw.Write(this.Sender);
            bw.Write(this.SenderDisplayName);
            bw.Write(this.DestDisplayName);
            bw.Write((byte)this.Type);
            bw.Write(this.senderColor.Argb());
            bw.Write(this.destColor.Argb());
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
            br.ReadByte(); // Version
            this.SenderID = new Guid(br.ReadBytes(16));
            this.DestID = new Guid(br.ReadBytes(16));
            this.PortraitID = new Guid(br.ReadBytes(16));
            this.Sender = br.ReadString();
            this.SenderDisplayName = br.ReadString();
            this.DestDisplayName = br.ReadString();
            this.Type = (RenderType)br.ReadByte();
            this.SenderColor = Extensions.FromArgb(br.ReadUInt32());
            this.DestColor = Extensions.FromArgb(br.ReadUInt32());
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
    }
}