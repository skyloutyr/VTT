namespace VTT.Render.Gui
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using VTT.Asset;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Render.Chat;
    using VTT.Util;
    using static VTT.Render.Chat.ChatRendererBase;

    public partial class GuiRenderer
    {
        private int _cSelAnimation;
        private readonly ParticleContainerModelAttachmentPointCache _particleContainerModelAttachmentPointCache = new ParticleContainerModelAttachmentPointCache();

        private List<(Guid, Guid)> SelectedToPacket2(List<MapObject> os) => os.Select(x => (x.MapID, x.ID)).ToList();
        private List<(Guid, Guid, object)> SelectedToPacket3(List<MapObject> os, object data) => os.Select(x => (x.MapID, x.ID, data)).ToList();
        private List<(Guid, Guid, T)> SelectedToPacketEx<T>(List<MapObject> os, Func<MapObject, T> data) => os.Select(x => (x.MapID, x.ID, data(x))).ToList();

        protected bool IdentifyTagRenderSizes(Tag t, out Vector2 renderSize)
        {
            switch (t.Kind)
            {
                case Tag.TagKind.Shape:
                {
                    renderSize = new Vector2(24, 24);
                    return true;
                }

                case Tag.TagKind.Text:
                {
                    Vector2 tSz = ImGui.CalcTextSize(ImGuiHelper.TextOrEmpty(t.Text));
                    renderSize = new Vector2(MathF.Min(tSz.X + 8, 64), 24);
                    return true;
                }

                case Tag.TagKind.CustomImageAsset:
                {
                    if (Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(t.AssetID, AssetType.Texture, out Asset a) == AssetStatus.Return && a != null && a.Type == AssetType.Texture && a.Texture != null && a.Texture.glReady)
                    {
                        Texture tex = a.Texture.GetOrCreateGLTexture(true, false, out VTT.Asset.Glb.TextureAnimation anim);
                        if (tex.IsAsyncReady)
                        {
                            VTT.Asset.Glb.TextureAnimation.Frame frame = anim.FindFrameForIndex(double.NaN);
                            renderSize = Vector2.Zero;
                            (renderSize.X, renderSize.Y) = VTTMath.ClampKeepAR(frame.Location.Width, frame.Location.Height, 64, 24, out _);
                            return true;
                        }
                    }

                    renderSize = Vector2.Zero;
                    return false;
                }

                case Tag.TagKind.CustomImageB64:
                {
                    AssetStatus imgStatus = ResolveImageBlock(t.EmbedB64Image, out ImageBlockImageType imgType, out Asset a, out AssetPreview ap);
                    if (imgStatus == AssetStatus.Return)
                    {
                        if (imgType == ImageBlockImageType.AssetRef
                                            ? a == null || a.Texture == null || !a.Texture.glReady
                                            : ap == null || ap.GLTex == null || !ap.GLTex.IsAsyncReady) // Impossible but sanity check in case of race condidion
                        {
                            renderSize = Vector2.Zero;
                            return false;
                        }

                        Vector2 imgSize;
                        if (imgType == ImageBlockImageType.AssetRef)
                        {
                            Texture tex = a.Texture.GetOrCreateGLTexture(false, true, out VTT.Asset.Glb.TextureAnimation animationData);
                            if (animationData != null && animationData.Frames.Length > 1)
                            {
                                VTT.Asset.Glb.TextureAnimation.Frame frame = animationData.FindFrameForIndex(double.NaN);
                                Vector2 tSzV2 = new Vector2(tex.Size.Width, tex.Size.Height);
                                imgSize = new Vector2(frame.Location.Width, frame.Location.Height) * tSzV2;
                            }
                            else
                            {
                                imgSize = new Vector2(tex.Size.Width, tex.Size.Height);
                            }
                        }
                        else
                        {
                            if (ap.IsAnimated && ap.FramesTotalDelay > 0)
                            {
                                AssetPreview.FrameData frame = ap.GetCurrentFrame((int)(((Client.Instance.Frontend.UpdatesExisted) & int.MaxValue) * (100f / 60f)));
                                imgSize = new Vector2(frame.Width, frame.Height);
                            }
                            else
                            {
                                imgSize = new Vector2(ap.GLTex.Size.Width, ap.GLTex.Size.Height);
                            }
                        }

                        (imgSize.X, imgSize.Y) = VTTMath.ClampKeepAR(imgSize.X, imgSize.Y, 64, 32, out _);
                        renderSize = imgSize;
                        return true;
                    }

                    renderSize = Vector2.Zero;
                    return false;
                }

                default:
                {
                    renderSize = Vector2.Zero;
                    return false;
                }
            }
        }

        // This assumes a Dummy was already supplied!
        protected void RenderTagIntoList(ImDrawListPtr drawList, Vector2 size, Tag t)
        {
            Vector2 start = ImGui.GetCursorScreenPos();
            switch (t.Kind)
            {
                case Tag.TagKind.Shape:
                {
                    float uvStep = 1.0f / 22.0f;
                    float uvStart = uvStep * (int)t.Shape;
                    drawList.AddImage(this.TagShapes, start, start + size, new Vector2(uvStart, 0), new Vector2(uvStart + uvStep, 0.5f), t.Color1.Abgr());
                    if (!string.IsNullOrEmpty(t.Text))
                    {
                        string txt = t.Text;
                        if (txt.Length > 3)
                        {
                            txt = txt[..3];
                        }

                        Vector2 tSize = ImGui.CalcTextSize(txt);
                        drawList.AddText(start + (size / 2.0f) - (tSize / 2.0f), t.TextColor.Abgr(), txt);
                    }

                    drawList.AddImage(this.TagShapes, start, start + size, new Vector2(uvStart, 0.5f), new Vector2(uvStart + uvStep, 1), t.Color2.Abgr());
                    break;
                }

                case Tag.TagKind.Text:
                {
                    drawList.AddRectFilled(start, start + size, t.Color1.Abgr(), t.BorderRounding);
                    if (!string.IsNullOrEmpty(t.Text))
                    {
                        string txt = t.Text;
                        if (txt.Length > 32)
                        {
                            txt = txt[..32];
                        }

                        Vector2 tSize = ImGui.CalcTextSize(txt);
                        drawList.AddText(start + (size / 2.0f) - (tSize / 2.0f), t.TextColor.Abgr(), txt);
                    }

                    drawList.AddRect(start, start + size, t.Color2.Abgr(), t.BorderRounding);
                    break;
                }

                case Tag.TagKind.CustomImageAsset:
                {
                    if (Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(t.AssetID, AssetType.Texture, out Asset a) == AssetStatus.Return && a != null && a.Type == AssetType.Texture && a.Texture != null && a.Texture.glReady)
                    {
                        Texture tex = a.Texture.GetOrCreateGLTexture(true, false, out VTT.Asset.Glb.TextureAnimation anim);
                        if (tex.IsAsyncReady)
                        {
                            VTT.Asset.Glb.TextureAnimation.Frame frame = anim.FindFrameForIndex(double.NaN);
                            drawList.AddImage(tex, start, start + size, frame.LocationUniform.Xy(), frame.LocationUniform.Xy() + frame.LocationUniform.Zw(), t.Color1.Abgr());
                        }

                        if (!string.IsNullOrEmpty(t.Text))
                        {
                            string txt = t.Text;
                            if (txt.Length > 32)
                            {
                                txt = txt[..32];
                            }

                            Vector2 tSize = ImGui.CalcTextSize(txt);
                            drawList.AddText(start + (size / 2.0f) - (tSize / 2.0f), t.TextColor.Abgr(), txt);
                        }
                    }

                    break;
                }

                case Tag.TagKind.CustomImageB64:
                {
                    AssetStatus imgStatus = ResolveImageBlock(t.EmbedB64Image, out ImageBlockImageType imgType, out Asset a, out AssetPreview ap);
                    if (imgStatus == AssetStatus.Return)
                    {
                        if (imgType == ImageBlockImageType.AssetRef
                                            ? a == null || a.Texture == null || !a.Texture.glReady
                                            : ap == null || ap.GLTex == null || !ap.GLTex.IsAsyncReady) // Impossible but sanity check in case of race condidion
                        {
                            break; // Can't render here, image is not ready yet
                        }

                        Texture imgTexture;
                        Vector2 imgSize;
                        Vector2 imgSt;
                        Vector2 imgUv;
                        bool needGammaCorrection = false;
                        if (imgType == ImageBlockImageType.AssetRef)
                        {
                            Texture tex = a.Texture.GetOrCreateGLTexture(false, true, out VTT.Asset.Glb.TextureAnimation animationData);
                            imgTexture = tex;
                            if (animationData != null && animationData.Frames.Length > 1)
                            {
                                VTT.Asset.Glb.TextureAnimation.Frame frame = animationData.FindFrameForIndex(double.NaN);
                                Vector2 tSzV2 = new Vector2(tex.Size.Width, tex.Size.Height);
                                imgSize = new Vector2(frame.Location.Width, frame.Location.Height) * tSzV2;
                                imgSt = new Vector2(frame.Location.Left, frame.Location.Top);
                                imgUv = new Vector2(frame.Location.Right, frame.Location.Bottom);
                            }
                            else
                            {
                                imgSize = new Vector2(tex.Size.Width, tex.Size.Height);
                                imgSt = Vector2.Zero;
                                imgUv = Vector2.One;
                            }

                            needGammaCorrection = a.Texture.Meta?.GammaCorrect ?? false;
                        }
                        else
                        {
                            imgTexture = ap.GLTex;
                            if (ap.IsAnimated && ap.FramesTotalDelay > 0)
                            {
                                float tW = ap.GLTex.Size.Width;
                                float tH = ap.GLTex.Size.Height;
                                AssetPreview.FrameData frame = ap.GetCurrentFrame((int)(((Client.Instance.Frontend.UpdatesExisted) & int.MaxValue) * (100f / 60f)));
                                float sS = frame.X / tW;
                                float sE = sS + (frame.Width / tW);
                                float tS = frame.Y / tH;
                                float tE = tS + (frame.Height / tH);
                                imgSize = new Vector2(frame.Width, frame.Height);
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

                        float originalSzX = imgSize.X;
                        float originalSzY = imgSize.Y;
                        (imgSize.X, imgSize.Y) = VTTMath.ClampKeepAR(imgSize.X, imgSize.Y, 64, 32, out bool originalSizeChanged);
                        if (needGammaCorrection)
                        {
                            drawList.AddCallback(Marshal.GetFunctionPointerForDelegate(ChatRendererLine.GammaSetterCallback), new IntPtr(1));
                        }

                        Vector2 cPosBeforeImage = ImGui.GetCursorScreenPos();
                        drawList.AddImage(imgTexture, start, start + imgSize, imgSt, imgUv, t.Color1.Abgr());
                        if (needGammaCorrection)
                        {
                            drawList.AddCallback(Marshal.GetFunctionPointerForDelegate(ChatRendererLine.GammaSetterCallback), new IntPtr(0));
                        }

                        if (!string.IsNullOrEmpty(t.Text))
                        {
                            string txt = t.Text;
                            if (txt.Length > 32)
                            {
                                txt = txt[..32];
                            }

                            Vector2 tSize = ImGui.CalcTextSize(txt);
                            drawList.AddText(start + (size / 2.0f) - (tSize / 2.0f), t.TextColor.Abgr(), txt);
                        }
                    }

                    break;
                }
            }
        }

        private void ImTagSettings(SimpleLanguage lang, MapObject mo, Tag t, int tagIndex,
            Action<PacketObjectTag.ActionType> tagUpdateCallback, Action<Tag> tagDuplicateCallback, Action tagAssetHoverCallback, Action tagB64HoverCallback
            )
        {
            if (ImGui.BeginChild("##Tag_" + t.ID, new Vector2(320, 0), ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY, ImGuiWindowFlags.ChildWindow | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                this.RenderTagIntoList(ImGui.GetWindowDrawList(), new Vector2(t.Kind == Tag.TagKind.Text ? 48 : 24, 24), t);
                ImGui.Dummy(new Vector2(t.Kind == Tag.TagKind.Text ? 48 : 24, 24));
                ImGui.SameLine();
                string txt = t.Text;
                ImGui.PushItemWidth(128);
                if (ImGui.InputText("##TagText", ref txt, 32))
                {
                    t.Text = txt;
                }

                ImGui.PopItemWidth();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.tag.text.tt"));
                }

                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    tagUpdateCallback(PacketObjectTag.ActionType.Update);
                }

                ImGui.SameLine();
                if (tagIndex == 0)
                {
                    ImGui.BeginDisabled();
                }

                if (ImGui.ArrowButton("##TagMoveUp", ImGuiDir.Up))
                {
                    tagUpdateCallback(PacketObjectTag.ActionType.MoveUp);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.tag.move_up.tt"));
                }

                if (tagIndex == 0)
                {
                    ImGui.EndDisabled();
                }

                ImGui.SameLine();
                if (tagIndex == mo.Tags.Count - 1)
                {
                    ImGui.BeginDisabled();
                }

                if (ImGui.ArrowButton("##TagMoveDown", ImGuiDir.Down))
                {
                    tagUpdateCallback(PacketObjectTag.ActionType.MoveDown);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.tag.move_down.tt"));
                }

                if (tagIndex == mo.Tags.Count - 1)
                {
                    ImGui.EndDisabled();
                }

                ImGui.SameLine();
                if (ImGui.ImageButton("##TagDuplicate", this.CopyIcon.Texture, new Vector2(16, 16), this.CopyIcon.ST, this.CopyIcon.UV))
                {
                    Tag t1 = t.Clone();
                    tagDuplicateCallback(t1);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.tag.duplicate.tt"));
                }

                ImGui.SameLine();
                if (ImGui.ImageButton("##TagDelete", this.DeleteIcon.Texture, new Vector2(16, 16), this.DeleteIcon.ST, this.DeleteIcon.UV))
                {
                    tagUpdateCallback(PacketObjectTag.ActionType.Delete);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.tag.delete.tt"));
                }

                Vector4 tagColor1 = t.Color1;
                ImGui.TextUnformatted(lang.Translate("ui.tag.clr1"));
                ImGui.SameLine();
                if (ImGui.ColorEdit4("##BGTagClr", ref tagColor1, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))
                {
                    t.Color1 = tagColor1;
                }

                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    tagUpdateCallback(PacketObjectTag.ActionType.Update);
                }

                if (t.Kind is Tag.TagKind.Shape or Tag.TagKind.Text)
                {
                    Vector4 tagColor2 = t.Color2;
                    ImGui.TextUnformatted(lang.Translate("ui.tag.clr2"));
                    ImGui.SameLine();
                    if (ImGui.ColorEdit4("##BorderTagClr", ref tagColor2, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))
                    {
                        t.Color2 = tagColor2;
                    }

                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        tagUpdateCallback(PacketObjectTag.ActionType.Update);
                    }
                }

                Vector4 tagColor3 = t.TextColor;
                ImGui.TextUnformatted(lang.Translate("ui.tag.clr3"));
                ImGui.SameLine();
                if (ImGui.ColorEdit4("##TextTagClr", ref tagColor3, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))
                {
                    t.TextColor = tagColor3;
                }

                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    tagUpdateCallback(PacketObjectTag.ActionType.Update);
                }

                bool tIsPublic = t.IsPublic;
                ImGui.TextUnformatted(lang.Translate("ui.tag.public"));
                ImGui.SameLine();
                if (ImGui.Checkbox("##TagIsPublic", ref tIsPublic))
                {
                    t.IsPublic = tIsPublic;
                    tagUpdateCallback(PacketObjectTag.ActionType.Update);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.tag.public.tt"));
                }

                ImGui.TextUnformatted(lang.Translate("ui.tag.type"));
                ImGui.SameLine();
                string[] types = { lang.Translate("ui.tag.type.none"), lang.Translate("ui.tag.type.shape"), lang.Translate("ui.tag.type.text"), lang.Translate("ui.tag.type.asset"), lang.Translate("ui.tag.type.b64img") };
                int tagType = (int)t.Kind;
                ImGui.PushItemWidth(192);
                if (ImGui.Combo("##TagTypeCombo", ref tagType, types, types.Length))
                {
                    t.Kind = (Tag.TagKind)tagType;
                    tagUpdateCallback(PacketObjectTag.ActionType.Update);
                }

                ImGui.PopItemWidth();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.tag.type.tt"));
                }

                switch (t.Kind)
                {
                    case Tag.TagKind.Shape:
                    {
                        ImGui.TextUnformatted(lang.Translate("ui.tag.shape"));
                        ImGui.SameLine();
                        string[] shapeTypes = Enum.GetValues<Tag.ShapeKind>().Select(x => lang.Translate($"ui.tag.shape.type.{Enum.GetName(x).ToLower()}")).ToArray();
                        int currentShapeIndex = (int)t.Shape;
                        ImGui.PushItemWidth(192);
                        if (ImGui.BeginCombo("##TagShapeCombo", shapeTypes[currentShapeIndex]))
                        {
                            Tag.ShapeKind[] shapeKinds = Enum.GetValues<Tag.ShapeKind>();
                            for (int i = 0; i < shapeKinds.Length; i++)
                            {
                                Tag.ShapeKind shapeKind = shapeKinds[i];
                                bool selected = i == currentShapeIndex;
                                bool selectableClicked = ImGui.Selectable("##TagShapeComboItem_" + i, selected);
                                ImGui.SameLine();
                                float uvStep = 1.0f / 22.0f;
                                float uvStart = uvStep * i;
                                Vector2 beforeLocalTagImage = ImGui.GetCursorScreenPos();
                                ImGui.Image(this.TagShapes, new Vector2(16, 16), new Vector2(uvStart, 0), new Vector2(uvStart + uvStep, 0.5f), t.Color1);
                                ImGui.SetCursorScreenPos(beforeLocalTagImage);
                                ImGui.Image(this.TagShapes, new Vector2(16, 16), new Vector2(uvStart, 0.5f), new Vector2(uvStart + uvStep, 1), t.Color2);
                                ImGui.SameLine();
                                ImGui.Text(shapeTypes[i]);
                                if (selectableClicked)
                                {
                                    ImGui.SetItemDefaultFocus();
                                    currentShapeIndex = i;
                                    t.Shape = (Tag.ShapeKind)i;
                                    tagUpdateCallback(PacketObjectTag.ActionType.Update);
                                }
                            }

                            ImGui.EndCombo();
                        }

                        ImGui.PopItemWidth();
                        break;
                    }

                    case Tag.TagKind.Text:
                    {
                        float borderRounding = t.BorderRounding;
                        if (ImGui.SliderFloat("##TagTextBorderRounding", ref borderRounding, 0f, 16f))
                        {
                            t.BorderRounding = borderRounding;
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.tag.border_rounding.tt"));
                        }

                        if (ImGui.IsItemDeactivatedAfterEdit())
                        {
                            tagUpdateCallback(PacketObjectTag.ActionType.Update);
                        }

                        break;
                    }

                    case Tag.TagKind.CustomImageAsset:
                    {
                        if (ImGuiHelper.ImAssetRecepticle(lang, t.AssetID, this.AssetImageIcon, new Vector2(0, 24), static x => x.Type == AssetType.Texture, out bool mouseOver) && this._draggedRef != null && this._draggedRef.Type == AssetType.Texture)
                        {
                            tagAssetHoverCallback();
                        }

                        if (mouseOver)
                        {
                            ImGui.SetTooltip(lang.Translate("ui.tag.custom_asset.tt"));
                        }

                        if (ImGui.Button(lang.Translate("ui.tag.custom_asset.delete")))
                        {
                            t.AssetID = Guid.Empty;
                            tagUpdateCallback(PacketObjectTag.ActionType.Update);
                        }

                        break;
                    }

                    case Tag.TagKind.CustomImageB64:
                    {
                        ImGui.Button(lang.Translate("ui.tag.custom_b64.action_" + (string.IsNullOrEmpty(t.EmbedB64Image) ? "upload" : "modify")), new Vector2(ImGui.GetContentRegionAvail().X, 24));
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.tag.custom_b64.tt"));
                            tagB64HoverCallback();
                        }

                        if (ImGui.Button(lang.Translate("ui.tag.custom_b64.delete")))
                        {
                            t.EmbedB64Image = string.Empty;
                            tagUpdateCallback(PacketObjectTag.ActionType.Update);
                        }

                        break;
                    }
                }
            }

            ImGui.EndChild();
        }

        private unsafe void RenderObjectProperties(SimpleLanguage lang, GuiState state, double time)
        {
            if (state.clientMap != null)
            {
                if (ImGui.Begin(lang.Translate("ui.properties") + "###Properties"))
                {
                    List<MapObject> os = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects;
                    if (os.Count > 0)
                    {
                        MapObject mo = os[0];

                        bool isAdmin = Client.Instance.IsAdmin;
                        bool canEdit = isAdmin || mo.CanEdit(Client.Instance.ID);

                        Vector2 v = ImGui.GetWindowSize();
                        ImGui.SetCursorPosX(v.X - 32);

                        if (!canEdit)
                        {
                            ImGui.BeginDisabled();
                        }

                        if (this.DeleteIcon.ImImageButton("btnDeleteObject", new Vector2(16, 16)) && canEdit)
                        {
                            PacketDeleteMapObject pdmo = new PacketDeleteMapObject() { DeletedObjects = SelectedToPacket2(os), IsServer = false, Session = Client.Instance.SessionID };
                            pdmo.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.delete"));
                        }

                        if (!canEdit)
                        {
                            ImGui.EndDisabled();
                        }

                        ImGui.SameLine();
                        ImGui.SetCursorPosX(8);
                        ImGui.TextDisabled(mo.ID.ToString());

                        if (!canEdit)
                        {
                            ImGui.BeginDisabled();
                        }

                        string n = mo.Name;
                        bool nVisible = mo.IsNameVisible;
                        if (ImGui.Checkbox("##NameVisible", ref nVisible))
                        {
                            os.ForEach(x => x.IsNameVisible = nVisible);
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.IsNameVisible, Data = SelectedToPacket3(os, nVisible), IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.name_visible.tt"));
                        }

                        ImGui.SameLine();
                        if (ImGui.ColorButton("##NameColor", ((Vector4)mo.NameColor)))
                        {
                            this._editedMapObject = mo;
                            state.changeNameColorPopup = true;
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.name_color.tt"));
                        }

                        ImGui.SameLine();
                        if (ImGui.InputText(lang.Translate("ui.properties.name") + "###Name", ref n, 255))
                        {
                            os.ForEach(x => x.Name = n);
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.Name, Data = SelectedToPacket3(os, n), IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        Vector3 oPos = mo.Position;
                        if (ImGui.InputFloat3(lang.Translate("ui.properties.position") + "###Position", ref oPos) && canEdit)
                        {
                            Vector3 deltaMain = oPos - mo.Position;
                            os.ForEach(x => x.Position += deltaMain);
                            List<(Guid, Guid, Vector4)> changes = SelectedToPacketEx(os, x => new Vector4(x.Position, 1.0f));
                            PacketChangeObjectModelMatrix pmo = new PacketChangeObjectModelMatrix() { IsServer = false, Session = Client.Instance.SessionID, MovedObjects = changes, MovementInducerID = Client.Instance.ID, Type = PacketChangeObjectModelMatrix.ChangeType.Position };
                            pmo.Send();
                        }

                        Vector3 oScale = mo.Scale;
                        if (ImGui.InputFloat3(lang.Translate("ui.properties.scale") + "###Scale", ref oScale) && canEdit)
                        {
                            Vector3 deltaMain = oScale - mo.Scale;
                            os.ForEach(x => x.Scale += deltaMain);
                            List<(Guid, Guid, Vector4)> changes = SelectedToPacketEx(os, x => new Vector4(x.Scale, 1.0f));
                            PacketChangeObjectModelMatrix pmo = new PacketChangeObjectModelMatrix() { IsServer = false, Session = Client.Instance.SessionID, MovedObjects = changes, MovementInducerID = Client.Instance.ID, Type = PacketChangeObjectModelMatrix.ChangeType.Scale };
                            pmo.Send();
                        }

                        // Can't do multi-select rotation changes because quaternion delta is whacky
                        Vector4 oRot = new Vector4(mo.Rotation.X, mo.Rotation.Y, mo.Rotation.Z, mo.Rotation.W);
                        if (ImGui.InputFloat4(lang.Translate("ui.properties.rotation") + "###Rotation", ref oRot) && canEdit)
                        {
                            mo.Rotation = new Quaternion(oRot.X, oRot.Y, oRot.Z, oRot.W);
                            List<(Guid, Guid, Vector4)> changes = new List<(Guid, Guid, Vector4)>() { (mo.MapID, mo.ID, oRot) };
                            PacketChangeObjectModelMatrix pmo = new PacketChangeObjectModelMatrix() { IsServer = false, Session = Client.Instance.SessionID, MovedObjects = changes, MovementInducerID = Client.Instance.ID, Type = PacketChangeObjectModelMatrix.ChangeType.Rotation };
                            pmo.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.rotation.tt"));
                        }

                        if (!canEdit)
                        {
                            ImGui.EndDisabled();
                        }

                        if (!isAdmin)
                        {
                            ImGui.BeginDisabled();
                        }

                        Client.Instance.TryGetClientNamesArray(mo.OwnerID, out int id, out string[] names, out Guid[] ids);
                        if (ImGui.Combo(lang.Translate("ui.properties.owner") + "###Owner", ref id, names, names.Length))
                        {
                            Guid nOID = ids[id];
                            os.ForEach(x => x.OwnerID = nOID);
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.Owner, Data = SelectedToPacket3(os, nOID), IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        int layer = mo.MapLayer;
                        if (ImGui.SliderInt(lang.Translate("ui.properties.layer") + "###Layer", ref layer, -2, 2))
                        {
                            os.ForEach(x => x.MapLayer = layer);
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.MapLayer, Data = SelectedToPacket3(os, layer), IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.layer.tt"));
                        }

                        bool mEnableLights = mo.LightsEnabled;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.enable_lights") + "###Enable Lights", ref mEnableLights))
                        {
                            os.ForEach(x => x.LightsEnabled = mEnableLights);
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.LightsEnabled, Data = SelectedToPacket3(os, mEnableLights), IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.enable_lights.tt"));
                        }

                        ImGui.SameLine();
                        bool mCastShadows = mo.LightsCastShadows;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.cast_shadows") + "###Cast Shadows", ref mCastShadows))
                        {
                            os.ForEach(x => x.LightsCastShadows = mCastShadows);
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.LightsCastShadows, Data = SelectedToPacket3(os, mCastShadows), IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.cast_shadows.tt"));
                        }

                        bool mSelfShadows = mo.LightsSelfCastsShadow;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.self_shadow") + "###Cast Own Shadows", ref mSelfShadows))
                        {
                            os.ForEach(x => x.LightsSelfCastsShadow = mSelfShadows);
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.SelfCastsShadow, Data = SelectedToPacket3(os, mSelfShadows), IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.self_shadow.tt"));
                        }

                        bool mCastsShadows = mo.CastsShadow;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.casts_shadow") + "###Casts Shadow", ref mCastsShadows))
                        {
                            os.ForEach(x => x.CastsShadow = mCastsShadows);
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.CastsShadow, Data = SelectedToPacket3(os, mCastsShadows), IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.casts_shadow.tt"));
                        }

                        bool mIsInfo = mo.IsInfoObject;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.is_info") + "###Is Info", ref mIsInfo))
                        {
                            os.ForEach(x => x.IsInfoObject = mIsInfo);
                            new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.IsInfo, Data = SelectedToPacket3(os, mIsInfo) }.Send();
                        }

                        bool mDoNoDraw = mo.DoNotRender;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.do_no_draw") + "###Do Not Draw", ref mDoNoDraw))
                        {
                            os.ForEach(x => x.DoNotRender = mDoNoDraw);
                            new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.DoNotDraw, Data = SelectedToPacket3(os, mDoNoDraw) }.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.do_no_draw.tt"));
                        }

                        bool mHideSelection = mo.HideFromSelection;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.hide_selection") + "###HideSelection", ref mHideSelection))
                        {
                            os.ForEach(x => x.HideFromSelection = mHideSelection);
                            new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.HideSelection, Data = SelectedToPacket3(os, mHideSelection) }.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.hide_selection.tt"));
                        }

                        if (!isAdmin)
                        {
                            ImGui.EndDisabled();
                        }

                        if (!canEdit)
                        {
                            ImGui.BeginDisabled();
                        }

                        bool mIsCrossed = mo.IsCrossedOut;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.crossed") + "###Crossed Out", ref mIsCrossed))
                        {
                            os.ForEach(x => x.IsCrossedOut = mIsCrossed);
                            new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.IsCrossedOut, Data = SelectedToPacket3(os, mIsCrossed) }.Send();
                        }

                        ImGui.Text(lang.Translate("ui.properties.tint_color"));
                        ImGui.SameLine();
                        Vector4 tClr = ((Vector4)mo.TintColor);
                        if (ImGui.ColorButton("##TintColorChangeBtn_" + mo.ID, tClr))
                        {
                            this._editedMapObject = mo;
                            state.changeTintColorPopup = true;
                        }

                        bool mNoNameplateBg = mo.DisableNameplateBackground;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.no_nameplate_bg") + "###Disable Nameplate Background", ref mNoNameplateBg))
                        {
                            os.ForEach(x => x.DisableNameplateBackground = mNoNameplateBg);
                            new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.DisableNameplateBackground, Data = SelectedToPacket3(os, mNoNameplateBg) }.Send();
                        }

                        if (!canEdit)
                        {
                            ImGui.EndDisabled();
                        }

                        if (isAdmin)
                        {
                            if (ImGuiHelper.ImAssetRecepticle(lang, mo.AssetID, this.AssetModelIcon, new Vector2(0, 24), static x => x.Type is AssetType.Model or AssetType.Texture, out bool mouseOver) && this._draggedRef != null && (this._draggedRef.Type == AssetType.Model || this._draggedRef.Type == AssetType.Texture))
                            {
                                state.objectModelHovered = mo;
                            }

                            if (mouseOver)
                            {
                                ImGui.SetTooltip(lang.Translate("ui.properties.model.tt"));
                            }

                            bool mHasCustomNameplate = mo.HasCustomNameplate;
                            if (ImGui.Checkbox(lang.Translate("ui.properties.has_custom_nameplate") + "###Has Custom Nameplate", ref mHasCustomNameplate))
                            {
                                os.ForEach(x => x.HasCustomNameplate = mHasCustomNameplate);
                                new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.HasCustomNameplate, Data = SelectedToPacket3(os, mHasCustomNameplate) }.Send();
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.properties.has_custom_nameplate.tt"));
                            }

                            if (ImGuiHelper.ImAssetRecepticle(lang, mo.CustomNameplateID, this.AssetImageIcon, new Vector2(0, 24), static x => x.Type == AssetType.Texture, out mouseOver) && this._draggedRef != null && this._draggedRef.Type == AssetType.Texture)
                            {
                                state.objectCustomNameplateHovered = mo;
                            }

                            if (mouseOver)
                            {
                                ImGui.SetTooltip(lang.Translate("ui.properties.custom_nameplate.tt"));
                            }

                            if (ImGuiHelper.ImAssetRecepticle(lang, mo.ShaderID, this.AssetShaderIcon, new Vector2(0, 24), static x => x.Type is AssetType.Shader or AssetType.GlslFragmentShader, out mouseOver) && this._draggedRef != null && this._draggedRef.Type is AssetType.Shader or AssetType.GlslFragmentShader)
                            {
                                state.objectCustomShaderHovered = mo;
                            }

                            if (mouseOver)
                            {
                                ImGui.SetTooltip(lang.Translate("ui.properties.custom_shader.tt"));
                            }

                            if (ImGui.Button(lang.Translate("ui.properties.custom_shader.delete")))
                            {
                                os.ForEach(x => x.ShaderID = Guid.Empty);
                                new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.ShaderID, Data = SelectedToPacket3(os, Guid.Empty) }.Send();
                            }
                        }

                        if (!canEdit)
                        {
                            ImGui.BeginDisabled();
                        }

                        if (ImGui.TreeNode(lang.Translate("ui.tags") + "###Tags"))
                        {
                            lock (mo.TagsLock)
                            {
                                int tagIndex = 0;
                                foreach (Tag t in mo.Tags)
                                {
                                    this.ImTagSettings(lang, mo, t, tagIndex,
                                        x => (x switch
                                        {
                                            PacketObjectTag.ActionType.Update => new PacketObjectTag() { MapID = mo.MapID, ObjectID = mo.ID, TagID = t.ID, TagData = t.Serialize(), Action = PacketObjectTag.ActionType.Update },
                                            PacketObjectTag.ActionType.MoveUp => new PacketObjectTag() { MapID = mo.MapID, ObjectID = mo.ID, TagID = t.ID, Action = PacketObjectTag.ActionType.MoveUp },
                                            PacketObjectTag.ActionType.MoveDown => new PacketObjectTag() { MapID = mo.MapID, ObjectID = mo.ID, TagID = t.ID, Action = PacketObjectTag.ActionType.MoveDown },
                                            PacketObjectTag.ActionType.Delete => new PacketObjectTag() { MapID = mo.MapID, ObjectID = mo.ID, TagID = t.ID, Action = PacketObjectTag.ActionType.Delete },
                                            _ => null
                                        }).Send(),
                                        x => new PacketObjectTag() { MapID = mo.MapID, ObjectID = mo.ID, TagID = x.ID, TagData = x.Serialize(), Action = PacketObjectTag.ActionType.Create }.Send(),
                                        () => 
                                        {
                                            state.tagCustomAssetImageHoveredOwner = mo;
                                            state.tagCustomAssetImageHovered = t;
                                        },
                                        () =>
                                        {
                                            state.tagCustomAssetImageHoveredOwner = mo;
                                            state.tagCustomB64ImageTextHovered = t;
                                        }
                                    );

                                    tagIndex += 1;
                                }
                            }

                            if (this.AddIcon.ImImageButton("btnAddTag", Vec12x12))
                            {
                                Tag t = new Tag()
                                {
                                    ID = Guid.NewGuid(),
                                    Text = string.Empty,
                                    Kind = Tag.TagKind.Shape,
                                    Shape = Tag.ShapeKind.Circle,
                                    Color1 = Color.DarkRed.Vec4(),
                                    Color2 = Color.Black.Vec4(),
                                    TextColor = Color.White.Vec4(),
                                    BorderRounding = 0f,
                                    AssetID = Guid.Empty,
                                    EmbedB64Image = string.Empty,
                                    IsPublic = false
                                };

                                new PacketObjectTag() { MapID = mo.MapID, ObjectID = mo.ID, TagID = t.ID, TagData = t.Serialize(), Action = PacketObjectTag.ActionType.Create }.Send();
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.tags.add.tt"));
                            }

                            ImGui.TreePop();
                        }

                        if (ImGui.TreeNode(lang.Translate("ui.bars") + "###Bars"))
                        {
                            float availX = ImGui.GetContentRegionAvail().X;
                            for (int i = 0; i < mo.Bars.Count; i++)
                            {
                                if (ImGui.BeginChild("##Bar_" + i, new Vector2(280, 64), ImGuiChildFlags.Borders))
                                {
                                    DisplayBar db = mo.Bars[i];
                                    float cVal = db.CurrentValue;
                                    float mVal = db.MaxValue;
                                    ImGui.PushItemWidth(100);
                                    if (ImBarInput("##DBValue_" + i, ref cVal, 0, db.MaxValue))
                                    {
                                        db.CurrentValue = cVal;
                                        PacketMapObjectBar pmob = new PacketMapObjectBar() { BarAction = PacketMapObjectBar.Action.Change, Index = i, MapID = mo.MapID, ContainerID = mo.ID, Session = Client.Instance.SessionID, IsServer = false, Bar = db };
                                        pmob.Send();
                                    }

                                    ImGui.SameLine();
                                    if (ImBarInput("##DBMax_" + i, ref mVal, 0, float.PositiveInfinity))
                                    {
                                        db.MaxValue = mVal;
                                        PacketMapObjectBar pmob = new PacketMapObjectBar() { BarAction = PacketMapObjectBar.Action.Change, Index = i, MapID = mo.MapID, ContainerID = mo.ID, Session = Client.Instance.SessionID, IsServer = false, Bar = db };
                                        pmob.Send();
                                    }

                                    ImGui.PopItemWidth();
                                    ImGui.SameLine();
                                    if (ImGui.ColorButton("##DBChangeColor_" + i, (Vector4)db.DrawColor))
                                    {
                                        this._editedBarIndex = i;
                                        this._editedMapObject = mo;
                                        this._editedBarColor = (Vector4)db.DrawColor;
                                        state.changeColorPopup = true;
                                    }

                                    ImGui.SameLine();
                                    if (this.DeleteIcon.ImImageButton("##BarDeleteBtn_" + i, Vec12x12))
                                    {
                                        PacketMapObjectBar pmob = new PacketMapObjectBar() { BarAction = PacketMapObjectBar.Action.Delete, Index = i, MapID = mo.MapID, ContainerID = mo.ID, Session = Client.Instance.SessionID, IsServer = false, Bar = db };
                                        pmob.Send();
                                    }

                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.bars.delete"));
                                    }

                                    string[] modes = new string[] { lang.Translate("ui.bars.mode.standard"), lang.Translate("ui.bars.mode.compact"), lang.Translate("ui.bars.mode.round") };
                                    int barRenderMode = (int)db.RenderMode;
                                    if (ImGui.Combo("##DBRenderMode_" + i, ref barRenderMode, modes, modes.Length))
                                    {
                                        db.RenderMode = (DisplayBar.DrawMode)barRenderMode;
                                        PacketMapObjectBar pmob = new PacketMapObjectBar() { BarAction = PacketMapObjectBar.Action.Change, Index = i, MapID = mo.MapID, ContainerID = mo.ID, Session = Client.Instance.SessionID, IsServer = false, Bar = db };
                                        pmob.Send();
                                    }
                                }

                                ImGui.EndChild();
                            }

                            if (this.AddIcon.ImImageButton("btnAddBar", Vec12x12))
                            {
                                Random rand = new Random();
                                Color hsv = (Color)new HSVColor((float)(rand.NextDouble() * 360), 1, 1);
                                DisplayBar db = new DisplayBar() { CurrentValue = 0, MaxValue = 100, DrawColor = hsv, RenderMode = DisplayBar.DrawMode.Default };
                                PacketMapObjectBar pmob = new PacketMapObjectBar() { BarAction = PacketMapObjectBar.Action.Add, Index = 0, MapID = mo.MapID, ContainerID = mo.ID, Session = Client.Instance.SessionID, IsServer = false, Bar = db };
                                pmob.Send();
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.bars.add"));
                            }

                            ImGui.TreePop();
                        }

                        if (ImGui.TreeNode(lang.Translate("ui.auras") + "###Auras"))
                        {
                            lock (mo.Lock)
                            {
                                for (int i = 0; i < mo.Auras.Count; i++)
                                {
                                    (float, Color) aura = mo.Auras[i];
                                    float aRange = aura.Item1;
                                    Color aClr = aura.Item2;

                                    if (this.DeleteIcon.ImImageButton("##AuraDeleteBtn_" + i, Vec12x12))
                                    {
                                        new PacketAura() { ActionType = PacketAura.Action.Delete, Index = i, MapID = mo.MapID, ObjectID = mo.ID }.Send();
                                    }

                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.auras.delete"));
                                    }

                                    ImGui.SameLine();
                                    ImGui.PushItemWidth(100);
                                    if (ImGui.InputFloat("##AUValue_" + i, ref aRange))
                                    {
                                        new PacketAura() { ActionType = PacketAura.Action.Update, Index = i, MapID = mo.MapID, ObjectID = mo.ID, AuraColor = aClr, AuraRange = aRange }.Send();
                                    }

                                    ImGui.PopItemWidth();
                                    ImGui.SameLine();
                                    if (ImGui.ColorButton("##AUChangeColor_" + i, (Vector4)aClr))
                                    {
                                        this._editedBarIndex = i;
                                        this._editedMapObject = mo;
                                        this._editedBarColor = (Vector4)aClr;
                                        state.changeAuraColorPopup = true;
                                    }
                                }
                            }

                            if (this.AddIcon.ImImageButton("btnAddAura", Vec12x12))
                            {
                                Random rand = new Random();
                                Color hsv = (Color)new HSVColor((float)(rand.NextDouble() * 360), 1, 1);
                                new PacketAura() { ActionType = PacketAura.Action.Add, MapID = mo.MapID, ObjectID = mo.ID, AuraColor = hsv, AuraRange = 30 }.Send();
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.auras.add"));
                            }

                            ImGui.TreePop();
                        }

                        if (!canEdit)
                        {
                            ImGui.EndDisabled();
                        }

                        if (Client.Instance.IsAdmin)
                        {
                            if (ImGui.TreeNode(lang.Translate("ui.particle_containers")))
                            {
                                lock (mo.Lock)
                                {
                                    foreach (ParticleContainer pc in mo.Particles.GetAllContainers())
                                    {
                                        if (ImGui.BeginChild($"##ParticleContainer_{pc.ID}", new Vector2(ImGui.GetContentRegionAvail().X, 0), ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.NavFlattened | ImGuiChildFlags.Borders))
                                        {
                                            if (ImGuiHelper.ImAssetRecepticle(lang, pc.SystemID, this.AssetParticleIcon, new Vector2(0, 28), x => x.Type == AssetType.ParticleSystem, out bool mouseOver) && this._draggedRef != null && this._draggedRef.Type == AssetType.ParticleSystem)
                                            {
                                                state.particleContainerHovered = pc;
                                            }

                                            if (mouseOver)
                                            {
                                                ImGui.SetTooltip(lang.Translate("ui.particle_containers.asset"));
                                            }

                                            ImGui.Text(lang.Translate("ui.particle_containers.offset"));
                                            Vector3 pOff = pc.ContainerPositionOffset;
                                            if (ImGui.DragFloat3("##ParticleContainerOffset_" + pc.ID, ref pOff, 0.01f))
                                            {
                                                pc.ContainerPositionOffset = pOff;
                                                new PacketParticleContainer() { ActionType = PacketParticleContainer.Action.Edit, Container = pc.Serialize(), MapID = mo.MapID, ObjectID = mo.ID, ParticleID = pc.ID }.Send();
                                            }

                                            bool pUseOrient = pc.UseContainerOrientation;
                                            if (ImGui.Checkbox(lang.Translate("ui.particle_containers.rotate") + "###ParticleContainerUseOrientation_" + pc.ID, ref pUseOrient))
                                            {
                                                pc.UseContainerOrientation = pUseOrient;
                                                new PacketParticleContainer() { ActionType = PacketParticleContainer.Action.Edit, Container = pc.Serialize(), MapID = mo.MapID, ObjectID = mo.ID, ParticleID = pc.ID }.Send();
                                            }

                                            bool pDoVRot = pc.RotateVelocityByOrientation;
                                            if (ImGui.Checkbox(lang.Translate("ui.particle.orient_velocity_by_container") + "###OrientVelocityByContainer_" + pc.ID, ref pDoVRot))
                                            {
                                                pc.RotateVelocityByOrientation = pDoVRot;
                                                new PacketParticleContainer() { ActionType = PacketParticleContainer.Action.Edit, Container = pc.Serialize(), MapID = mo.MapID, ObjectID = mo.ID, ParticleID = pc.ID }.Send();
                                            }

                                            if (ImGui.IsItemHovered())
                                            {
                                                ImGui.SetTooltip(lang.Translate("ui.particle.orient_velocity_by_container.tt"));
                                            }

                                            bool pActive = pc.IsActive;
                                            if (ImGui.Checkbox(lang.Translate("ui.particle_containers.active") + "###ParticleContainerIsActive_" + pc.ID, ref pActive))
                                            {
                                                pc.IsActive = pActive;
                                                new PacketParticleContainer() { ActionType = PacketParticleContainer.Action.Edit, Container = pc.Serialize(), MapID = mo.MapID, ObjectID = mo.ID, ParticleID = pc.ID }.Send();
                                            }

                                            if (!mo.AssetID.Equals(Guid.Empty))
                                            {
                                                if (Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(mo.AssetID, AssetType.Model, out Asset a) == AssetStatus.Return && a.ModelGlReady)
                                                {
                                                    ImGui.TextUnformatted(lang.Translate("ui.particle_containers.attachment"));
                                                    string[] arr = this._particleContainerModelAttachmentPointCache.GetAllMeshes(a, pc.AttachmentPoint, out int idx);
                                                    if (ImGui.Combo("##ParticleContainerAttachment_" + pc.ID, ref idx, arr, arr.Length))
                                                    {
                                                        pc.AttachmentPoint = arr[idx];
                                                        new PacketParticleContainer() { ActionType = PacketParticleContainer.Action.Edit, Container = pc.Serialize(), MapID = mo.MapID, ObjectID = mo.ID, ParticleID = pc.ID }.Send();
                                                    }

                                                    // Somewhat jank but if the bone index goes OOB it won't be processed anyways.
                                                    // Solution - don't have multiple armatures, there is no graceful way to make particle systems work with that as the cache will be overridden by the most recent armature used
                                                    // The cache can't be per armature for performance concerns
                                                    if (a.Model.GLMdl.Armatures.Count > 0)
                                                    {
                                                        string[] boneArr = a.Model.GLMdl.Armatures.OrderByDescending(x => x.UnsortedBones.Count).First().UnsortedBones.Select(x => x.Name).ToArray();
                                                        if (boneArr.Length > 0)
                                                        {
                                                            idx = pc.BoneAttachmentIndex;
                                                            ImGui.TextUnformatted(lang.Translate("ui.particle_containers.bone_attachment"));
                                                            if (ImGui.IsItemHovered())
                                                            {
                                                                ImGui.SetTooltip(lang.Translate("ui.particle_containers.bone_attachment.tt"));
                                                            }

                                                            if (ImGui.Combo("##ParticleContainerBoneAttachment_" + pc.ID, ref idx, boneArr, boneArr.Length))
                                                            {
                                                                pc.BoneAttachmentIndex = idx;
                                                                new PacketParticleContainer() { ActionType = PacketParticleContainer.Action.Edit, Container = pc.Serialize(), MapID = mo.MapID, ObjectID = mo.ID, ParticleID = pc.ID }.Send();
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                            if (ImGui.Button(lang.Translate("ui.particle_containers.delete") + "###DeleteParticleContainer_" + pc.ID))
                                            {
                                                new PacketParticleContainer() { ActionType = PacketParticleContainer.Action.Delete, MapID = mo.MapID, ObjectID = mo.ID, ParticleID = pc.ID }.Send();
                                            }
                                        }

                                        ImGui.EndChild();
                                    }
                                }

                                if (this.AddIcon.ImImageButton("btnAddParticleContainer", Vec12x12))
                                {
                                    ParticleContainer pc = new ParticleContainer(mo);
                                    new PacketParticleContainer() { ActionType = PacketParticleContainer.Action.Add, MapID = mo.MapID, ObjectID = mo.ID, Container = pc.Serialize() }.Send();
                                }

                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip(lang.Translate("ui.particle_containers.add"));
                                }

                                ImGui.TreePop();
                            }

                            if (ImGui.TreeNode(lang.Translate("ui.fast_lights") + "###FastLights"))
                            {
                                lock (mo.FastLightsLock)
                                {
                                    for (int i = 0; i < mo.FastLights.Count; i++)
                                    {
                                        FastLight light = mo.FastLights[i];

                                        Vector3 offset = light.Translation;
                                        Vector3 color = light.LightColor;
                                        float flSize = light.Radius;
                                        float flInt = light.Intensity;
                                        bool bEnable = light.Enabled;
                                        bool bUOT = light.UseObjectTransform;

                                        ImGui.Text(lang.Translate("ui.fast_light.offset"));
                                        if (ImGui.DragFloat3("##FLOffset_" + i, ref offset))
                                        {
                                            light.Translation = offset;
                                            new PacketFastLight() { ActionType = PacketFastLight.Action.Update, Index = i, MapID = mo.MapID, ObjectID = mo.ID, Light = light.Clone() }.Send();
                                        }

                                        ImGui.Text(lang.Translate("ui.fast_light.radius"));
                                        if (ImGui.SliderFloat("##FLSize_" + i, ref flSize, 0, 10))
                                        {
                                            light.Radius = flSize;
                                            new PacketFastLight() { ActionType = PacketFastLight.Action.Update, Index = i, MapID = mo.MapID, ObjectID = mo.ID, Light = light.Clone() }.Send();
                                        }

                                        ImGui.Text(lang.Translate("ui.fast_light.intensity"));
                                        if (ImGui.SliderFloat("##FLIntensity_" + i, ref flInt, 0, 10))
                                        {
                                            light.Intensity = flInt;
                                            new PacketFastLight() { ActionType = PacketFastLight.Action.Update, Index = i, MapID = mo.MapID, ObjectID = mo.ID, Light = light.Clone() }.Send();
                                        }

                                        if (ImGui.Checkbox(lang.Translate("ui.fast_light.enabled") + "###FLEnabled_" + i, ref bEnable))
                                        {
                                            light.Enabled = bEnable;
                                            new PacketFastLight() { ActionType = PacketFastLight.Action.Update, Index = i, MapID = mo.MapID, ObjectID = mo.ID, Light = light.Clone() }.Send();
                                        }

                                        if (ImGui.Checkbox(lang.Translate("ui.fast_light.use_object_rotation") + "###FLUOT_" + i, ref bUOT))
                                        {
                                            light.UseObjectTransform = bUOT;
                                            new PacketFastLight() { ActionType = PacketFastLight.Action.Update, Index = i, MapID = mo.MapID, ObjectID = mo.ID, Light = light.Clone() }.Send();
                                        }

                                        if (ImGui.ColorButton("##FLChangeColor_" + i, new Vector4(color, 1.0f)))
                                        {
                                            this._editedBarIndex = i;
                                            this._editedMapObject = mo;
                                            this._editedBarColor = new Vector4(color, 1.0f);
                                            this._initialEditedFastLightColor = color;
                                            state.changeFastLightColorPopup = true;
                                        }

                                        if (ImGui.Button(lang.Translate("ui.fast_light.delete") + "###FastLightDeleteBtn_" + i))
                                        {
                                            new PacketFastLight() { ActionType = PacketFastLight.Action.Delete, Index = i, MapID = mo.MapID, ObjectID = mo.ID }.Send();
                                        }

                                        ImGui.NewLine();
                                    }
                                }

                                if (this.AddIcon.ImImageButton("btnAddFastLight", Vec12x12))
                                {
                                    Random rand = new Random();
                                    HSVColor hsv = new HSVColor(rand.NextSingle() * 360, 1, 1);
                                    FastLight fl = new FastLight()
                                    {
                                        Offset = new Vector4(0, 0, 0, 0),
                                        Color = new Vector4(((Color)hsv).Vec3(), 1.0f)
                                    };

                                    new PacketFastLight() { ActionType = PacketFastLight.Action.Add, MapID = mo.MapID, ObjectID = mo.ID, Light = fl }.Send();
                                }

                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip(lang.Translate("ui.fast_light.add"));
                                }

                                ImGui.TreePop();
                            }
                        }

                        if (canEdit)
                        {
                            bool haveAnimations = mo.LastRenderModel != null && mo.LastRenderModel.IsAnimated;
                            if (!haveAnimations)
                            {
                                ImGui.BeginDisabled();
                            }

                            if (ImGui.TreeNode(lang.Translate("ui.animations") + "###Animations"))
                            {
                                if (haveAnimations)
                                {
                                    float aTotal = mo.AnimationContainer.CurrentAnimation?.Duration ?? 1;
                                    float aNow = mo.AnimationContainer.GetTime(time);
                                    ImGui.ProgressBar(aNow / aTotal, new Vector2(ImGui.GetContentRegionAvail().X - 48, 16), string.Empty);
                                    ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
                                    string aName = ImGuiHelper.TextOrEmpty(mo.AnimationContainer.CurrentAnimation?.Name ?? lang.Translate("ui.animation.none"));
                                    ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X / 2) - (ImGui.CalcTextSize(aName).X / 2));
                                    ImGui.TextUnformatted(aName);
                                    aName = "↓↓↓";
                                    ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X / 2) - (ImGui.CalcTextSize(aName).X / 2));
                                    ImGui.TextUnformatted(aName);
                                    string next = mo.AnimationContainer.LoopingAnimationName;
                                    next = string.IsNullOrEmpty(next) ? lang.Translate("ui.animation.none") : next;
                                    ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X / 2) - (ImGui.CalcTextSize(next).X / 2));
                                    ImGui.TextUnformatted(next);
                                    ImGui.PopStyleColor();

                                    bool bPaused = mo.AnimationContainer.Paused;
                                    if (ImGui.Checkbox(lang.Translate("ui.animation.paused"), ref bPaused))
                                    {
                                        mo.AnimationContainer.Paused = bPaused;
                                        if (bPaused)
                                        {
                                            new PacketAnimationRequest() { Action = PacketAnimationRequest.ActionType.TogglePause, Data = true, ObjectID = mo.ID, MapID = mo.MapID }.Send();
                                        }
                                        else
                                        {
                                            new PacketAnimationRequest() { Action = PacketAnimationRequest.ActionType.TogglePause, Data = false, ObjectID = mo.ID, MapID = mo.MapID }.Send();
                                        }
                                    }

                                    string[] anims = new string[mo.LastRenderModel.Animations.Count];
                                    for (int i = 0; i < anims.Length; ++i)
                                    {
                                        anims[i] = mo.LastRenderModel.Animations[i].Name;
                                    }

                                    this._cSelAnimation = Math.Min(this._cSelAnimation, anims.Length);
                                    if (ImGui.Combo("##TransitionToCombo", ref this._cSelAnimation, anims, anims.Length))
                                    {
                                        // NOOP
                                    }

                                    if (ImGui.Button(lang.Translate("ui.animtions.transition")))
                                    {
                                        mo.AnimationContainer.SwitchNow(mo.LastRenderModel, anims[this._cSelAnimation]);
                                        new PacketAnimationRequest() { Action = PacketAnimationRequest.ActionType.SwitchToAnimationNow, Data = anims[this._cSelAnimation], ObjectID = mo.ID, MapID = mo.MapID }.Send();
                                    }

                                    ImGui.SameLine();
                                    if (ImGui.Button(lang.Translate("ui.animtions.set_default")))
                                    {
                                        mo.AnimationContainer.SwitchNow(mo.LastRenderModel, anims[this._cSelAnimation]);
                                        mo.AnimationContainer.LoopingAnimationName = anims[this._cSelAnimation];
                                        new PacketAnimationRequest() { Action = PacketAnimationRequest.ActionType.SetDefaultAnimation, Data = anims[this._cSelAnimation], ObjectID = mo.ID, MapID = mo.MapID }.Send();
                                    }

                                    ImGui.TextUnformatted(lang.Translate("ui.animations.play_rate"));
                                    float asf = mo.AnimationContainer.AnimationPlayRate;
                                    if (ImGui.SliderFloat("##PlayRate", ref asf, -2.0f, 2.0f))
                                    {
                                        asf = Math.Clamp(asf, -10, 10);
                                        mo.AnimationContainer.AnimationPlayRate = asf;
                                        new PacketAnimationRequest() { Action = PacketAnimationRequest.ActionType.SetPlayRate, Data = asf, ObjectID = mo.ID, MapID = mo.MapID }.Send();
                                    }
                                }

                                ImGui.TreePop();
                            }

                            if (!haveAnimations)
                            {
                                ImGui.EndDisabled();
                            }

                            if (Client.Instance.IsAdmin)
                            {
                                if (ImGui.TreeNode(lang.Translate("ui.2dshadow") + "###2D Shadows and Light"))
                                {
                                    bool mis2DShadowViewpoint = mo.IsShadow2DViewpoint;
                                    if (ImGui.Checkbox(lang.Translate("ui.object_is_2dshadow_viewport") + "###Is 2D Shadow Viewport", ref mis2DShadowViewpoint))
                                    {
                                        mo.IsShadow2DViewpoint = mis2DShadowViewpoint;
                                        new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.IsShadow2DViewport, Data = SelectedToPacket3(os, mis2DShadowViewpoint) }.Send();
                                    }

                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.object_is_2dshadow_viewport.tt"));
                                    }

                                    ImGui.TextUnformatted(lang.Translate("ui.object_2dshadow_data"));
                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.object_2dshadow_data.tt"));
                                    }

                                    float mLightDim = mo.Shadow2DViewpointData.X;
                                    float mLightThreshold = mo.Shadow2DViewpointData.Y;
                                    ImGui.SetNextItemWidth(100);
                                    if (ImGui.SliderFloat("##Shadow2DRadiusDim", ref mLightDim, 0, mLightThreshold))
                                    {
                                        mo.Shadow2DViewpointData = new Vector2(mLightDim, mLightThreshold);
                                        new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.Shadow2DViewportData, Data = SelectedToPacket3(os, mo.Shadow2DViewpointData) }.Send();
                                    }

                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.object_2dshadow_data.dim.tt"));
                                    }

                                    ImGui.SameLine();
                                    ImGui.SetNextItemWidth(100);
                                    if (ImGui.InputFloat("##Shadow2DRadiusMax", ref mLightThreshold))
                                    {
                                        mo.Shadow2DViewpointData = new Vector2(mLightDim, mLightThreshold);
                                        new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.Shadow2DViewportData, Data = SelectedToPacket3(os, mo.Shadow2DViewpointData) }.Send();
                                    }

                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.object_2dshadow_data.max.tt"));
                                    }

                                    ImGui.NewLine();

                                    bool mis2dShadowLightSource = mo.IsShadow2DLightSource;
                                    if (ImGui.Checkbox(lang.Translate("ui.object_is_2dshadow_lightsource") + "###Is 2D Light", ref mis2dShadowLightSource))
                                    {
                                        mo.IsShadow2DLightSource = mis2dShadowLightSource;
                                        new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.IsShadow2DLightSource, Data = SelectedToPacket3(os, mis2dShadowLightSource) }.Send();
                                    }

                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.object_is_2dshadow_lightsource.tt"));
                                    }

                                    ImGui.TextUnformatted(lang.Translate("ui.object_2dlight_data"));
                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.object_2dlight_data.tt"));
                                    }

                                    float mLightsourceDim = mo.Shadow2DLightSourceData.X;
                                    float mLightsourceThreshold = mo.Shadow2DLightSourceData.Y;
                                    ImGui.SetNextItemWidth(100);
                                    if (ImGui.SliderFloat("##Light2DRadiusDim", ref mLightsourceDim, 0, mLightsourceThreshold))
                                    {
                                        mo.Shadow2DLightSourceData = new Vector2(mLightsourceDim, mLightsourceThreshold);
                                        new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.Shadow2DLightSourceData, Data = SelectedToPacket3(os, mo.Shadow2DLightSourceData) }.Send();
                                    }

                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.object_2dlight_data.dim.tt"));
                                    }

                                    ImGui.SameLine();
                                    ImGui.SetNextItemWidth(100);
                                    if (ImGui.InputFloat("##Light2DRadiusMax", ref mLightsourceThreshold))
                                    {
                                        mo.Shadow2DLightSourceData = new Vector2(mLightsourceDim, mLightsourceThreshold);
                                        new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.Shadow2DLightSourceData, Data = SelectedToPacket3(os, mo.Shadow2DLightSourceData) }.Send();
                                    }

                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.object_2dlight_data.max.tt"));
                                    }

                                    ImGui.TreePop();
                                }

                                if (ImGui.TreeNode(lang.Translate("ui.portal") + "###Portal"))
                                {
                                    bool oIsPortal = mo.IsPortal;
                                    if (ImGui.Checkbox(lang.Translate("ui.portal.is_portal") + "###Is Portal", ref oIsPortal))
                                    {
                                        mo.IsPortal = oIsPortal;
                                        new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.IsPortal, Data = SelectedToPacket3(os, oIsPortal) }.Send();
                                    }

                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.portal.is_portal.tt"));
                                    }

                                    ImGui.TextUnformatted(lang.Translate("ui.portal.size"));
                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.portal.size.tt"));
                                    }

                                    Vector3 oPortalScale = mo.PortalSize;
                                    if (ImGui.DragFloat3("##Portal Size", ref oPortalScale, 0.1f))
                                    {
                                        mo.PortalSize = oPortalScale;
                                        new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.PortalSize, Data = SelectedToPacket3(os, oPortalScale) }.Send();
                                    }

                                    if (ImGui.Button(lang.Translate("ui.portal.picker") + "###Pick object"))
                                    {
                                        Client.Instance.Frontend.Renderer.SelectionManager.ObjectPickerModeObjectID = mo.ID;
                                        Client.Instance.Frontend.Renderer.SelectionManager.IsObjectPickerModeForPortal = true;
                                    }

                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.portal.picker.tt"));
                                    }

                                    ImGui.SameLine();
                                    if (ImGui.Button(lang.Translate("ui.portal.clear") + "###Clear Portal"))
                                    {
                                        mo.PairedPortalID = Guid.Empty;
                                        mo.PairedPortalMapID = Guid.Empty;
                                        new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.LinkedPortalID, Data = new List<(Guid, Guid, object)>() { (state.clientMap.ID, mo.ID, Guid.Empty) } }.Send();
                                        new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.LinkedPortalMapID, Data = new List<(Guid, Guid, object)>() { (state.clientMap.ID, mo.ID, Guid.Empty) } }.Send();
                                    }

                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.portal.clear.tt"));
                                    }

                                    ImGui.TextUnformatted(lang.Translate("ui.portal.link"));
                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.portal.link.tt"));
                                    }

                                    ImGuiHelper.ImObjectReferenceFrame(lang, mo.PairedPortalID, new Vector2(0, 28), out _);

                                    ImGui.TreePop();
                                }
                            }

                        }

                        if (!canEdit)
                        {
                            ImGui.BeginDisabled();
                        }

                        bool mIsDescMarkdown = mo.UseMarkdownForDescription;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.markdown") + "###IsMarkdown", ref mIsDescMarkdown))
                        {
                            os.ForEach(x => x.UseMarkdownForDescription = mIsDescMarkdown);
                            new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.DescriptionIsMarkdown, Data = SelectedToPacket3(os, mIsDescMarkdown) }.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.markdown.tt"));
                        }

                        string d = mo.Description;
                        ImGui.Text(lang.Translate("ui.properties.description"));
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.description.tt"));
                        }

                        if (ImGuiHelper.InputTextMultilinePreallocated("objdesc", "###Description", ref d, ushort.MaxValue, new Vector2(v.X - 108, 256)))
                        {
                            mo.Description = d;
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.Description, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, d) }, IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        if (Client.Instance.IsAdmin)
                        {
                            ImGui.Text(lang.Translate("ui.properties.notes"));
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.properties.notes.tt"));
                            }

                            string dn = mo.Notes;
                            if (ImGuiHelper.InputTextMultilinePreallocated("objnote", "###Notes", ref dn, ushort.MaxValue, new Vector2(v.X - 108, 100)))
                            {
                                mo.Notes = dn;
                                PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.Notes, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, dn) }, IsServer = false, Session = Client.Instance.SessionID };
                                pmogd.Send();
                            }
                        }

                        if (ImGui.BeginChild("##Statuses", new Vector2(v.X - 16, 256), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoSavedSettings))
                        {
                            int cX = 0;
                            int cY = 0;
                            float aW = ImGui.GetWindowWidth();

                            Vector2 cursorNow = ImGui.GetCursorPos();

                            lock (mo.Lock)
                            {
                                foreach (KeyValuePair<string, (float, float)> kv in mo.StatusEffects)
                                {
                                    ImGui.SetCursorPos(cursorNow + new Vector2(cX, cY));
                                    Vector2 st = new Vector2(kv.Value.Item1, kv.Value.Item2);
                                    if (ImGui.ImageButton("##BtnRemoveStatus_" + kv.Key, this.StatusAtlas, Vec24x24, st, st + new Vector2(this._statusStepX, this._statusStepY)))
                                    {
                                        new PacketObjectStatusEffect() { MapID = state.clientMap.ID, ObjectID = mo.ID, EffectName = kv.Key, Remove = true }.Send();
                                    }

                                    cX += 40;
                                    if (cX + 40 > aW)
                                    {
                                        cX = 0;
                                        cY += 40;
                                    }
                                }
                            }

                            ImGui.SetCursorPos(cursorNow + new Vector2(cX, cY));
                            if (this.AddIcon.ImImageButton("##BtnAddStatus", Vec24x24))
                            {
                                this._editedMapObject = mo;
                                state.newStatusEffectPopup = true;
                            }
                        }

                        ImGui.EndChild();

                        if (!canEdit)
                        {
                            ImGui.EndDisabled();
                        }
                    }
                }

                ImGui.End();
            }
        }

        private string _objectSearchText = "";
        private unsafe void RenderObjectsList(GuiState state, SimpleLanguage lang)
        {
            if (Client.Instance.Frontend?.Renderer?.ObjectRenderer?.ObjectListObjectMouseOver != null)
            {
                Client.Instance.Frontend.Renderer.ObjectRenderer.ObjectListObjectMouseOver = null;
            }

            if (ImGui.Begin(lang.Translate("ui.objects") + "###Objects"))
            {
                Vector2 wC = ImGui.GetWindowSize();
                if (state.clientMap != null)
                {
                    void RenderObjectInfo(MapObject mo, bool isCurrentLayer)
                    {
                        bool selected = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Contains(mo);
                        bool boxSelect = Client.Instance.Frontend.Renderer.SelectionManager.BoxSelectCandidates.Contains(mo);
                        bool mouseOver = Client.Instance.Frontend.Renderer.ObjectRenderer.ObjectMouseOver == mo;
                        if (!Client.Instance.IsAdmin && !mo.CanEdit(Client.Instance.ID) && !Client.Instance.IsObserver)
                        {
                            return;
                        }

                        bool changedColor = selected || boxSelect || mouseOver;
                        if (changedColor)
                        {
                            Color c = mouseOver ? Color.RoyalBlue : boxSelect ? Color.SkyBlue : Color.Orange;
                            ImGui.PushStyleColor(ImGuiCol.Border, (Vector4)c);
                        }

                        if (ImGui.BeginChild("objNav_" + mo.ID.ToString(), new Vector2(wC.X - 32, 32), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoScrollbar))
                        {
                            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
                            if (this.GotoIcon.ImImageButton("btnGotoObj_self_" + mo.ID.ToString(), new Vector2(10, 10)) && !Client.Instance.Frontend.Renderer.SelectionManager.IsDraggingObjects)
                            {
                                Vector3 p = mo.Position;
                                Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
                                cam.Position = state.clientMap.Is2D ? new Vector3(p.X, p.Y, cam.Position.Z) : p - (cam.Direction * 5.0f);
                                cam.RecalculateData();
                                bool shift = ImGui.IsKeyDown(ImGuiKey.LeftShift);
                                if (!shift)
                                {
                                    Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Clear();
                                    Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Add(mo);
                                }
                                else
                                {
                                    if (!selected)
                                    {
                                        Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Add(mo);
                                    }
                                }
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.objects.goto"));
                            }

                            ImGui.PopStyleColor();
                            ImGui.SameLine();
                            ImGui.TextUnformatted((mo.Name ?? lang.Translate("ui.objects.unnamed")) + "(" + mo.ID + ")");
                        }

                        ImGui.EndChild();
                        bool mOver = ImGui.IsItemHovered();
                        if (mOver)
                        {
                            Client.Instance.Frontend.Renderer.ObjectRenderer.ObjectListObjectMouseOver = mo;
                        }

                        if (changedColor)
                        {
                            ImGui.PopStyleColor();
                        }
                    }

                    ImGui.SetNextItemWidth(wC.X - 64);
                    ImGui.InputText("##ObjectsSearchBar", ref this._objectSearchText, ushort.MaxValue, ImGuiInputTextFlags.EscapeClearsAll);
                    if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    {
                        ImGui.SetKeyboardFocusHere();
                        this._searchText = "";
                    }

                    ImGui.SameLine();
                    this.Search.ImImage(new Vector2(24, 24));

                    int currentLayer = Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer;
                    if (ImGui.TreeNode(lang.Translate("ui.object_layer." + currentLayer)))
                    {
                        foreach (MapObject mo in state.clientMap.IterateObjects(currentLayer).OrderBy(x => x.Name))
                        {
                            if (!string.IsNullOrEmpty(this._objectSearchText))
                            {
                                if (!mo.ID.ToString().Contains(this._objectSearchText, StringComparison.InvariantCultureIgnoreCase) && !mo.Name.Contains(this._objectSearchText, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    continue;
                                }
                            }

                            RenderObjectInfo(mo, true);
                        }

                        ImGui.TreePop();
                    }

                    if (Client.Instance.IsAdmin)
                    {
                        for (int i = -2; i <= 2; ++i)
                        {
                            if (i == currentLayer)
                            {
                                continue;
                            }

                            if (ImGui.TreeNode(lang.Translate("ui.object_layer." + i)))
                            {
                                foreach (MapObject mo in state.clientMap.IterateObjects(i).OrderBy(x => x.Name))
                                {
                                    if (!string.IsNullOrEmpty(this._objectSearchText))
                                    {
                                        if (!mo.ID.ToString().Contains(this._objectSearchText, StringComparison.InvariantCultureIgnoreCase) && !mo.Name.Contains(this._objectSearchText, StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            continue;
                                        }
                                    }

                                    RenderObjectInfo(mo, false);
                                }

                                ImGui.TreePop();
                            }
                        }
                    }
                }
            }

            ImGui.End();
        }

        private static readonly List<(Vector2, Vector2, Tag)> tempBufferForTagRendering = new List<(Vector2, Vector2, Tag)>();
        private unsafe void RenderObjectOverlays()
        {
            IEnumerable<MapObject> objectsSelected = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects;
            objectsSelected = !ImGui.GetIO().WantCaptureMouse
                ? objectsSelected.Concat(new[] { Client.Instance.Frontend.Renderer.ObjectRenderer.ObjectMouseOver }).Distinct()
                : objectsSelected.Concat(new[] { Client.Instance.Frontend.Renderer.ObjectRenderer.ObjectListObjectMouseOver }).Distinct();

            void RenderStatusEffects(MapObject mo, Vector3 screen, float tX = float.MaxValue)
            {
                lock (mo.Lock)
                {
                    int nEffects = mo.StatusEffects.Count;
                    float nW = MathF.Min(nEffects * 24, tX);
                    if (nW > 0)
                    {
                        float nH = MathF.Ceiling(nEffects * 24 / nW) * 24;

                        ImGui.SetNextWindowSize(new Vector2(nW, nH));
                        ImGui.SetNextWindowPos(new Vector2(screen.X - (nW / 2), screen.Y));
                        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.Zero);
                        ImGui.Begin("OverlayEffects_" + mo.ID.ToString(), ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoSavedSettings);
                        ImDrawListPtr imDrawList = ImGui.GetWindowDrawList();
                        Vector2 imCursor = ImGui.GetCursorScreenPos();
                        float cX = 0;
                        float cY = 0;
                        foreach ((float, float) eff in mo.StatusEffects.Values)
                        {
                            Vector2 st = new Vector2(eff.Item1, eff.Item2);
                            imDrawList.AddImage(this.StatusAtlas,
                                imCursor + new Vector2(cX, cY),
                                imCursor + new Vector2(cX, cY) + Vec24x24,
                                st,
                                st + new Vector2(this._statusStepX, this._statusStepY));

                            cX += 24;
                            if (cX + 24 > nW)
                            {
                                cX = 0;
                                cY += 24;
                            }
                        }

                        ImGui.PopStyleVar();
                        ImGui.PopStyleVar();
                        ImGui.End();
                    }
                }
            }

            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            // Object names and bars overlay
            foreach (MapObject mo in objectsSelected)
            {
                if (mo != null && mo.ClientRenderedThisFrame)
                {
                    bool renderName = mo.CanEdit(Client.Instance.ID) || Client.Instance.IsAdmin || Client.Instance.IsObserver || mo.IsNameVisible;
                    bool renderBars = mo.CanEdit(Client.Instance.ID) || Client.Instance.IsAdmin || Client.Instance.IsObserver;

                    if (!renderName && !renderBars)
                    {
                        continue;
                    }

                    mo.ClientGuiOverlayDrawnThisFrame = true;
                    bool is2d = Client.Instance.Frontend.Renderer.MapRenderer.IsOrtho;
                    float cbby = mo.ClientRaycastOOBB.Size.Y;
                    Vector3 screen = is2d ?
                        Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.ToScreenspace(mo.Position + new Vector3(0, cbby * 0.5f, 0)) :
                        Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.ToScreenspace(mo.Position + new Vector3(0, 0, 1));

                    float nS = ImGuiHelper.CalcTextSize(mo.Name).X;
                    float tX = nS;
                    tX = MathF.Max(128, tX + 16);
                    bool hasNp = mo.HasCustomNameplate && mo.CustomNameplateID != Guid.Empty;
                    Vector2 customPadding = ImGui.GetStyle().WindowPadding;
                    int barsHeight = 0;
                    bool layoutgenPrevBarWasInline = false;
                    float layoutgenPenX = 0;
                    for (int i = 0; i < mo.Bars.Count; i++)
                    {
                        DisplayBar db = mo.Bars[i];
                        switch (db.RenderMode)
                        {
                            case DisplayBar.DrawMode.Default:
                            {
                                layoutgenPrevBarWasInline = false;
                                layoutgenPenX = 0;
                                barsHeight += 16;
                                break;
                            }

                            case DisplayBar.DrawMode.Compact:
                            {
                                layoutgenPrevBarWasInline = false;
                                layoutgenPenX = 0;
                                barsHeight += 16;
                                break;
                            }

                            case DisplayBar.DrawMode.Round:
                            {
                                if (layoutgenPrevBarWasInline)
                                {
                                    if (layoutgenPenX + 56 > tX)
                                    {
                                        layoutgenPenX = 0;
                                        barsHeight += 48;
                                    }
                                }
                                else
                                {
                                    layoutgenPenX = 0;
                                    barsHeight += 48;
                                }


                                layoutgenPenX += 56;
                                layoutgenPrevBarWasInline = true;
                                break;
                            }
                        }
                    }

                    int h = (renderName ? 32 : 8) + barsHeight + (hasNp && !(renderBars && mo.Bars.Count > 0) ? -8 : 0);
                    ImGuiWindowFlags flags = ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoSavedSettings;
                    if (mo.DisableNameplateBackground)
                    {
                        flags |= ImGuiWindowFlags.NoBackground;
                    }

                    if (renderName)
                    {
                        RenderStatusEffects(mo, screen, tX);
                    }

                    tempBufferForTagRendering.Clear();
                    Vector2 tagWndSize = new Vector2(tX + 48, 26);
                    Vector2 tagWndLocalCursor = new Vector2(0, 0);
                    lock (mo.TagsLock)
                    {
                        foreach (Tag t in mo.Tags)
                        {
                            if (t.IsPublic || mo.CanEdit(Client.Instance.ID) || Client.Instance.IsAdmin || Client.Instance.IsObserver)
                            {
                                if (this.IdentifyTagRenderSizes(t, out Vector2 tagSize))
                                {
                                    Vector2 cursorAfterAddition = tagWndLocalCursor + (tagSize * Vector2.UnitX);
                                    if (tagWndLocalCursor.X >= tX + 48)
                                    {
                                        tagWndLocalCursor.Y += 26;
                                        tagWndLocalCursor.X = 0;
                                    }

                                    tempBufferForTagRendering.Add((tagWndLocalCursor, tagSize, t));
                                    tagWndLocalCursor.X += tagSize.X + 2;
                                    tagWndSize = Vector2.Max(tagWndLocalCursor + new Vector2(0, 26), tagWndSize);
                                }
                            }
                        }
                    }

                    if (tempBufferForTagRendering.Count > 0)
                    {
                        ImGui.SetNextWindowPos(new Vector2(screen.X - ((tX + 48) / 2), screen.Y - h - tagWndSize.Y));
                        ImGui.SetNextWindowSizeConstraints(tagWndSize, tagWndSize);
                        ImGui.SetNextWindowBgAlpha(0.0f);

                        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
                        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

                        if (ImGui.Begin("OverlayTags_" + mo.ID.ToString(), flags))
                        {
                            Vector2 start = ImGui.GetCursorScreenPos();
                            ImGui.Dummy(tagWndSize);
                            foreach ((Vector2, Vector2, Tag) tmpData in tempBufferForTagRendering)
                            {
                                ImGui.SetCursorScreenPos(start + tmpData.Item1);
                                this.RenderTagIntoList(ImGui.GetWindowDrawList(), tmpData.Item2, tmpData.Item3);
                            }
                        }

                        ImGui.End();
                        ImGui.PopStyleVar(2);
                    }

                    ImGui.SetNextWindowPos(new Vector2(screen.X - (tX / 2), screen.Y - h));
                    ImGui.SetNextWindowSizeConstraints(new Vector2(tX, 0), new Vector2(float.MaxValue, float.MaxValue));
                    if (hasNp)
                    {
                        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
                        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                    }

                    if (ImGui.Begin("Overlay_" + mo.ID.ToString(), flags | ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        if (hasNp)
                        {
                            if (Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(mo.CustomNameplateID, AssetType.Texture, out Asset a) == AssetStatus.Return && a != null && a.Type == AssetType.Texture && a.Texture != null && a.Texture.glReady)
                            {
                                Vector2 cPn = ImGui.GetCursorPos();
                                ImDrawListPtr backList = ImGui.GetWindowDrawList();
                                Vector2 oPs = ImGui.GetStyle().WindowPadding;
                                GL.Texture tex = a.Texture.GetOrCreateGLTexture(true, false, out VTT.Asset.Glb.TextureAnimation anim);
                                if (tex.IsAsyncReady)
                                {
                                    VTT.Asset.Glb.TextureAnimation.Frame frame = anim.FindFrameForIndex(double.NaN);
                                    Vector2 dc = ImGui.GetCursorScreenPos() - oPs;
                                    backList.AddImage(tex, dc, dc + new Vector2(tX, 32), frame.LocationUniform.Xy(), frame.LocationUniform.Xy() + frame.LocationUniform.Zw());
                                    ImGui.SetCursorPos(cPn + customPadding);
                                }
                            }
                        }

                        if (renderName)
                        {
                            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - customPadding.X + (tX * 0.5f) - (nS * 0.5f));

                            uint nClr = mo.NameColor.Abgr();
                            if (mo.NameColor.Alpha() < 0.5f)
                            {
                                nClr = ImGui.GetColorU32(ImGuiCol.Text);
                            }

                            ImGui.PushStyleColor(ImGuiCol.Text, nClr);
                            ImGui.TextUnformatted(mo.Name);
                            ImGui.PopStyleColor();
                        }

                        if (renderBars)
                        {
                            if (hasNp)
                            {
                                ImGui.Dummy(customPadding);
                            }

                            bool prevWasRound = false;
                            float penX = 0;
                            for (int i = 0; i < mo.Bars.Count; i++)
                            {
                                DisplayBar db = mo.Bars[i];
                                float mW = MathF.Max(112, tX - 16);
                                RenderBar(db.CurrentValue, db.MaxValue, db.RenderMode, db.DrawColor, hasNp, customPadding, mW, tX, ref prevWasRound, ref penX);
                            }
                        }
                    }

                    ImGui.End();
                    if (hasNp)
                    {
                        ImGui.PopStyleVar();
                        ImGui.PopStyleVar();
                    }

                    if (mo.IsInfoObject)
                    {
                        Vector2 tSizeMin = ImGui.CalcTextSize(mo.Description, 400f);
                        float tWM = MathF.Min(tSizeMin.X + 32, 400);
                        ImGui.SetNextWindowSize(new Vector2(tWM, tSizeMin.Y + 32));
                        ImGui.SetNextWindowPos(new Vector2(screen.X - (tWM / 2), screen.Y));
                        if (ImGui.Begin("InfoPanel_" + mo.ID.ToString(), flags))
                        {
                            ImGui.PushTextWrapPos();
                            ImGui.TextUnformatted(mo.Description);
                            ImGui.PopTextWrapPos();
                        }

                        ImGui.End();
                    }
                }
            }

            Map cMap = Client.Instance.CurrentMap;
            if (cMap == null)
            {
                return;
            }

            foreach (MapObject mo in cMap.IterateObjects(Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer))
            {
                if (!mo.ClientGuiOverlayDrawnThisFrame)
                {
                    if (mo.CanEdit(Client.Instance.ID))
                    {
                        bool is2d = Client.Instance.Frontend.Renderer.MapRenderer.IsOrtho;
                        float cbby = mo.ClientRaycastOOBB.Size.Y;
                        Vector3 screen = is2d ?
                            Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.ToScreenspace(mo.Position + new Vector3(0, cbby * 0.5f, 0)) :
                            Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.ToScreenspace(mo.Position + new Vector3(0, 0, 1));
                        RenderStatusEffects(mo, screen);
                    }
                }
                else
                {
                    mo.ClientGuiOverlayDrawnThisFrame = false;
                    continue;
                }
            }
        
            if (cMap != null && Client.Instance.Frontend.Renderer.SelectionManager.IsDraggingObjects && Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode == EditMode.Translate)
            {
                if (Client.Instance.Frontend.Renderer.ObjectRenderer.MovementMode == TranslationMode.Path)
                {
                    if (Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Count > 0)
                    {
                        MapObject mo = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects[0];
                        float lengthAccum = 0;
                        for (int i = 0; i < Client.Instance.Frontend.Renderer.SelectionManager.ObjectMovementPath.Count; ++i)
                        {
                            Vector3 start = Client.Instance.Frontend.Renderer.SelectionManager.ObjectMovementPath[i];
                            Vector3 end = i == Client.Instance.Frontend.Renderer.SelectionManager.ObjectMovementPath.Count - 1 ? mo.Position : Client.Instance.Frontend.Renderer.SelectionManager.ObjectMovementPath[i + 1];
                            lengthAccum += (end - start).Length();
                        }

                        lengthAccum *= cMap.GridUnit;
                        Vector3 half = mo.ClientDragMoveResetInitialPosition + ((mo.Position - mo.ClientDragMoveResetInitialPosition) * 0.5f);
                        Vector3 screen = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.ToScreenspace(half + Vector3.UnitZ);
                        if (screen.Z >= 0)
                        {
                            string text = lengthAccum.ToString("0.00");
                            Vector2 tLen3 = ImGuiHelper.CalcTextSize(text);
                            ImGui.SetNextWindowPos(screen.Xy() - (new Vector2(tLen3.X, tLen3.Y) / 2));
                            ImGuiWindowFlags flags = ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings;
                            if (ImGui.Begin("PathMovementOverlayData", flags))
                            {
                                ImGui.TextUnformatted(text);
                            }

                            ImGui.End();
                        }
                    }
                }
            }
        }

        public static void RenderBar(float current, float max, DisplayBar.DrawMode renderMode, ColorAbgr barColor, bool hasNp, Vector2 customPadding, float barWidth, float maxWidth, ref bool prevWasRound, ref float penX)
        {
            Vector2 cNow = ImGui.GetCursorPos();
            string dbText = current + "/" + max;
            Vector2 dbTextSize = ImGui.CalcTextSize(dbText);
            switch (renderMode)
            {
                case DisplayBar.DrawMode.Default:
                {
                    prevWasRound = false;
                    penX = 0;
                    ImGui.Dummy(new(0, 16));
                    ImGui.PushStyleColor(ImGuiCol.PlotHistogram, barColor);
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 2f);

                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (hasNp ? customPadding.X : 0));
                    ImGui.SetCursorPosY(cNow.Y);
                    ImGui.ProgressBar(current / max, new Vector2(barWidth, 12), string.Empty);
                    ImGui.PopStyleVar();

                    float tW = dbTextSize.X;
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 0, 0, 1));
                    if (Client.Instance.Settings.TextThickDropShadow)
                    {
                        for (int j = 0; j < 4; ++j)
                        {
                            ImGui.SetCursorPosY(cNow.Y - 5 + ((j & 1) << 1));
                            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (barWidth / 2) - (tW / 2) + (hasNp ? customPadding.X : 0) - 1 + ((j >> 1) << 1));
                            ImGui.Text(dbText);
                        }
                    }
                    else
                    {
                        ImGui.SetCursorPosY(cNow.Y - 3);
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (barWidth / 2) - (tW / 2) + (hasNp ? customPadding.X : 0) + 1);
                        ImGui.Text(dbText);
                    }

                    ImGui.PopStyleColor();
                    ImGui.SetCursorPosY(cNow.Y - 4);
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (barWidth / 2) - (tW / 2) + (hasNp ? customPadding.X : 0));
                    ImGui.PushStyleColor(ImGuiCol.Text, Vector4.One);
                    ImGui.Text(dbText);
                    ImGui.PopStyleColor();
                    ImGui.SetCursorPosY(cNow.Y + 16);

                    ImGui.PopStyleColor();
                    break;
                }

                case DisplayBar.DrawMode.Compact:
                {
                    prevWasRound = false;
                    penX = 0;
                    ImGui.PushStyleColor(ImGuiCol.PlotHistogram, barColor);
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f);
                    ImGui.SetCursorPosY(cNow.Y - 7);
                    float tW = dbTextSize.X;
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + barWidth - tW + (hasNp ? customPadding.X : 0));
                    ImGui.Text(dbText);
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (hasNp ? customPadding.X : 0));
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 5);
                    ImGui.ProgressBar(current / max, new Vector2(barWidth, 4));
                    ImGui.PopStyleVar();
                    ImGui.PopStyleColor();
                    break;
                }

                case DisplayBar.DrawMode.Round:
                {
                    if (prevWasRound)
                    {
                        if (penX + 56 > maxWidth)
                        {
                            penX = 0;
                        }
                        else
                        {
                            ImGui.SetCursorPos(cNow - new Vector2(-penX, 48));
                            cNow = ImGui.GetCursorPos();
                        }
                    }

                    prevWasRound = true;
                    penX += 56;
                    ImGui.Dummy(new Vector2(56, 48));
                    ImGui.SetCursorPos(cNow);
                    ImDrawListPtr drawList = ImGui.GetWindowDrawList();
                    Vector2 dCHere = ImGui.GetCursorScreenPos();
                    uint clrEmpty = ImGui.GetColorU32(ImGuiCol.FrameBg);
                    Vector2 center = dCHere + new Vector2(28, 24);
                    drawList.AddCircleFilled(center, 24, clrEmpty);
                    float progress = current / max;
                    if (float.IsNaN(progress))
                    {
                        progress = 0;
                    }

                    float angleStep = MathF.PI * 2 / 32f;
                    float angleNeeded = MathF.PI * 2 * progress;
                    if (progress > 0)
                    {
                        drawList.PathLineTo(center);
                        for (int k = 0; k <= 32; ++k)
                        {
                            bool stopIterHere = false;
                            float angleHere = angleStep * k;
                            if (angleHere >= angleNeeded)
                            {
                                angleHere = angleNeeded;
                                stopIterHere = true;
                            }

                            Vector2 v = new Vector2(-MathF.Sin(angleHere), MathF.Cos(angleHere)) * 24;
                            drawList.PathLineTo(center + v);
                            if (stopIterHere)
                            {
                                break;
                            }
                        }

                        drawList.PathFillConvex(barColor);
                        drawList.PathClear();
                    }

                    drawList.AddCircleFilled(center, 12, ImGui.GetColorU32(ImGuiCol.WindowBg));
                    if (Client.Instance.Settings.TextThickDropShadow)
                    {
                        for (int j = 0; j < 4; ++j)
                        {
                            drawList.AddText(center - (dbTextSize * 0.5f) + new Vector2(-1 + ((j & 1) * 2), -1 + ((j >> 1) * 2)), 0xff000000, dbText);
                        }
                    }
                    else
                    {
                        drawList.AddText(center - (dbTextSize * 0.5f) + Vector2.One, 0xff000000, dbText);
                    }

                    drawList.AddText(center - (dbTextSize * 0.5f), ImGui.GetColorU32(ImGuiCol.Text), dbText);

                    ImGui.SetCursorPosY(cNow.Y + 48);
                    ImGui.SetCursorPosX(ImGui.GetStyle().WindowPadding.X);
                    break;
                }
            }

        }

        private static readonly Dictionary<uint, (bool, bool, float)> activeSliders = new Dictionary<uint, (bool, bool, float)>();
        private static string activeSliderText = string.Empty;

        private static bool ImBarInput(string id, ref float val, float min, float max)
        {
            uint imId = ImGui.GetID(id);
            bool haveSliderEdited = activeSliders.TryGetValue(imId, out (bool, bool, float) result);
            if (haveSliderEdited && result.Item1)
            {
                if (!result.Item2)
                {
                    activeSliderText = val.ToString("0.000");
                    ImGui.SetKeyboardFocusHere(0);
                    activeSliders[imId] = (true, true, result.Item3);
                }

                bool rChanged = false;
                ImGui.InputText("##" + id + "_tedit", ref activeSliderText, 256, ImGuiInputTextFlags.AutoSelectAll);
                if (result.Item2)
                {
                    if (!ImGui.IsItemActive())
                    {
                        bool canParse = float.TryParse(activeSliderText, out float rFloat);
                        if (canParse)
                        {
                            if (activeSliderText[0] is '-' or '+')
                            {
                                rFloat = result.Item3 + rFloat;
                            }
                        }

                        activeSliders.Remove(imId);
                        val = rFloat;
                        return canParse && rFloat != result.Item3;
                    }
                }

                return rChanged;
            }

            int sliderIntVal = (int)MathF.Round(val);
            bool b = float.IsInfinity(max) ? ImGui.DragInt(id, ref sliderIntVal, 1, 0, int.MaxValue, "%d", ImGuiSliderFlags.NoInput) : ImGui.SliderInt(id, ref sliderIntVal, (int)min, (int)max, "%d", ImGuiSliderFlags.NoInput);

            if (ImGui.IsItemActive())
            {
                if (Client.Instance.Frontend.GameHandle.IsAnyControlDown())
                {
                    activeSliders[imId] = (true, false, val);
                    b = false;
                }
            }

            if (b)
            {
                val = sliderIntVal;
            }

            return b;
        }

        private static unsafe void CopyObjects(GuiState state)
        {
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(ms);
            foreach (MapObject mo in Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects)
            {
                mo.Clone().Serialize().Write(bw);
            }

            string s = Convert.ToBase64String(ms.ToArray());
            ImGui.SetClipboardText(s);
        }

        private static unsafe void PasteObjects(GuiState state)
        {
            if (!Client.Instance.IsAdmin)
            {
                return;
            }

            try
            {
                string s = ImGui.GetClipboardText();
                using MemoryStream ms = new MemoryStream(Convert.FromBase64String(s));
                using BinaryReader br = new BinaryReader(ms);
                List<MapObject> accumList = new List<MapObject>();
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    DataElement elem = new DataElement();
                    elem.Read(br);
                    MapObject mo = new MapObject();
                    mo.Deserialize(elem);
                    mo.ID = Guid.NewGuid();
                    mo.MapID = state.clientMap.ID;
                    mo.MapLayer = Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer;
                    accumList.Add(mo);
                }

                if (Client.Instance.Frontend.Renderer.MapRenderer.TerrainHit.HasValue)
                {
                    Vector3 vNewCenter = Client.Instance.Frontend.Renderer.MapRenderer.TerrainHit.Value;
                    if (state.clientMap.Is2D)
                    {
                        vNewCenter += new Vector3(0, 0, 0.01f);
                    }

                    Vector3 origCenter = default;
                    foreach (MapObject mo in accumList)
                    {
                        origCenter += mo.Position;
                    }

                    origCenter /= accumList.Count;
                    foreach (MapObject mo in accumList)
                    {
                        mo.Position = vNewCenter + (mo.Position - origCenter);
                        new PacketMapObject() { Obj = mo }.Send();
                    }
                }
            }
            catch
            {
                // NOOP - no idea what the user has in their clipboard, could be anything
            }
        }

        private class ParticleContainerModelAttachmentPointCache
        {
            private Guid _lastAssetID;
            private string[] _cache = Array.Empty<string>();

            public string[] GetAllMeshes(Asset a, string cname, out int index)
            {
                if (!Guid.Equals(this._lastAssetID, a.ID) || (this._cache.Length != a.Model.GLMdl.Meshes.Count + 1))
                {
                    this._lastAssetID = a.ID;
                    this._cache = a.Model.GLMdl.Meshes.Select(s => s.Name).Append(string.Empty).ToArray();
                }

                index = Array.IndexOf(this._cache, cname);
                if (index == -1)
                {
                    index = this._cache.Length - 1;
                }

                return this._cache;
            }
        }
    }
}
