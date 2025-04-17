namespace VTT.Render.Chat
{
    using ImGuiNET;
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using VTT.Asset;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Render.Gui;
    using VTT.Util;

    public class ChatRendererMapObject : ChatRendererBase
    {
        public ChatRendererMapObject(ChatLine container) : base(container)
        {
        }

        private float _cachedHeight;
        public override void Cache(Vector2 windowSize, out float width, out float height)
        {
            width = 340f;
            height = 48f;
            int i = 5;
            bool layoutgenPrevBarWasInline = false;
            float layoutgenPenX = 0;
            while (true)
            {
                if (this.Container.TryGetBlockAt(i++, out ChatBlock barBlock))
                {
                    if (Enum.TryParse(barBlock.Text[(Math.Max(0, barBlock.Text.LastIndexOf('/')) + 1)..], out DisplayBar.DrawMode dm))
                    { 
                        switch (dm)
                        {
                            case DisplayBar.DrawMode.Default:
                            {
                                layoutgenPrevBarWasInline = false;
                                layoutgenPenX = 0;
                                height += 16;
                                break;
                            }

                            case DisplayBar.DrawMode.Compact:
                            {
                                layoutgenPrevBarWasInline = false;
                                layoutgenPenX = 0;
                                height += 16;
                                break;
                            }

                            case DisplayBar.DrawMode.Round:
                            {
                                if (layoutgenPrevBarWasInline)
                                {
                                    if (layoutgenPenX + 56 > 340f)
                                    {
                                        layoutgenPenX = 0;
                                        height += 48;
                                    }
                                }
                                else
                                {
                                    layoutgenPenX = 0;
                                    height += 48;
                                }


                                layoutgenPenX += 56;
                                layoutgenPrevBarWasInline = true;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    break;
                }
            }

            this._cachedHeight = height;
        }

        public override void ClearCache()
        {

        }

        public override string ProvideTextForClipboard(DateTime dateTime, string senderName, SimpleLanguage lang) => string.Empty;

        public override void Render(Guid senderId, uint senderColorAbgr)
        {
            // Blocks:
            // ObjectID
            // OwnerID
            // Name
            // AssetID
            // Description
            // Bars as Min/Max Color Type
            Vector2 avail = ImGui.GetContentRegionAvail();
            Vector2 startingPos = ImGui.GetCursorPos();
            ImGui.Dummy(new Vector2(340, this._cachedHeight));
            Vector2 endPos = ImGui.GetCursorPos();
            this.Container.TryGetBlockAt(0, out ChatBlock objectIdBlock);
            this.Container.TryGetBlockAt(1, out ChatBlock ownerIdBlock);
            this.Container.TryGetBlockAt(2, out ChatBlock nameBlock);
            this.Container.TryGetBlockAt(3, out ChatBlock assetIdBlock);
            this.Container.TryGetBlockAt(4, out ChatBlock descriptionBlock);
            List<ChatBlock> barBlocks = new List<ChatBlock>();
            int i = 5;
            while (true)
            {
                if (this.Container.TryGetBlockAt(i++, out ChatBlock barBlock))
                {
                    barBlocks.Add(barBlock);
                }
                else
                {
                    break;
                }
            }

            GuiRenderer uiRoot = GuiRenderer.Instance;

            Texture portraitTexture = uiRoot.TurnTrackerBackgroundNoObject;
            Vector2 st = Vector2.Zero;
            Vector2 uv = Vector2.One;
            if (assetIdBlock != null && Guid.TryParse(assetIdBlock.Text, out Guid aId))
            {
                if (Client.Instance.AssetManager.ClientAssetLibrary.Portraits.Get(aId, AssetType.Model, out AssetPreview ap) == AssetStatus.Return)
                {
                    Texture glTex = ap.GetGLTexture();
                    if (glTex != null && glTex.IsAsyncReady)
                    {
                        portraitTexture = glTex;
                        AssetPreview.FrameData frame = ap.GetCurrentFrame((int)Client.Instance.Frontend.UpdatesExisted);
                        if (ap.IsAnimated && frame.IsValidFrame)
                        {
                            float atW = glTex.Size.Width;
                            float atH = glTex.Size.Height;
                            float sS = frame.X / atW;
                            float sE = sS + (frame.Width / atW);
                            float tS = frame.Y / atH;
                            float tE = tS + (frame.Height / atH);
                            st = new Vector2(sS, tS);
                            uv = new Vector2(sE, tE);
                        }
                    }
                }

                ImGui.SetCursorPos(startingPos);
                Vector4 borderColor = Vector4.Zero;
                bool hover = false;
                if (hover = ImGui.IsMouseHoveringRect(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(48, 48)))
                {
                    unsafe
                    {
                        borderColor = *ImGui.GetStyleColorVec4(ImGuiCol.ButtonActive);
                    }
                }

                ImGui.Image(portraitTexture, new Vector2(48, 48), st, uv, Vector4.One, borderColor);
                if (hover && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && objectIdBlock != null && ownerIdBlock != null)
                {
                    bool alt = Client.Instance.Frontend.GameHandle.IsAnyAltDown();
                    Map m = Client.Instance.CurrentMap;
                    if (m != null && Guid.TryParse(objectIdBlock.Text, out Guid objectId) && Guid.TryParse(ownerIdBlock.Text, out Guid ownerId) && m.GetObject(objectId, out MapObject mo))
                    {
                        if (alt)
                        {
                            if (mo.MapLayer <= 0 && !mo.DoNotRender && !mo.HideFromSelection)
                            {
                                Ping p = new Ping() { DeathTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 10000, OwnerColor = Extensions.FromArgb(Client.Instance.Settings.Color), OwnerID = Client.Instance.ID, OwnerName = Client.Instance.Settings.Name, Position = mo.Position, Type = Ping.PingType.Generic };
                                new PacketPing() { Ping = p }.Send();
                            }
                        }
                        else
                        {
                            if (Client.Instance.IsAdmin || Client.Instance.IsObserver || mo.CanEdit(Client.Instance.ID) || mo.MapLayer <= 0)
                            {
                                Vector3 camPos = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Position;
                                if (Client.Instance.Frontend.Renderer.MapRenderer.IsOrtho)
                                {
                                    Vector3 oPos = mo.Position;
                                    Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.MoveCamera(new Vector3(oPos.X, oPos.Y, camPos.Z), true);
                                }
                                else
                                {
                                    Vector3 camDirection = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Direction;
                                    Vector3 oPos = mo.Position;
                                    Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.MoveCamera(oPos - (camDirection * 5.0f), true);
                                }

                                if (Client.Instance.IsAdmin || mo.CanEdit(Client.Instance.ID))
                                {
                                    Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Add(mo);
                                }
                            }
                        }
                    }
                }
            }

            if (nameBlock != null)
            {
                Vector2 nameSize = ImGui.CalcTextSize(nameBlock.Text);
                ImGui.SetCursorPos(startingPos + new Vector2((avail.X * 0.5f) - (nameSize.X * 0.5f), 0));
                ImGui.PushStyleColor(ImGuiCol.Text, nameBlock.Color.Abgr());
                ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(nameBlock.Text));
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered() && descriptionBlock != null)
                {
                    ImGui.SetTooltip(ImGuiHelper.TextOrEmpty(descriptionBlock.Text));
                }
            }

            if (barBlocks.Count > 0)
            {
                bool prevWasRound = false;
                float penX = 0;
                ImGui.SetCursorPos(startingPos + new Vector2(0, 52));
                foreach (ChatBlock cb in barBlocks)
                {
                    int index1 = Math.Max(0, cb.Text.IndexOf('/'));
                    int index2 = Math.Max(0, cb.Text.IndexOf('/', index1 + 1));
                    if (float.TryParse(cb.Text.AsSpan(0, index1), out float current) &&
                        float.TryParse(cb.Text.AsSpan(index1 + 1, index2 - index1 - 1), out float max) &&
                        Enum.TryParse(cb.Text.AsSpan(index2 + 1), out DisplayBar.DrawMode drawMode))
                    {
                        GuiRenderer.RenderBar(current, max, drawMode, cb.Color, false, Vector2.Zero, 320f, 340f, ref prevWasRound, ref penX);
                    }
                }
            }

            ImGui.SetCursorPos(endPos);
        }

        public static void SendChatSnapshot(MapObject mo, bool includeBars)
        {
            ChatBuilder cb = new ChatBuilder()
                .SpecifyRenderType(ChatLine.RenderType.ObjectSnapshot)
                .AddPassthrough(mo.ID.ToString())
                .AddPassthrough(mo.OwnerID.ToString())
                .AddPassthrough(ChatBuilder.Escape(mo.Name))
                .AddPassthrough(mo.AssetID.ToString())
                .AddPassthrough(ChatBuilder.Escape(mo.Description));

            if (includeBars)
            {
                foreach (DisplayBar db in mo.Bars)
                {
                    cb.SetColor(db.DrawColor);
                    cb.AddPassthrough($"{db.CurrentValue}/{db.MaxValue}/{Enum.GetName(db.RenderMode)}");
                    cb.ResetColor();
                }
            }

            new PacketChatMessage() { Message = cb.Build() }.Send();
        }
    }
}
