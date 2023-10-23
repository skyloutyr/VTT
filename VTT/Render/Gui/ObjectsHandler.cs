namespace VTT.Render.Gui
{
    using ImGuiNET;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using VTT.Asset;
    using VTT.Control;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public partial class GuiRenderer
    {
        private unsafe void RenderObjectProperties(SimpleLanguage lang, GuiState state)
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

                        System.Numerics.Vector2 v = ImGui.GetWindowSize();
                        ImGui.SetCursorPosX(v.X - 32);

                        if (!canEdit)
                        {
                            ImGui.BeginDisabled();
                        }

                        if (ImGui.ImageButton("btnDeleteObject", this.DeleteIcon, new System.Numerics.Vector2(16, 16)) && canEdit)
                        {
                            List<(Guid, Guid)> l = new List<(Guid, Guid)>() { (mo.MapID, mo.ID) };
                            PacketDeleteMapObject pdmo = new PacketDeleteMapObject() { DeletedObjects = l, SenderID = Client.Instance.ID, IsServer = false, Session = Client.Instance.SessionID };
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
                            mo.IsNameVisible = nVisible;
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.IsNameVisible, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, nVisible) }, IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.name_visible.tt"));
                        }

                        ImGui.SameLine();
                        if (ImGui.ColorButton("##NameColor", ((System.Numerics.Vector4)mo.NameColor)))
                        {
                            this._editedMapObject = mo;
                            state.changeNameColorPopup = true;
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.name_color.tt"));
                        }

                        ImGui.SameLine();
                        if (ImGui.InputText(lang.Translate("ui.properties.name") + "###Name", ref n, ushort.MaxValue))
                        {
                            mo.Name = n;
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.Name, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, n) }, IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        System.Numerics.Vector3 oPos = mo.Position.SystemVector();
                        if (ImGui.InputFloat3(lang.Translate("ui.properties.position") + "###Position", ref oPos) && canEdit)
                        {
                            mo.Position = oPos.GLVector();
                            List<(Guid, Guid, Vector4)> changes = new List<(Guid, Guid, Vector4)>() { (mo.MapID, mo.ID, new Vector4(mo.Position, 1.0f)) };
                            PacketChangeObjectModelMatrix pmo = new PacketChangeObjectModelMatrix() { IsServer = false, Session = Client.Instance.SessionID, MovedObjects = changes, MovementInducerID = Client.Instance.ID, Type = PacketChangeObjectModelMatrix.ChangeType.Position };
                            pmo.Send();
                        }

                        System.Numerics.Vector3 oScale = mo.Scale.SystemVector();
                        if (ImGui.InputFloat3(lang.Translate("ui.properties.scale") + "###Scale", ref oScale) && canEdit)
                        {
                            mo.Scale = oScale.GLVector();
                            List<(Guid, Guid, Vector4)> changes = new List<(Guid, Guid, Vector4)>() { (mo.MapID, mo.ID, new Vector4(mo.Scale, 1.0f)) };
                            PacketChangeObjectModelMatrix pmo = new PacketChangeObjectModelMatrix() { IsServer = false, Session = Client.Instance.SessionID, MovedObjects = changes, MovementInducerID = Client.Instance.ID, Type = PacketChangeObjectModelMatrix.ChangeType.Scale };
                            pmo.Send();
                        }

                        System.Numerics.Vector4 oRot = new System.Numerics.Vector4(mo.Rotation.X, mo.Rotation.Y, mo.Rotation.Z, mo.Rotation.W);
                        if (ImGui.InputFloat4(lang.Translate("ui.properties.rotation") + "###Rotation", ref oRot) && canEdit)
                        {
                            mo.Rotation = new Quaternion(oRot.X, oRot.Y, oRot.Z, oRot.W);
                            List<(Guid, Guid, Vector4)> changes = new List<(Guid, Guid, Vector4)>() { (mo.MapID, mo.ID, oRot.GLVector()) };
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
                            mo.OwnerID = nOID;
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.Owner, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, nOID) }, IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        int layer = mo.MapLayer;
                        if (ImGui.SliderInt(lang.Translate("ui.properties.layer") + "###Layer", ref layer, -2, 2))
                        {
                            mo.MapLayer = layer;
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.MapLayer, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, layer) }, IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.layer.tt"));
                        }

                        bool mEnableLights = mo.LightsEnabled;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.enable_lights") + "###Enable Lights", ref mEnableLights))
                        {
                            mo.LightsEnabled = mEnableLights;
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.LightsEnabled, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, mEnableLights) }, IsServer = false, Session = Client.Instance.SessionID };
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
                            mo.LightsCastShadows = mCastShadows;
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.LightsCastShadows, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, mCastShadows) }, IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.cast_shadows.tt"));
                        }

                        bool mSelfShadows = mo.LightsSelfCastsShadow;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.self_shadow") + "###Cast Own Shadows", ref mSelfShadows))
                        {
                            mo.LightsSelfCastsShadow = mSelfShadows;
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.SelfCastsShadow, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, mSelfShadows) }, IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.self_shadow.tt"));
                        }

                        bool mCastsShadows = mo.CastsShadow;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.casts_shadow") + "###Casts Shadow", ref mCastsShadows))
                        {
                            mo.CastsShadow = mCastsShadows;
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.CastsShadow, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, mCastsShadows) }, IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.casts_shadow.tt"));
                        }

                        bool mIsInfo = mo.IsInfoObject;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.is_info") + "###Is Info", ref mIsInfo))
                        {
                            mo.IsInfoObject = mIsInfo;
                            new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.IsInfo, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, mIsInfo) } }.Send();
                        }

                        bool mDoNoDraw = mo.DoNotRender;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.do_no_draw") + "###Do Not Draw", ref mDoNoDraw))
                        {
                            mo.DoNotRender = mDoNoDraw;
                            new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.DoNotDraw, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, mDoNoDraw) } }.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.do_no_draw.tt"));
                        }

                        if (!isAdmin)
                        {
                            ImGui.EndDisabled();
                        }

                        bool mIsCrossed = mo.IsCrossedOut;
                        if (ImGui.Checkbox(lang.Translate("ui.properties.crossed") + "###Crossed Out", ref mIsCrossed))
                        {
                            mo.IsCrossedOut = mIsCrossed;
                            new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.IsCrossedOut, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, mIsCrossed) } }.Send();
                        }

                        ImGui.Text(lang.Translate("ui.properties.tint_color"));
                        ImGui.SameLine();
                        System.Numerics.Vector4 tClr = ((System.Numerics.Vector4)mo.TintColor);
                        if (ImGui.ColorButton("##TintColorChangeBtn_" + mo.ID, tClr))
                        {
                            this._editedMapObject = mo;
                            state.changeTintColorPopup = true;
                        }

                        if (isAdmin)
                        {
                            bool mouseOver = DrawObjectAssetRecepticle(mo, mo.AssetID, () => this._draggedRef.Type is AssetType.Model or AssetType.Texture);
                            if (mouseOver && this._draggedRef != null && (this._draggedRef.Type == AssetType.Model || this._draggedRef.Type == AssetType.Texture))
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
                                mo.HasCustomNameplate = mHasCustomNameplate;
                                new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.HasCustomNameplate, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, mHasCustomNameplate) } }.Send();
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.properties.has_custom_nameplate.tt"));
                            }

                            mouseOver = DrawObjectAssetRecepticle(mo, mo.CustomNameplateID, () => this._draggedRef.Type == AssetType.Texture, this.AssetImageIcon);
                            if (mouseOver && this._draggedRef != null && this._draggedRef.Type == AssetType.Texture)
                            {
                                state.objectCustomNameplateHovered = mo;
                            }

                            if (mouseOver)
                            {
                                ImGui.SetTooltip(lang.Translate("ui.properties.custom_nameplate.tt"));
                            }

                            mouseOver = DrawObjectAssetRecepticle(mo, mo.ShaderID, () => this._draggedRef.Type == AssetType.Shader, this.AssetShaderIcon);
                            if (mouseOver && this._draggedRef != null && this._draggedRef.Type == AssetType.Shader)
                            {
                                state.objectCustomShaderHovered = mo;
                            }

                            if (mouseOver)
                            {
                                ImGui.SetTooltip(lang.Translate("ui.properties.custom_shader.tt"));
                            }

                            if (ImGui.Button(lang.Translate("ui.properties.custom_shader.delete")))
                            {
                                mo.ShaderID = Guid.Empty;
                                new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.ShaderID, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, Guid.Empty) } }.Send();
                            }
                        }

                        if (!canEdit)
                        {
                            ImGui.BeginDisabled();
                        }

                        if (ImGui.TreeNode(lang.Translate("ui.bars") + "###Bars"))
                        {
                            for (int i = 0; i < mo.Bars.Count; i++)
                            {
                                DisplayBar db = mo.Bars[i];
                                float cVal = db.CurrentValue;
                                float mVal = db.MaxValue;
                                bool compact = db.Compact;
                                if (ImGui.ImageButton("##BarDeleteBtn_" + i, this.DeleteIcon, Vec12x12))
                                {
                                    PacketMapObjectBar pmob = new PacketMapObjectBar() { BarAction = PacketMapObjectBar.Action.Delete, Index = i, MapID = mo.MapID, ContainerID = mo.ID, Session = Client.Instance.SessionID, IsServer = false, Bar = db };
                                    pmob.Send();
                                }

                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip(lang.Translate("ui.bars.delete"));
                                }

                                ImGui.SameLine();
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
                                if (ImGui.ColorButton("##DBChangeColor_" + i, (System.Numerics.Vector4)db.DrawColor))
                                {
                                    this._editedBarIndex = i;
                                    this._editedMapObject = mo;
                                    this._editedBarColor = (System.Numerics.Vector4)db.DrawColor;
                                    state.changeColorPopup = true;
                                }

                                ImGui.SameLine();

                                if (ImGui.Checkbox("##DBCompact_" + i, ref compact))
                                {
                                    db.Compact = compact;
                                    PacketMapObjectBar pmob = new PacketMapObjectBar() { BarAction = PacketMapObjectBar.Action.Change, Index = i, MapID = mo.MapID, ContainerID = mo.ID, Session = Client.Instance.SessionID, IsServer = false, Bar = db };
                                    pmob.Send();
                                }

                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip(lang.Translate("ui.bars.compact"));
                                }

                            }

                            if (ImGui.ImageButton("btnAddBar", this.AddIcon, Vec12x12))
                            {
                                Random rand = new Random();
                                Color hsv = (Color)new HSVColor((float)(rand.NextDouble() * 360), 1, 1);
                                DisplayBar db = new DisplayBar() { CurrentValue = 0, MaxValue = 100, DrawColor = hsv, Compact = true };
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

                                    if (ImGui.ImageButton("##AuraDeleteBtn_" + i, this.DeleteIcon, Vec12x12))
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
                                    if (ImGui.ColorButton("##AUChangeColor_" + i, (System.Numerics.Vector4)aClr))
                                    {
                                        this._editedBarIndex = i;
                                        this._editedMapObject = mo;
                                        this._editedBarColor = (System.Numerics.Vector4)aClr;
                                        state.changeAuraColorPopup = true;
                                    }
                                }
                            }

                            if (ImGui.ImageButton("btnAddAura", this.AddIcon, Vec12x12))
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

                        if (Client.Instance.IsAdmin)
                        {
                            if (ImGui.TreeNode(lang.Translate("ui.particle_containers")))
                            {
                                lock (mo.Lock)
                                {
                                    foreach (ParticleContainer pc in mo.ParticleContainers.Values)
                                    {
                                        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
                                        var imScreenPos = ImGui.GetCursorScreenPos();
                                        var rectEnd = imScreenPos + new System.Numerics.Vector2(320, 24);
                                        bool mouseOver = ImGui.IsMouseHoveringRect(imScreenPos, rectEnd);
                                        uint bClr = mouseOver ? this._draggedRef != null && this._draggedRef.Type == AssetType.ParticleSystem ? ImGui.GetColorU32(ImGuiCol.HeaderHovered) : ImGui.GetColorU32(ImGuiCol.ButtonHovered) : ImGui.GetColorU32(ImGuiCol.Border);
                                        drawList.AddRect(imScreenPos, rectEnd, bClr);
                                        drawList.AddImage(this.AssetParticleIcon, imScreenPos + new System.Numerics.Vector2(4, 4), imScreenPos + new System.Numerics.Vector2(20, 20));
                                        Guid aId = pc.SystemID;
                                        string mdlTxt = "";
                                        int mdlTxtOffset = 0;
                                        if (Client.Instance.AssetManager.Refs.ContainsKey(aId))
                                        {
                                            AssetRef aRef = Client.Instance.AssetManager.Refs[aId];
                                            mdlTxt += aRef.Name;
                                            if (Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestPreview(aId, out AssetPreview ap) == AssetStatus.Return && ap != null)
                                            {
                                                GL.Texture tex = ap.GetGLTexture();
                                                if (tex != null)
                                                {
                                                    drawList.AddImage(tex, imScreenPos + new System.Numerics.Vector2(20, 4), imScreenPos + new System.Numerics.Vector2(36, 20));
                                                    mdlTxtOffset += 20;
                                                }
                                            }
                                        }

                                        mdlTxt += " (" + aId.ToString() + ")\0";
                                        drawList.PushClipRect(imScreenPos, rectEnd);
                                        drawList.AddText(imScreenPos + new System.Numerics.Vector2(20 + mdlTxtOffset, 4), ImGui.GetColorU32(ImGuiCol.Text), mdlTxt);
                                        drawList.PopClipRect();
                                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 28);
                                        if (mouseOver && this._draggedRef != null && this._draggedRef.Type == AssetType.ParticleSystem)
                                        {
                                            state.particleContainerHovered = pc;
                                        }

                                        if (mouseOver)
                                        {
                                            ImGui.SetTooltip(lang.Translate("ui.particle_containers.asset"));
                                        }

                                        ImGui.Text(lang.Translate("ui.particle_containers.offset"));
                                        System.Numerics.Vector3 pOff = pc.ContainerPositionOffset.SystemVector();
                                        if (ImGui.DragFloat3("##ParticleContainerOffset_" + pc.ID, ref pOff, 0.01f))
                                        {
                                            pc.ContainerPositionOffset = pOff.GLVector();
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

                                        ImGui.Text(lang.Translate("ui.particle_containers.attachment"));
                                        if (!mo.AssetID.Equals(Guid.Empty))
                                        {
                                            if (Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(mo.AssetID, AssetType.Model, out Asset a) == AssetStatus.Return && (a?.Model?.GLMdl?.glReady ?? false))
                                            {
                                                string[] arr = a.Model.GLMdl.Meshes.Select(s => s.Name).Append(string.Empty).ToArray();
                                                int idx = Array.IndexOf(arr, arr.FirstOrDefault(s => s.Equals(pc.AttachmentPoint), string.Empty));
                                                if (ImGui.Combo("##ParticleContainerAttachment_" + pc.ID, ref idx, arr, arr.Length))
                                                {
                                                    pc.AttachmentPoint = arr[idx];
                                                    new PacketParticleContainer() { ActionType = PacketParticleContainer.Action.Edit, Container = pc.Serialize(), MapID = mo.MapID, ObjectID = mo.ID, ParticleID = pc.ID }.Send();
                                                }
                                            }
                                        }

                                        if (ImGui.Button(lang.Translate("ui.particle_containers.delete") + "###DeleteParticleContainer_" + pc.ID))
                                        {
                                            new PacketParticleContainer() { ActionType = PacketParticleContainer.Action.Delete, MapID = mo.MapID, ObjectID = mo.ID, ParticleID = pc.ID }.Send();
                                        }

                                        ImGui.Separator();
                                    }
                                }

                                if (ImGui.ImageButton("btnAddParticleContainer", this.AddIcon, Vec12x12))
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

                                        System.Numerics.Vector3 offset = light.Translation.SystemVector();
                                        System.Numerics.Vector3 color = light.LightColor.SystemVector();
                                        float flSize = light.Radius;
                                        float flInt = light.Intensity;
                                        bool bEnable = light.Enabled;
                                        bool bUOT = light.UseObjectTransform;

                                        ImGui.Text(lang.Translate("ui.fast_light.offset"));
                                        if (ImGui.DragFloat3("##FLOffset_" + i, ref offset))
                                        {
                                            light.Translation = offset.GLVector();
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

                                        if (ImGui.ColorButton("##FLChangeColor_" + i, new System.Numerics.Vector4(color, 1.0f)))
                                        {
                                            this._editedBarIndex = i;
                                            this._editedMapObject = mo;
                                            this._editedBarColor = new System.Numerics.Vector4(color, 1.0f);
                                            this._initialEditedFastLightColor = color.GLVector();
                                            state.changeFastLightColorPopup = true;
                                        }

                                        if (ImGui.Button(lang.Translate("ui.fast_light.delete") + "###FastLightDeleteBtn_" + i))
                                        {
                                            new PacketFastLight() { ActionType = PacketFastLight.Action.Delete, Index = i, MapID = mo.MapID, ObjectID = mo.ID }.Send();
                                        }

                                        ImGui.NewLine();
                                    }
                                }

                                if (ImGui.ImageButton("btnAddFastLight", this.AddIcon, Vec12x12))
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

                        string d = mo.Description;
                        ImGui.Text(lang.Translate("ui.properties.description"));
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.properties.description.tt"));
                        }

                        if (ImGui.InputTextMultiline("###Description", ref d, ushort.MaxValue, new System.Numerics.Vector2(v.X - 108, 256)))
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
                            if (ImGui.InputTextMultiline("###Notes", ref dn, ushort.MaxValue, new System.Numerics.Vector2(v.X - 108, 100)))
                            {
                                mo.Notes = dn;
                                PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.Notes, Data = new List<(Guid, Guid, object)>() { (mo.MapID, mo.ID, dn) }, IsServer = false, Session = Client.Instance.SessionID };
                                pmogd.Send();
                            }
                        }

                        ImGui.BeginChild("##Statuses", new System.Numerics.Vector2(v.X - 16, 256), true, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoSavedSettings);
                        int cX = 0;
                        int cY = 0;
                        float aW = ImGui.GetWindowWidth();

                        System.Numerics.Vector2 cursorNow = ImGui.GetCursorPos();

                        lock (mo.Lock)
                        {
                            foreach (KeyValuePair<string, (float, float)> kv in mo.StatusEffects)
                            {
                                ImGui.SetCursorPos(cursorNow + new System.Numerics.Vector2(cX, cY));
                                System.Numerics.Vector2 st = new System.Numerics.Vector2(kv.Value.Item1, kv.Value.Item2);
                                if (ImGui.ImageButton("##BtnRemoveStatus_" + kv.Key, this.StatusAtlas, Vec24x24, st, st + new System.Numerics.Vector2(this._statusStepX, this._statusStepY)))
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

                        ImGui.SetCursorPos(cursorNow + new System.Numerics.Vector2(cX, cY));
                        if (ImGui.ImageButton("##BtnAddStatus", this.AddIcon, Vec24x24))
                        {
                            this._editedMapObject = mo;
                            state.newStatusEffectPopup = true;
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

            unsafe bool DrawObjectAssetRecepticle(MapObject mo, Guid aId, Func<bool> assetEval, GL.Texture iconTex = null)
            {
                ImDrawListPtr drawList = ImGui.GetWindowDrawList();
                var imScreenPos = ImGui.GetCursorScreenPos();
                var rectEnd = imScreenPos + new System.Numerics.Vector2(320, 24);
                bool mouseOver = ImGui.IsMouseHoveringRect(imScreenPos, rectEnd);
                uint bClr = mouseOver ? this._draggedRef != null && assetEval() ? ImGui.GetColorU32(ImGuiCol.HeaderHovered) : ImGui.GetColorU32(ImGuiCol.ButtonHovered) : ImGui.GetColorU32(ImGuiCol.Border);
                drawList.AddRect(imScreenPos, rectEnd, bClr);
                drawList.AddImage(iconTex ?? this.AssetModelIcon, imScreenPos + new System.Numerics.Vector2(4, 4), imScreenPos + new System.Numerics.Vector2(20, 20));
                string mdlTxt = "";
                int mdlTxtOffset = 0;
                if (Client.Instance.AssetManager.Refs.ContainsKey(aId))
                {
                    AssetRef aRef = Client.Instance.AssetManager.Refs[aId];
                    mdlTxt += aRef.Name;
                    if (Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestPreview(aId, out AssetPreview ap) == AssetStatus.Return && ap != null)
                    {
                        GL.Texture tex = ap.GetGLTexture();
                        if (tex != null)
                        {
                            drawList.AddImage(tex, imScreenPos + new System.Numerics.Vector2(20, 4), imScreenPos + new System.Numerics.Vector2(36, 20));
                            mdlTxtOffset += 20;
                        }
                    }
                }

                if (Guid.Equals(Guid.Empty, aId))
                {
                    mdlTxt = lang.Translate("generic.none");
                }
                else
                {
                    mdlTxt += " (" + aId.ToString() + ")\0";
                }

                drawList.PushClipRect(imScreenPos, rectEnd);
                drawList.AddText(imScreenPos + new System.Numerics.Vector2(20 + mdlTxtOffset, 4), ImGui.GetColorU32(ImGuiCol.Text), mdlTxt);
                drawList.PopClipRect();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 28);
                return mouseOver;
            }
        }

        private unsafe void RenderObjectsList(GuiState state, SimpleLanguage lang)
        {
            if (ImGui.Begin(lang.Translate("ui.objects") + "###Objects"))
            {
                System.Numerics.Vector2 wC = ImGui.GetWindowSize();
                if (state.clientMap != null)
                {
                    foreach (MapObject mo in state.clientMap.IterateObjects(Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer))
                    {
                        bool selected = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Contains(mo);
                        bool boxSelect = Client.Instance.Frontend.Renderer.SelectionManager.BoxSelectCandidates.Contains(mo);
                        bool mouseOver = Client.Instance.Frontend.Renderer.ObjectRenderer.ObjectMouseOver == mo;
                        if (!Client.Instance.IsAdmin && !mo.CanEdit(Client.Instance.ID) && !Client.Instance.IsObserver)
                        {
                            continue;
                        }

                        bool changedColor = selected || boxSelect || mouseOver;
                        if (changedColor)
                        {
                            Color c = mouseOver ? Color.RoyalBlue : boxSelect ? Color.SkyBlue : Color.Orange;
                            ImGui.PushStyleColor(ImGuiCol.Border, (System.Numerics.Vector4)c);
                        }

                        ImGui.BeginChild("objNav_" + mo.ID.ToString(), new System.Numerics.Vector2(wC.X - 32, 32), true, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoScrollbar);
                        ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.0f, 0.0f, 0.0f, 0.0f));
                        if (ImGui.ImageButton("btnGotoObj_self_" + mo.ID.ToString(), this.GotoIcon, new System.Numerics.Vector2(10, 10)) && !Client.Instance.Frontend.Renderer.SelectionManager.IsDraggingObjects)
                        {
                            Vector3 p = mo.Position;
                            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
                            cam.Position = p - (cam.Direction * 5.0f);
                            cam.RecalculateData(assumedUpAxis: Vector3.UnitZ);
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
                        ImGui.EndChild();
                        if (changedColor)
                        {
                            ImGui.PopStyleColor();
                        }
                    }
                }
            }

            ImGui.End();
        }

        private unsafe void RenderObjectOverlays()
        {
            IEnumerable<MapObject> objectsSelected = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects;
            if (!ImGui.GetIO().WantCaptureMouse)
            {
                objectsSelected = objectsSelected.Concat(new[] { Client.Instance.Frontend.Renderer.ObjectRenderer.ObjectMouseOver }).Distinct();
            }

            void RenderStatusEffects(MapObject mo, Vector3 screen, float tX = float.MaxValue)
            {
                lock (mo.Lock)
                {
                    int nEffects = mo.StatusEffects.Count;
                    float nW = MathF.Min(nEffects * 24, tX);
                    if (nW > 0)
                    {
                        float nH = MathF.Ceiling(nEffects * 24 / nW) * 24;

                        ImGui.SetNextWindowSize(new System.Numerics.Vector2(nW, nH));
                        ImGui.SetNextWindowPos(new System.Numerics.Vector2(screen.X - (nW / 2), screen.Y));
                        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, System.Numerics.Vector2.Zero);
                        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, System.Numerics.Vector2.Zero);
                        ImGui.Begin("OverlayEffects_" + mo.ID.ToString(), ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoSavedSettings);
                        ImDrawListPtr imDrawList = ImGui.GetWindowDrawList();
                        System.Numerics.Vector2 imCursor = ImGui.GetCursorScreenPos();
                        float cX = 0;
                        float cY = 0;
                        foreach ((float, float) eff in mo.StatusEffects.Values)
                        {
                            System.Numerics.Vector2 st = new System.Numerics.Vector2(eff.Item1, eff.Item2);
                            imDrawList.AddImage(this.StatusAtlas,
                                imCursor + new System.Numerics.Vector2(cX, cY),
                                imCursor + new System.Numerics.Vector2(cX, cY) + Vec24x24,
                                st,
                                st + new System.Numerics.Vector2(this._statusStepX, this._statusStepY));

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

            // Object names and bars overlay
            foreach (MapObject mo in objectsSelected)
            {
                if (mo != null && mo.ClientRenderedThisFrame)
                {
                    bool renderName = mo.CanEdit(Client.Instance.ID) || Client.Instance.IsAdmin || mo.IsNameVisible;
                    bool renderBars = mo.CanEdit(Client.Instance.ID) || Client.Instance.IsAdmin;

                    if (!renderName && !renderBars)
                    {
                        continue;
                    }

                    mo.ClientGuiOverlayDrawnThisFrame = true;
                    bool is2d = Client.Instance.Frontend.Renderer.MapRenderer.IsOrtho;
                    float cbby = mo.ClientBoundingBox.End.Y - mo.ClientBoundingBox.Start.Y;
                    Vector3 screen = is2d ?
                        Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.ToScreenspace(mo.Position + new Vector3(0, cbby * 0.5f, 0)) :
                        Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.ToScreenspace(mo.Position + new Vector3(0, 0, 1));

                    float nS = ImGuiHelper.CalcTextSize(mo.Name).X;
                    float tX = nS;
                    tX = MathF.Max(128, tX + 16);
                    bool hasNp = mo.HasCustomNameplate && mo.CustomNameplateID != Guid.Empty;
                    int h = (renderName ? 32 : 8) + ((renderBars ? mo.Bars.Count : 0) * 16) + (hasNp && !(renderBars && mo.Bars.Count > 0) ? -8 : 0);
                    ImGuiWindowFlags flags = ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoSavedSettings;

                    if (renderName)
                    {
                        RenderStatusEffects(mo, screen, tX);
                    }

                    System.Numerics.Vector2 customPadding = ImGui.GetStyle().WindowPadding;
                    ImGui.SetNextWindowPos(new System.Numerics.Vector2(screen.X - (tX / 2), screen.Y - h));
                    ImGui.SetNextWindowSize(new System.Numerics.Vector2(tX, h + (hasNp ? customPadding.Y : 0)));
                    if (hasNp)
                    {
                        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
                        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, System.Numerics.Vector2.Zero);
                    }

                    ImGui.Begin("Overlay_" + mo.ID.ToString(), flags);
                    if (hasNp)
                    {
                        if (Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(mo.CustomNameplateID, AssetType.Texture, out Asset a) == AssetStatus.Return && a != null && a.Type == AssetType.Texture && a.Texture != null && a.Texture.glReady)
                        {
                            System.Numerics.Vector2 cPn = ImGui.GetCursorPos();
                            ImDrawListPtr backList = ImGui.GetWindowDrawList();
                            System.Numerics.Vector2 oPs = ImGui.GetStyle().WindowPadding;
                            GL.Texture tex = a.Texture.GetOrCreateGLTexture(out VTT.Asset.Glb.TextureAnimation anim);
                            VTT.Asset.Glb.TextureAnimation.Frame frame = anim.FindFrameForIndex(double.NaN);
                            System.Numerics.Vector2 dc = ImGui.GetCursorScreenPos() - oPs;
                            backList.AddImage(tex, dc, dc + new System.Numerics.Vector2(tX, 32), frame.LocationUniform.Xy.SystemVector(), frame.LocationUniform.Xy.SystemVector() + frame.LocationUniform.Zw.SystemVector());
                            ImGui.SetCursorPos(cPn + customPadding);
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
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + customPadding.Y);
                        }

                        for (int i = 0; i < mo.Bars.Count; i++)
                        {
                            DisplayBar db = mo.Bars[i];
                            float mW = MathF.Max(112, tX - 16);
                            if (!db.Compact)
                            {
                                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, (System.Numerics.Vector4)db.DrawColor);
                                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 2);
                                float cYPreBar = ImGui.GetCursorPosY();

                                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (hasNp ? customPadding.X : 0));
                                ImGui.SetCursorPosY(cYPreBar);
                                ImGui.ProgressBar(db.CurrentValue / db.MaxValue, new System.Numerics.Vector2(mW, 12), string.Empty);
                                ImGui.PopStyleVar();

                                float tW = ImGuiHelper.CalcTextSize(db.CurrentValue + "/" + db.MaxValue).X;
                                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0, 0, 0, 1));
                                if (Client.Instance.Settings.TextThickDropShadow)
                                {
                                    for (int j = 0; j < 4; ++j)
                                    {
                                        ImGui.SetCursorPosY(cYPreBar - 5 + ((j & 1) << 1));
                                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (mW / 2) - (tW / 2) + (hasNp ? customPadding.X : 0) - 1 + ((j >> 1) << 1));
                                        ImGui.Text(db.CurrentValue + "/" + db.MaxValue);
                                    }
                                }
                                else
                                {
                                    ImGui.SetCursorPosY(cYPreBar - 3);
                                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (mW / 2) - (tW / 2) + (hasNp ? customPadding.X : 0) + 1);
                                    ImGui.Text(db.CurrentValue + "/" + db.MaxValue);
                                }

                                ImGui.PopStyleColor();
                                ImGui.SetCursorPosY(cYPreBar - 4);
                                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (mW / 2) - (tW / 2) + (hasNp ? customPadding.X : 0));
                                ImGui.PushStyleColor(ImGuiCol.Text, System.Numerics.Vector4.One);
                                ImGui.Text(db.CurrentValue + "/" + db.MaxValue);
                                ImGui.PopStyleColor();
                                ImGui.SetCursorPosY(cYPreBar + 16);

                                ImGui.PopStyleColor();
                            }
                            else
                            {
                                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, (System.Numerics.Vector4)db.DrawColor);
                                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5);
                                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 7);
                                float tW = ImGuiHelper.CalcTextSize(db.CurrentValue + "/" + db.MaxValue).X;
                                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + mW - tW + (hasNp ? customPadding.X : 0));
                                ImGui.Text(db.CurrentValue + "/" + db.MaxValue);
                                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (hasNp ? customPadding.X : 0));
                                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 5);
                                ImGui.ProgressBar(db.CurrentValue / db.MaxValue, new System.Numerics.Vector2(mW, 4));
                                ImGui.PopStyleVar();
                                ImGui.PopStyleColor();
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
                        System.Numerics.Vector2 tSizeMin = ImGui.CalcTextSize(mo.Description, 400f);
                        float tWM = MathF.Min(tSizeMin.X + 32, 400);
                        ImGui.SetNextWindowSize(new System.Numerics.Vector2(tWM, tSizeMin.Y + 32));
                        ImGui.SetNextWindowPos(new System.Numerics.Vector2(screen.X - (tWM / 2), screen.Y));
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
                        float cbby = mo.ClientBoundingBox.End.Y - mo.ClientBoundingBox.Start.Y;
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
                if (Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftControl) || Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.RightControl))
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

                if (Client.Instance.Frontend.Renderer.RulerRenderer.TerrainHit.HasValue || Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld.HasValue)
                {
                    Vector3 vNewCenter = Client.Instance.Frontend.Renderer.RulerRenderer.TerrainHit ?? Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld.Value;
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
    }
}
