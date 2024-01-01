namespace VTT.Render.Gui
{
    using ImGuiNET;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using VTT.Asset;
    using VTT.Control;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public partial class GuiRenderer
    {
        private unsafe void RenderMaps(SimpleLanguage lang, GuiState state)
        {
            if (ImGui.Begin(lang.Translate("ui.maps") + "###Maps"))
            {
                System.Numerics.Vector2 origPos = ImGui.GetCursorPos();
                if (state.clientMap != null)
                {
                    ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - 64);
                    if (ImGui.Button(lang.Translate("ui.maps.cam_snap") + "###Cam Snap"))
                    {
                        Vector3 cPos = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Position;
                        if (state.clientMap.Is2D)
                        {
                            cPos = new Vector3(cPos.X, cPos.Y, Client.Instance.Frontend.Renderer.MapRenderer.ZoomOrtho);
                        }

                        PacketCameraSnap pcs = new PacketCameraSnap() { CameraPosition = cPos, CameraDirection = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Direction };
                        pcs.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.cam_snap.tt"));
                    }
                }

                if (Client.Instance.IsAdmin && state.clientMap != null)
                {
                    ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - 64);
                    if (ImGui.Button(lang.Translate("ui.maps.cam_set") + "###Set Cam"))
                    {
                        Vector3 cPos = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Position;
                        if (state.clientMap.Is2D)
                        {
                            cPos = new Vector3(cPos.X, cPos.Y, Client.Instance.Frontend.Renderer.MapRenderer.ZoomOrtho);
                        }

                        Vector3 cDir = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Direction;
                        new PacketChangeMapData() { MapID = state.clientMap.ID, Data = cPos, Type = PacketChangeMapData.DataType.CameraPosition }.Send();
                        new PacketChangeMapData() { MapID = state.clientMap.ID, Data = cDir, Type = PacketChangeMapData.DataType.CameraDirection }.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.cam_set.tt"));
                    }

                    ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - 64);
                    if (ImGui.Button(lang.Translate("ui.maps.clear_marks") + "###Clear Marks"))
                    {
                        new PacketClearMarks().Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.clear_marks.tt"));
                    }

                    ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - 64);
                    if (ImGui.Button(lang.Translate("ui.maps.clear_drawings") + "###Clear Drawings"))
                    {
                        new PacketRemoveAllDrawings() { MapID = Client.Instance.CurrentMap.ID }.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.clear_drawings.tt"));
                    }

                    ImGui.SetCursorPos(origPos);

                    int cLayer = Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer;
                    if (ImGui.SliderInt(lang.Translate("ui.maps.layer") + "###Layer", ref cLayer, -2, 2))
                    {
                        Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer = cLayer;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.layer.tt"));
                    }

                    string mName = state.clientMap.Name;
                    if (ImGui.InputText(lang.Translate("ui.maps.name") + "###Name", ref mName, ushort.MaxValue))
                    {
                        state.clientMap.Name = mName;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = mName, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.Name };
                        pcmd.Send();
                    }

                    string mFolder = state.clientMap.Folder;
                    if (ImGui.InputText(lang.Translate("ui.maps.folder") + "###Folder", ref mFolder, ushort.MaxValue))
                    {
                        state.clientMap.Folder = mFolder;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = mFolder, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.Folder };
                        pcmd.Send();
                    }

                    if (ImGui.Button(lang.Translate("ui.maps.set_default") + "###Set Default"))
                    {
                        PacketSetDefaultMap psdm = new PacketSetDefaultMap() { IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID };
                        psdm.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.set_default.tt"));
                    }

                    ImGui.SameLine();
                    if (ImGui.Button(lang.Translate("ui.maps.move_all") + "###Move All"))
                    {
                        if (!Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftControl))
                        {
                            PacketChangeMap pcm = new PacketChangeMap() { Clients = Client.Instance.ClientInfos.Keys.ToArray(), NewMapID = state.clientMap.ID, IsServer = false, Session = Client.Instance.SessionID };
                            pcm.Send();
                        }
                        else
                        {
                            PacketChangeMap pcm = new PacketChangeMap() { Clients = Client.Instance.ClientInfos.Where(x => x.Value.IsLoggedOn).Select(x => x.Key).ToArray(), NewMapID = state.clientMap.ID, IsServer = false, Session = Client.Instance.SessionID };
                            pcm.Send();
                        }
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.move_all.tt"));
                    }

                    ImGui.SameLine();
                    if (ImGui.Button(lang.Translate("ui.maps.delete") + "###Delete"))
                    {
                        state.deleteMapPopup = true;
                        this._deletedMapId = state.clientMap.ID;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.delete.tt"));
                    }

                    bool m2d = state.clientMap.Is2D;
                    if (ImGui.Checkbox(lang.Translate("ui.maps.2d") + "###Enable 2D Mode", ref m2d))
                    {
                        state.clientMap.Is2D = m2d;
                        Client.Instance.Frontend.Renderer.MapRenderer.Switch2D(m2d);
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = m2d, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.Is2D };
                        Vector3 cPos = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Position;
                        if (state.clientMap.Is2D)
                        {
                            cPos = new Vector3(cPos.X, cPos.Y, Client.Instance.Frontend.Renderer.MapRenderer.ZoomOrtho);
                        }

                        Vector3 cDir = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Direction;
                        new PacketChangeMapData() { MapID = state.clientMap.ID, Data = cPos, Type = PacketChangeMapData.DataType.CameraPosition }.Send();
                        new PacketChangeMapData() { MapID = state.clientMap.ID, Data = cDir, Type = PacketChangeMapData.DataType.CameraDirection }.Send();
                        pcmd.Send(); // Change default camera position/direction when 2d is switched to current to fix 2d zoom levels
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.2d.tt"));
                    }

                    float m2dh = state.clientMap.Camera2DHeight;
                    if (ImGui.InputFloat(lang.Translate("ui.maps.2d_height") + "###2D Camera Height", ref m2dh))
                    {
                        state.clientMap.Camera2DHeight = m2dh;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = m2dh, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.Camera2DHeight };
                        pcmd.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.2d_height.tt"));
                    }

                    bool mEnableGrid = state.clientMap.GridEnabled;
                    if (ImGui.Checkbox(lang.Translate("ui.maps.enable_grid") + "###Enable Grid", ref mEnableGrid))
                    {
                        state.clientMap.GridEnabled = mEnableGrid;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = mEnableGrid, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.GridEnabled };
                        pcmd.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.enable_grid.tt"));
                    }

                    ImGui.SameLine();
                    bool mDrawGrid = state.clientMap.GridDrawn;
                    if (ImGui.Checkbox(lang.Translate("ui.maps.draw_world_grid") + "###Draw World Grid", ref mDrawGrid))
                    {
                        state.clientMap.GridDrawn = mDrawGrid;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = mDrawGrid, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.GridDrawn };
                        pcmd.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.draw_world_grid.tt"));
                    }

                    float mGridSize = state.clientMap.GridSize;
                    if (ImGui.InputFloat(lang.Translate("ui.maps.grid_size") + "###Grid Size", ref mGridSize))
                    {
                        state.clientMap.GridSize = mGridSize;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = mGridSize, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.GridSize };
                        pcmd.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.grid_size.tt"));
                    }

                    float mGridUnits = state.clientMap.GridUnit;
                    if (ImGui.InputFloat(lang.Translate("ui.maps.grid_units") + "###Grid Units", ref mGridUnits))
                    {
                        state.clientMap.GridUnit = mGridUnits;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = mGridUnits, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.GridUnits };
                        pcmd.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.grid_units.tt"));
                    }

                    bool mEnableFow = Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.HasFOW;
                    System.Numerics.Vector2 fowSize = Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.FOWWorldSize.SystemVector();
                    int fowSizeX = (int)fowSize.X;
                    if (ImGui.Checkbox(lang.Translate("ui.maps.enable_fow") + "###Enable FOW", ref mEnableFow))
                    {
                        if (!Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.HasFOW)
                        {
                            fowSize = new System.Numerics.Vector2(256, 256);
                        }

                        PacketEnableDisableFow pedf = new PacketEnableDisableFow() { Status = mEnableFow, Size = fowSize.GLVector() };
                        pedf.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.enable_fow.tt"));
                    }

                    if (!mEnableFow)
                    {
                        ImGui.BeginDisabled();
                    }

                    if (ImGui.InputInt(lang.Translate("ui.maps.fow_size") + "###FOW size", ref fowSizeX, 32, 32, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        if (fowSizeX is >= 32 and <= 4096)
                        {
                            PacketEnableDisableFow pedf = new PacketEnableDisableFow() { Status = mEnableFow, Size = new Vector2(fowSizeX) };
                            pedf.Send();
                        }
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.fow_size.tt"));
                    }

                    float mFowMod = Client.Instance.Settings.FOWAdmin;
                    if (ImGui.SliderFloat(lang.Translate("ui.maps.fow_opacity") + "###FOW Opacity", ref mFowMod, 0.0f, 1.0f))
                    {
                        Client.Instance.Settings.FOWAdmin = mFowMod;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.fow_opacity"));
                    }

                    if (!mEnableFow)
                    {
                        ImGui.EndDisabled();
                    }

                    System.Numerics.Vector4 mGridColor = (System.Numerics.Vector4)state.clientMap.GridColor;
                    System.Numerics.Vector4 mSkyColor = (System.Numerics.Vector4)state.clientMap.BackgroundColor;
                    System.Numerics.Vector4 mAmbientColor = (System.Numerics.Vector4)state.clientMap.AmbientColor;
                    System.Numerics.Vector4 mSunColor = (System.Numerics.Vector4)state.clientMap.SunColor;
                    ImGui.Text(lang.Translate("ui.maps.grid_color"));
                    ImGui.SameLine();
                    if (ImGui.ColorButton("##GridColor", mGridColor))
                    {
                        this._editedMapColor = mGridColor;
                        this._editedMapColorIndex = 0;
                        state.changeMapColorPopup = true;
                    }

                    ImGui.SameLine();
                    ImGui.Text(lang.Translate("ui.maps.sky_color"));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.sky_color.tt"));
                    }

                    ImGui.SameLine();
                    if (ImGui.ColorButton("##SkyColor", mSkyColor))
                    {
                        this._editedMapColor = mSkyColor;
                        this._editedMapColorIndex = 1;
                        state.changeMapColorPopup = true;
                    }

                    ImGui.Text(lang.Translate("ui.maps.ambient_color"));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.ambient_color.tt"));
                    }

                    ImGui.SameLine();
                    if (ImGui.ColorButton("##AmbientColor", mAmbientColor))
                    {
                        this._editedMapColor = mAmbientColor;
                        this._editedMapColorIndex = 2;
                        state.changeMapColorPopup = true;
                    }

                    ImGui.Text(lang.Translate("ui.maps.sun_color"));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.sun_color.tt"));
                    }

                    ImGui.SameLine();
                    if (ImGui.ColorButton("##SunColor", mSunColor))
                    {
                        this._editedMapColor = mSunColor;
                        this._editedMapColorIndex = 3;
                        state.changeMapColorPopup = true;
                    }

                    bool mEnableSun = state.clientMap.SunEnabled;
                    if (ImGui.Checkbox(lang.Translate("ui.maps.enable_sun") + "###Enable Sun", ref mEnableSun))
                    {
                        state.clientMap.SunEnabled = mEnableSun;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = mEnableSun, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.SunEnabled };
                        pcmd.Send();
                        if (!mEnableSun)
                        {
                            state.clientMap.EnableShadows = false;
                            pcmd = new PacketChangeMapData() { Data = false, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.EnableShadows };
                            pcmd.Send();
                        }
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.enable_sun.tt"));
                    }

                    if (!mEnableSun)
                    {
                        ImGui.BeginDisabled();
                    }

                    float yaw = state.clientMap.SunYaw;
                    float pitch = state.clientMap.SunPitch;
                    float intensity = state.clientMap.SunIntensity;
                    float aintensity = state.clientMap.AmbietIntensity;
                    if (ImGui.SliderAngle(lang.Translate("ui.maps.sun_yaw") + "###Sun Yaw", ref yaw, -180, 180))
                    {
                        state.clientMap.SunYaw = yaw;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = yaw, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.SunYaw };
                        pcmd.Send();
                    }

                    if (ImGui.SliderAngle(lang.Translate("ui.maps.sun_pitch") + "###Sun Pitch", ref pitch, -180, 180))
                    {
                        state.clientMap.SunPitch = pitch;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = pitch, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.SunPitch };
                        pcmd.Send();
                    }

                    if (ImGui.SliderFloat(lang.Translate("ui.maps.sun_intensity") + "###Sun Intensity", ref intensity, 0, 100))
                    {
                        state.clientMap.SunIntensity = intensity;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = intensity, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.SunIntensity };
                        pcmd.Send();
                    }

                    if (ImGui.SliderFloat(lang.Translate("ui.maps.ambient_intensity") + "###Ambient Intensity", ref aintensity, 0, 100))
                    {
                        state.clientMap.AmbietIntensity = aintensity;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = aintensity, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.AmbietIntensity };
                        pcmd.Send();
                    }

                    if (!mEnableSun)
                    {
                        ImGui.EndDisabled();
                    }

                    bool mSunShadows = state.clientMap.EnableShadows;
                    if (ImGui.Checkbox(lang.Translate("ui.maps.sun_shadows") + "###Enable Sun Shadows", ref mSunShadows))
                    {
                        state.clientMap.EnableShadows = mSunShadows;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = mSunShadows, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.EnableShadows };
                        pcmd.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.sun_shadows.tt"));
                    }

                    ImGui.SameLine();
                    bool mLightsShadows = state.clientMap.EnableDirectionalShadows;
                    if (ImGui.Checkbox(lang.Translate("ui.maps.point_shadows") + "###Enable Light Shadows", ref mLightsShadows))
                    {
                        state.clientMap.EnableDirectionalShadows = mLightsShadows;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = mLightsShadows, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.EnableDirectionalShadows };
                        pcmd.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.point_shadows.tt"));
                    }

                    bool mDrawings = state.clientMap.EnableDrawing;
                    if (ImGui.Checkbox(lang.Translate("ui.maps.drawing") + "###Enable Drawing", ref mDrawings))
                    {
                        state.clientMap.EnableDrawing = mDrawings;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = mDrawings, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.EnableDrawing };
                        pcmd.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.drawing.tt"));
                    }

                    #region Ambiance

                    bool mouseOverAmbiance = DrawMapAssetRecepticle(state.clientMap, state.clientMap.AmbientSoundID, () => this._draggedRef?.Type == AssetType.Sound, this.AssetMusicIcon);
                    if (mouseOverAmbiance && this._draggedRef != null && this._draggedRef.Type == AssetType.Sound)
                    {
                        state.mapAmbianceHovered = state.clientMap;
                    }

                    if (mouseOverAmbiance)
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.ambiance.tt"));
                    }

                    if (ImGui.Button(lang.Translate("ui.maps.clear_ambiance") + "###Clear Ambiance"))
                    {
                        if (Client.Instance.IsAdmin)
                        {
                            new PacketChangeMapData() { Data = Guid.Empty, MapID = state.clientMap.ID, Type = PacketChangeMapData.DataType.AmbientSoundID }.Send();
                        }
                    }

                    ImGui.TextUnformatted(lang.Translate("ui.maps.ambient_volume"));
                    float fAV = state.clientMap.AmbientSoundVolume;
                    if (ImGui.SliderFloat("##Ambiance Volume", ref fAV, 0, 1))
                    {
                        fAV = Math.Clamp(fAV, 0, 1);
                        state.clientMap.AmbientSoundVolume = fAV;
                        new PacketChangeMapData() { Data = fAV, MapID = state.clientMap.ID, Type = PacketChangeMapData.DataType.AmbientVolume }.Send();
                    }

                    ImGui.NewLine();
                    unsafe bool DrawMapAssetRecepticle(Map m, Guid aId, Func<bool> assetEval, GL.Texture iconTex = null)
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

                    #endregion

                    #region Darkvision
                    Map cMap = state.clientMap;
                    bool mDarkvision = cMap.EnableDarkvision;
                    if (ImGui.Checkbox(lang.Translate("ui.maps.darkvision.enabled") + "###EnableDarkVision", ref mDarkvision))
                    {
                        cMap.EnableDarkvision = mDarkvision;
                        new PacketChangeMapData() { MapID = cMap.ID, Data = mDarkvision, Type = PacketChangeMapData.DataType.DarkvisionEnabled }.Send();
                    }

                    if (ImGui.CollapsingHeader(lang.Translate("ui.maps.darkvision") + "###DarkvisionRules"))
                    {
                        if (this._objects.Length != (cMap.Objects.Count + 1))
                        {
                            Array.Resize(ref this._objects, cMap.Objects.Count + 1);
                        }

                        lock (cMap.Lock)
                        {
                            this._objects[0] = "(" + Guid.Empty.ToString() + ")";
                            for (int i = 0; i < cMap.Objects.Count; i++)
                            {
                                MapObject mo = cMap.Objects[i];
                                this._objects[i + 1] = mo.Name + " (" + mo.ID.ToString() + ")";
                            }

                            System.Numerics.Vector2 wC = ImGui.GetWindowSize();
                            int j = 0;
                            foreach (KeyValuePair<Guid, (Guid, float)> darkvisionData in cMap.DarkvisionData)
                            {
                                ImGui.BeginChild("dvEntry" + darkvisionData.Key, new System.Numerics.Vector2(wC.X - 32, 32), ImGuiChildFlags.Border, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollWithMouse);
                                Client.Instance.TryGetClientNamesArray(darkvisionData.Key, out int pIdx, out string[] cNames, out Guid[] cIds);
                                int oIdx = 0;
                                float v = darkvisionData.Value.Item2;
                                for (int i = 0; i < cMap.Objects.Count; ++i)
                                {
                                    MapObject o = cMap.Objects[i];
                                    if (o.ID.Equals(darkvisionData.Value.Item1))
                                    {
                                        oIdx = i + 1;
                                        break;
                                    }
                                }

                                ImGui.PushItemWidth(100);
                                if (ImGui.Combo("##DarkvisionId_" + j, ref pIdx, cNames, cNames.Length))
                                {
                                    new PacketDarkvisionData() { Deletion = false, MapID = state.clientMap.ID, ObjectID = darkvisionData.Value.Item1, PlayerID = cIds[pIdx], Value = darkvisionData.Value.Item2 }.Send();
                                }

                                ImGui.SameLine();
                                if (ImGui.Combo("##DarkvisionObject_" + j, ref oIdx, this._objects, this._objects.Length))
                                {
                                    string selected = this._objects[oIdx];
                                    Guid nId = Guid.Parse(selected.AsSpan(selected.LastIndexOf('(') + 1, 36));
                                    new PacketDarkvisionData() { Deletion = false, MapID = state.clientMap.ID, ObjectID = nId, PlayerID = darkvisionData.Key, Value = darkvisionData.Value.Item2 }.Send();
                                }

                                ImGui.SameLine();
                                if (ImGui.InputFloat("##DarkvisionValue_" + j, ref v, 0, 0, "%.3f", ImGuiInputTextFlags.EnterReturnsTrue))
                                {
                                    new PacketDarkvisionData() { Deletion = false, MapID = state.clientMap.ID, ObjectID = darkvisionData.Value.Item1, PlayerID = darkvisionData.Key, Value = v }.Send();
                                }

                                ImGui.SameLine();
                                ImGui.PopItemWidth();
                                if (ImGui.ImageButton("btnDeleteDarkvision_" + j, this.DeleteIcon, Vec12x12))
                                {
                                    new PacketDarkvisionData() { Deletion = true, MapID = cMap.ID, PlayerID = darkvisionData.Key }.Send();
                                }

                                ImGui.EndChild();
                                ++j;
                            }

                            if (ImGui.ImageButton("btnNewDarkvision", this.AddIcon, Vec12x12))
                            {
                                new PacketDarkvisionData() { MapID = state.clientMap.ID, ObjectID = Guid.Empty, PlayerID = Guid.Empty, Value = 0 }.Send();
                            }
                        }
                    }
                    #endregion


                    void RecursivelyRenderMaps(MPMapPointer dir, bool first, System.Numerics.Vector2 wC)
                    {
                        bool b1 = false;
                        if (!first)
                        {
                            ImGui.SetNextItemOpen(false, ImGuiCond.Once);
                            b1 = ImGui.TreeNode(dir.Name);
                        }

                        if (first || b1)
                        {
                            foreach (MPMapPointer d in dir.Elements.Where(x => !x.IsMap))
                            {
                                RecursivelyRenderMaps(d, false, wC);
                            }

                            foreach (MPMapPointer d in dir.Elements.Where(x => x.IsMap))
                            {
                                bool selected = state.clientMap.ID.Equals(d.MapID);
                                if (selected)
                                {
                                    ImGui.PushStyleColor(ImGuiCol.Border, (System.Numerics.Vector4)Color.RoyalBlue);
                                }

                                bool hadTT = false;
                                ImGui.BeginChild("mapNav_" + d.MapID.ToString(), new System.Numerics.Vector2(wC.X - 32, 32), ImGuiChildFlags.Border, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
                                if (selected)
                                {
                                    ImGui.PopStyleColor();
                                }

                                if (ImGui.ImageButton("moveToBtn_" + d.MapID.ToString(), this.MoveToIcon, Vec12x12))
                                {
                                    PacketChangeMap pcm = new PacketChangeMap() { Clients = new Guid[1] { Client.Instance.ID }, NewMapID = d.MapID, IsServer = false, Session = Client.Instance.SessionID };
                                    pcm.Send();
                                }

                                if (ImGui.IsItemHovered())
                                {
                                    hadTT = true;
                                    ImGui.SetTooltip(lang.Translate("ui.maps.nav.move_to"));
                                }

                                ImGui.SameLine();
                                if (ImGui.ImageButton("moveAllToBtn_" + d.MapID.ToString(), this.MoveAllToIcon, Vec12x12))
                                {
                                    if (!Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftControl))
                                    {
                                        PacketChangeMap pcm = new PacketChangeMap() { Clients = Client.Instance.ClientInfos.Keys.ToArray(), NewMapID = d.MapID, IsServer = false, Session = Client.Instance.SessionID };
                                        pcm.Send();
                                    }
                                    else
                                    {
                                        PacketChangeMap pcm = new PacketChangeMap() { Clients = Client.Instance.ClientInfos.Where(x => x.Value.IsLoggedOn).Select(x => x.Key).ToArray(), NewMapID = d.MapID, IsServer = false, Session = Client.Instance.SessionID };
                                        pcm.Send();
                                    }
                                }

                                if (ImGui.IsItemHovered())
                                {
                                    hadTT = true;
                                    ImGui.SetTooltip(lang.Translate("ui.maps.nav.move_all"));
                                }

                                ImGui.SameLine();
                                if (ImGui.ImageButton("deleteMapBtn_" + d.MapID.ToString(), this.DeleteIcon, Vec12x12))
                                {
                                    state.deleteMapPopup = true;
                                    this._deletedMapId = d.MapID;
                                }

                                if (ImGui.IsItemHovered())
                                {
                                    hadTT = true;
                                    ImGui.SetTooltip(lang.Translate("ui.maps.nav.delete"));
                                }

                                ImGui.SameLine();
                                string mName = d.Name + "(" + d.MapID.ToString() + ")";
                                if (d.MapID.Equals(Client.Instance.DefaultMPMapID))
                                {
                                    mName = "★ " + mName;
                                }

                                ImGui.TextUnformatted(mName);

                                ImGui.EndChild();
                                bool hover = ImGui.IsItemHovered();
                                if (hover && !hadTT)
                                {
                                    ImGui.BeginTooltip();
                                    if (d.MapID.Equals(Client.Instance.DefaultMPMapID))
                                    {
                                        ImGui.TextUnformatted(lang.Translate("ui.maps.default"));
                                    }

                                    ImGui.Text(lang.Translate("ui.maps.players_here"));
                                    foreach (KeyValuePair<Guid, ClientInfo> ckv in Client.Instance.ClientInfos)
                                    {
                                        if (!ckv.Key.Equals(Guid.Empty) && ckv.Value.MapID.Equals(d.MapID))
                                        {
                                            ImGui.TextUnformatted(" " + ckv.Value.Name);
                                        }
                                    }

                                    ImGui.EndTooltip();
                                }
                            }

                            if (b1)
                            {
                                ImGui.TreePop();
                            }
                        }
                    }

                    if (ImGui.CollapsingHeader(lang.Translate("ui.maps.maps") + "###Maps"))
                    {
                        System.Numerics.Vector2 wC = ImGui.GetWindowSize();
                        lock (Client.Instance.ServerMapPointersLock)
                        {
                            RecursivelyRenderMaps(Client.Instance.ClientMPMapsRoot, true, wC);
                        }

                        if (ImGui.ImageButton("btnNewMap", this.AddIcon, Vec12x12))
                        {
                            PacketCreateMap pcm = new PacketCreateMap();
                            pcm.Send();
                        }
                    }
                }

            }

            ImGui.End();
        }
    }
}
