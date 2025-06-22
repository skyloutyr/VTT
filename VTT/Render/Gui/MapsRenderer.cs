namespace VTT.Render.Gui
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using VTT.Asset;
    using VTT.Control;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public partial class GuiRenderer
    {
        private float _dayGradientKeyMem = 0;
        private Vector3 _dayGradientValueMem = Vector3.Zero;
        private float _nightGradientKeyMem = 0;
        private Vector3 _nightGradientValueMem = Vector3.Zero;


        private unsafe void RenderMaps(SimpleLanguage lang, GuiState state)
        {
            if (ImGui.Begin(lang.Translate("ui.maps") + "###Maps"))
            {
                Vector2 origPos = ImGui.GetCursorPos();
                if (state.clientMap != null)
                {
                    ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - 64);
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
                    ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - 64);
                    if (ImGui.Button(lang.Translate("ui.maps.cam_set") + "###Set Cam"))
                    {
                        Vector3 cPos = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Position;
                        if (state.clientMap.Is2D)
                        {
                            cPos = new Vector3(cPos.X, cPos.Y, Client.Instance.Frontend.Renderer.MapRenderer.ZoomOrtho);
                        }

                        new PacketChangeMapData() { MapID = state.clientMap.ID, Data = cPos, Type = PacketChangeMapData.DataType.CameraPosition }.Send();

                        if (!state.clientMap.Is2D)
                        {
                            Vector3 cDir = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Direction;
                            new PacketChangeMapData() { MapID = state.clientMap.ID, Data = cDir, Type = PacketChangeMapData.DataType.CameraDirection }.Send();
                        }
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.cam_set.tt"));
                    }

                    ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - 64);
                    if (ImGui.Button(lang.Translate("ui.maps.clear_marks") + "###Clear Marks"))
                    {
                        new PacketClearMarks().Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.clear_marks.tt"));
                    }

                    ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - 64);
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
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted(lang.Translate("ui.maps.layer.tt.intro"));
                        for (int j = 2; j >= -2; --j)
                        {
                            if (j == cLayer)
                            {
                                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextLink));
                            }

                            ImGui.TextUnformatted(lang.Translate($"ui.maps.layer.tt.{j}"));

                            if (j == cLayer)
                            {
                                ImGui.PopStyleColor();
                            }
                        }

                        ImGui.EndTooltip();
                    }

                    string mName = state.clientMap.Name;
                    if (ImGui.InputText(lang.Translate("ui.maps.name") + "###Name", ref mName, 255))
                    {
                        state.clientMap.Name = mName;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = mName, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.Name };
                        pcmd.Send();
                    }

                    string mFolder = state.clientMap.Folder;
                    if (ImGui.InputText(lang.Translate("ui.maps.folder") + "###Folder", ref mFolder, 4096))
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
                        if (!Client.Instance.Frontend.GameHandle.IsAnyControlDown())
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

                        if (state.clientMap.DaySkyboxAssetID.IsEmpty())
                        {
                            state.clientMap.DaySkyboxColors.SwitchType(MapSkyboxColors.ColorsPointerType.FullBlack);
                            new PacketChangeMapSkyboxColors() { MapID = state.clientMap.ID, Action = PacketChangeMapSkyboxColors.ActionType.SwitchKind, ColorsType = MapSkyboxColors.ColorsPointerType.FullBlack, IsNightGradientColors = false }.Send();
                        }

                        if (state.clientMap.NightSkyboxAssetID.IsEmpty())
                        {
                            state.clientMap.NightSkyboxColors.SwitchType(MapSkyboxColors.ColorsPointerType.FullBlack);
                            new PacketChangeMapSkyboxColors() { MapID = state.clientMap.ID, Action = PacketChangeMapSkyboxColors.ActionType.SwitchKind, ColorsType = MapSkyboxColors.ColorsPointerType.FullBlack, IsNightGradientColors = true }.Send();
                        }

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

                    ImGui.TextUnformatted(lang.Translate("ui.maps.grid_type"));
                    int mGridType = (int)state.clientMap.GridType;
                    string[] gridTypes = new string[] { lang.Translate("ui.maps.grid_type.square"), lang.Translate("ui.maps.grid_type.hhex"), lang.Translate("ui.maps.grid_type.vhex") };
                    if (ImGui.Combo("##MapGridType", ref mGridType, gridTypes, gridTypes.Length))
                    {
                        state.clientMap.GridType = (MapGridType)mGridType;
                        new PacketChangeMapData() { Type = PacketChangeMapData.DataType.GridType, MapID = state.clientMap.ID, Data = (uint)mGridType }.Send();
                    }

                    bool mEnableFow = Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.HasFOW;
                    Vector2 fowSize = Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.FOWWorldSize;
                    int fowSizeX = (int)fowSize.X;
                    if (ImGui.Checkbox(lang.Translate("ui.maps.enable_fow") + "###Enable FOW", ref mEnableFow))
                    {
                        if (!Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.HasFOW)
                        {
                            fowSize = new Vector2(256, 256);
                        }

                        PacketEnableDisableFow pedf = new PacketEnableDisableFow() { Status = mEnableFow, Size = fowSize };
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

                    ImGui.InputInt(lang.Translate("ui.maps.fow_size") + "###FOW size", ref fowSizeX, 32, 32);
                    if (ImGui.IsItemDeactivatedAfterEdit())
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

                    Vector4 mGridColor = (Vector4)state.clientMap.GridColor;
                    Vector4 mAmbientColor = (Vector4)state.clientMap.AmbientColor;
                    Vector4 mSunColor = (Vector4)state.clientMap.SunColor;
                    ImGui.Text(lang.Translate("ui.maps.grid_color"));
                    ImGui.SameLine();
                    if (ImGui.ColorButton("##GridColor", mGridColor))
                    {
                        this._editedMapColor = mGridColor;
                        this._editedMapColorIndex = 0;
                        state.changeMapColorPopup = true;
                    }

                    static void ImGradient(string id, Vector2 size, Gradient<Vector3> grad, Vector3 solidColor, bool readOnly, Action<PacketChangeMapSkyboxColors.ActionType, GradientPoint<Vector3>> callback, ref float selectedKey, ref Vector3 selectedKeyColor)
                    {
                        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
                        Vector2 contentAvail = ImGui.GetContentRegionAvail();
                        if (size.X == 0)
                        {
                            size.X = contentAvail.X;
                        }

                        Vector2 cHere = ImGui.GetCursorScreenPos() + new Vector2(0, 8);
                        ImGui.Dummy(size + new Vector2(0, 16));
                        GradientPoint<Vector3>? pointToDelete = null;
                        bool clickedBoxOrTriangle = false;
                        if (grad != null)
                        {
                            for (int i = 0; i < grad.InternalList.Count; i++)
                            {
                                GradientPoint<Vector3> curr = grad.InternalList[i];
                                GradientPoint<Vector3> next = grad.InternalList[(i + 1) % grad.InternalList.Count];
                                float fS = curr.Key / 24.0f * size.X;
                                float fN = (next.Key < curr.Key ? 1 : next.Key / 24.0f) * size.X;
                                uint clrL = curr.Color.Abgr();
                                uint clrR = next.Color.Abgr();
                                drawList.AddRectFilledMultiColor(
                                    new Vector2(cHere.X + fS, cHere.Y),
                                    new Vector2(cHere.X + fN, cHere.Y + size.Y),
                                    clrL, clrR, clrR, clrL
                                );

                                bool hoverTri = !readOnly && VTTMath.PointInTriangle(
                                    ImGui.GetMousePos(),
                                    new Vector2(cHere.X + fS, cHere.Y + size.Y),
                                    new Vector2(cHere.X + fS - 4, cHere.Y + size.Y + 8),
                                    new Vector2(cHere.X + fS + 4, cHere.Y + size.Y + 8)
                                );

                                if (hoverTri)
                                {
                                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                                    {
                                        selectedKey = curr.Key;
                                        selectedKeyColor = curr.Color;
                                        clickedBoxOrTriangle = true;
                                    }
                                }

                                if (hoverTri && i != 0 && i != grad.InternalList.Count - 1 && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                                {
                                    pointToDelete = curr;
                                    clickedBoxOrTriangle = true;
                                }

                                drawList.AddTriangleFilled(
                                    new Vector2(cHere.X + fS, cHere.Y + size.Y),
                                    new Vector2(cHere.X + fS - 4, cHere.Y + size.Y + 8),
                                    new Vector2(cHere.X + fS + 4, cHere.Y + size.Y + 8),
                                    hoverTri ? ImGui.GetColorU32(ImGuiCol.ButtonHovered) : ImGui.GetColorU32(ImGuiCol.Button)
                                );

                                drawList.AddTriangle(
                                    new Vector2(cHere.X + fS, cHere.Y + size.Y),
                                    new Vector2(cHere.X + fS - 4, cHere.Y + size.Y + 8),
                                    new Vector2(cHere.X + fS + 4, cHere.Y + size.Y + 8),
                                    ImGui.GetColorU32(curr.Key == selectedKey ? ImGuiCol.ButtonActive : ImGuiCol.Border)
                                );

                                drawList.AddRectFilled(
                                    new Vector2(cHere.X + fS - 4, cHere.Y - 8),
                                    new Vector2(cHere.X + fS + 4, cHere.Y - 0),
                                    clrL
                                );

                                bool hoverQuad = !readOnly && ImGui.IsMouseHoveringRect(new Vector2(cHere.X + fS - 4, cHere.Y - 8), new Vector2(cHere.X + fS + 4, cHere.Y - 0));
                                drawList.AddRect(
                                    new Vector2(cHere.X + fS - 4, cHere.Y - 8),
                                    new Vector2(cHere.X + fS + 4, cHere.Y - 0),
                                    hoverQuad ? ImGui.GetColorU32(ImGuiCol.ButtonHovered) : ImGui.GetColorU32(ImGuiCol.Border)
                                );

                                if (hoverQuad && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                                {
                                    callback(PacketChangeMapSkyboxColors.ActionType.SetSolidColor, curr);
                                    clickedBoxOrTriangle = true;
                                }
                            }
                        }
                        else
                        {
                            drawList.AddRectFilled(cHere, cHere + size, solidColor.Abgr());
                        }

                        drawList.AddRect(
                            cHere,
                            cHere + size,
                            ImGui.GetColorU32(ImGuiCol.Border)
                        );

                        if (!readOnly)
                        {
                            if (!clickedBoxOrTriangle && ImGui.IsMouseHoveringRect(cHere, cHere + size) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                            {
                                float a = Math.Clamp(((ImGui.GetMousePos() - cHere) / size).X * 24, 0, 24);
                                Vector3 closest = grad.Interpolate(a, GradientInterpolators.LerpVec3);
                                if (!grad.ContainsKey(a))
                                {
                                    grad.Add(a, closest);
                                    selectedKey = a;
                                    selectedKeyColor = closest;
                                    callback(PacketChangeMapSkyboxColors.ActionType.AddGradientPoint, new GradientPoint<Vector3>(a, closest));
                                }
                            }

                            if (pointToDelete.HasValue)
                            {
                                grad.Remove(pointToDelete.Value.Key);
                                callback(PacketChangeMapSkyboxColors.ActionType.RemoveGradientPoint, pointToDelete.Value);
                            }

                            if (!grad.ContainsKey(selectedKey))
                            {
                                var kv = grad.First();
                                selectedKey = kv.Key;
                                selectedKeyColor = kv.Value;
                            }

                            grad.TryGetValue(selectedKey, out Vector3 clr);
                            if (clr != selectedKeyColor)
                            {
                                selectedKeyColor = clr;
                            }

                            float key = selectedKey;
                            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                            if (ImGui.SliderFloat($"{id}_keySelector", ref key, 0, 24))
                            {
                                if (selectedKey is > 0 and < 24)
                                {
                                    key = Math.Clamp(key, 0.001f, 23.999f);
                                    grad.Remove(selectedKey);
                                    grad.Add(key, clr);
                                    float prev = selectedKey;
                                    selectedKey = key;
                                    selectedKeyColor = clr;
                                    callback(PacketChangeMapSkyboxColors.ActionType.MoveGradientPoint, new GradientPoint<Vector3>(prev, clr));
                                }
                            }

                            ImGui.PopItemWidth();
                            if (ImGui.ColorPicker3($"{id}_valueSelector", ref selectedKeyColor, ImGuiColorEditFlags.NoSidePreview | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.PickerHueBar))
                            {
                                grad.Remove(selectedKey);
                                grad.Add(selectedKey, selectedKeyColor);
                                callback(PacketChangeMapSkyboxColors.ActionType.ChangeGradientPointColor, new GradientPoint<Vector3>(selectedKey, selectedKeyColor));
                            }
                        }
                    }

                    static void ImGradientReadonly(Vector2 size, Gradient<Vector3> grad, Vector3 solidColor)
                    {
                        float f = -1;
                        Vector3 v = Vector3.Zero;
                        ImGradient(string.Empty, size, grad, solidColor, true, static (x, y) => { }, ref f, ref v);
                    }

                    void SkyboxSettings(bool isDay)
                    {
                        string dayNight = isDay ? "Day" : "Night";
                        string dayNightLower = isDay ? "day" : "night";
                        MapSkyboxColors colors = isDay ? state.clientMap.DaySkyboxColors : state.clientMap.NightSkyboxColors;
                        ImGui.TextUnformatted(lang.Translate($"ui.maps.sky_settings.skybox_{dayNightLower}.asset"));
                        if (ImGui.BeginChild(lang.Translate($"ui.maps.sky_settings.skybox_{dayNightLower}") + $"###{dayNight}SkyboxSettings", new Vector2(ImGui.GetContentRegionAvail().X, 0), ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.Borders))
                        {
                            bool mouseOverSkyboxAssetRecepticle = ImGuiHelper.ImAssetRecepticle(lang, isDay ? state.clientMap.DaySkyboxAssetID : state.clientMap.NightSkyboxAssetID, this.AssetImageIcon, new Vector2(0, 24), static x => x.Type == AssetType.Texture, out bool assetRecepticleHovered);
                            if (mouseOverSkyboxAssetRecepticle && this._draggedRef != null && this._draggedRef.Type == AssetType.Texture)
                            {
                                if (isDay)
                                {
                                    state.mapDaySkyboxAssetHovered = state.clientMap;
                                }
                                else
                                {
                                    state.mapNightSkyboxAssetHovered = state.clientMap;
                                }
                            }

                            if (assetRecepticleHovered)
                            {
                                ImGui.BeginTooltip();
                                ImGui.TextUnformatted(lang.Translate($"ui.maps.sky_settings.skybox_{dayNightLower}.asset.tt"));
                                Vector2 cHere = ImGui.GetCursorScreenPos();
                                ImGui.Image(this.SkyboxUIExample, new Vector2(64 * 4, 64 * 3));
                                string t = lang.Translate("generic.top");
                                ImDrawListPtr drawList = ImGui.GetWindowDrawList();
                                ImGuiHelper.AddTextWithSingleDropShadow(drawList, cHere + new Vector2((64 * 1) + 32, (64 * 0) + 32) - (ImGui.CalcTextSize(t) * 0.5f), ImGui.GetColorU32(ImGuiCol.Text), t);
                                t = lang.Translate("generic.left");
                                ImGuiHelper.AddTextWithSingleDropShadow(drawList, cHere + new Vector2((64 * 0) + 32, (64 * 1) + 32) - (ImGui.CalcTextSize(t) * 0.5f), ImGui.GetColorU32(ImGuiCol.Text), t);
                                t = lang.Translate("generic.front");
                                ImGuiHelper.AddTextWithSingleDropShadow(drawList, cHere + new Vector2((64 * 1) + 32, (64 * 1) + 32) - (ImGui.CalcTextSize(t) * 0.5f), ImGui.GetColorU32(ImGuiCol.Text), t);
                                t = lang.Translate("generic.right");
                                ImGuiHelper.AddTextWithSingleDropShadow(drawList, cHere + new Vector2((64 * 2) + 32, (64 * 1) + 32) - (ImGui.CalcTextSize(t) * 0.5f), ImGui.GetColorU32(ImGuiCol.Text), t);
                                t = lang.Translate("generic.back");
                                ImGuiHelper.AddTextWithSingleDropShadow(drawList, cHere + new Vector2((64 * 3) + 32, (64 * 1) + 32) - (ImGui.CalcTextSize(t) * 0.5f), ImGui.GetColorU32(ImGuiCol.Text), t);
                                t = lang.Translate("generic.bottom");
                                ImGuiHelper.AddTextWithSingleDropShadow(drawList, cHere + new Vector2((64 * 1) + 32, (64 * 2) + 32) - (ImGui.CalcTextSize(t) * 0.5f), ImGui.GetColorU32(ImGuiCol.Text), t);
                                ImGui.EndTooltip();
                            }
                        }

                        if (ImGui.Button(lang.Translate($"ui.maps.sky_settings.skybox_any.asset.clear") + $"###Clear{dayNight}SkyboxAsset"))
                        {
                            new PacketSetMapSkyboxAsset() { AssetID = Guid.Empty, MapID = state.clientMap.ID, IsNightSkybox = !isDay }.Send();
                            colors.SwitchType(MapSkyboxColors.ColorsPointerType.DefaultSky);
                            new PacketChangeMapSkyboxColors() { MapID = state.clientMap.ID, Action = PacketChangeMapSkyboxColors.ActionType.SwitchKind, ColorsType = MapSkyboxColors.ColorsPointerType.DefaultSky, IsNightGradientColors = !isDay }.Send();
                        }

                        ImGui.NewLine();
                        string[] sbColorPointerTypes = new string[] { lang.Translate("ui.maps.sky_settings.skybox.colors.default"), lang.Translate("ui.maps.sky_settings.skybox.colors.black"), lang.Translate("ui.maps.sky_settings.skybox.colors.white"), lang.Translate("ui.maps.sky_settings.skybox.colors.solid"), lang.Translate("ui.maps.sky_settings.skybox.colors.custom_gradient"), lang.Translate("ui.maps.sky_settings.skybox.colors.asset") };
                        int sbColorPointerIndex = (int)colors.OwnType;
                        ImGui.TextUnformatted(lang.Translate("ui.maps.sky_settings.skybox.colors_label"));
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.maps.sky_settings.skybox.colors.tt"));
                        }

                        if (ImGui.Combo($"##{dayNight}SkyboxColorsKind", ref sbColorPointerIndex, sbColorPointerTypes, sbColorPointerTypes.Length))
                        {
                            colors.SwitchType((MapSkyboxColors.ColorsPointerType)sbColorPointerIndex);
                            new PacketChangeMapSkyboxColors() { MapID = state.clientMap.ID, Action = PacketChangeMapSkyboxColors.ActionType.SwitchKind, ColorsType = (MapSkyboxColors.ColorsPointerType)sbColorPointerIndex, IsNightGradientColors = !isDay }.Send();
                        }

                        switch (colors.OwnType)
                        {
                            case MapSkyboxColors.ColorsPointerType.DefaultSky:
                            {
                                ImGradientReadonly(new Vector2(0, 32), Client.Instance.Frontend.Renderer.SkyRenderer.SkyGradient, default);
                                break;
                            }

                            case MapSkyboxColors.ColorsPointerType.FullBlack:
                            {
                                ImGradientReadonly(new Vector2(0, 32), null, Vector3.Zero);
                                break;
                            }
                            case MapSkyboxColors.ColorsPointerType.FullWhite:
                            {
                                ImGradientReadonly(new Vector2(0, 32), null, Vector3.One);
                                break;
                            }

                            case MapSkyboxColors.ColorsPointerType.SolidColor:
                            {
                                if (ImGui.ColorButton("##DaySkyboxColorSolidColor", new Vector4(colors.SolidColor, 1.0f), ImGuiColorEditFlags.NoAlpha, new Vector2(ImGui.GetContentRegionAvail().X, 32)))
                                {
                                    this._editedMapSkyboxColorIsDay = isDay;
                                    this._editedMapSkyboxColorGradientKey = float.NaN;
                                    this._editedMapSkyboxColorGradientValue = colors.SolidColor;
                                    state.changeMapSkyboxColorPopup = true;
                                }

                                break;
                            }

                            case MapSkyboxColors.ColorsPointerType.CustomGradient:
                            {
                                ImGradient($"##GradientSkyColor{dayNight}", new Vector2(0, 32), colors.ColorGradient, default, false, (action, x) => 
                                {
                                    switch (action)
                                    {
                                        case PacketChangeMapSkyboxColors.ActionType.SetSolidColor:
                                        {
                                            this._editedMapSkyboxColorIsDay = isDay;
                                            this._editedMapSkyboxColorGradientKey = x.Key;
                                            this._editedMapSkyboxColorGradientValue = x.Color;
                                            state.changeMapSkyboxColorPopup = true;
                                            break;
                                        }

                                        case PacketChangeMapSkyboxColors.ActionType.AddGradientPoint:
                                        {
                                            new PacketChangeMapSkyboxColors() { MapID = state.clientMap.ID, Action = PacketChangeMapSkyboxColors.ActionType.AddGradientPoint, GradientPointKey = x.Key, GradientPointColor = x.Color, IsNightGradientColors = !isDay }.Send();
                                            break;
                                        }

                                        case PacketChangeMapSkyboxColors.ActionType.RemoveGradientPoint:
                                        {
                                            new PacketChangeMapSkyboxColors() { MapID = state.clientMap.ID, Action = PacketChangeMapSkyboxColors.ActionType.RemoveGradientPoint, GradientPointKey = x.Key, IsNightGradientColors = !isDay }.Send();
                                            break;
                                        }

                                        case PacketChangeMapSkyboxColors.ActionType.MoveGradientPoint:
                                        {
                                            new PacketChangeMapSkyboxColors() { MapID = state.clientMap.ID, Action = PacketChangeMapSkyboxColors.ActionType.MoveGradientPoint, GradientPointKey = x.Key, GradientPointDesination = isDay ? this._dayGradientKeyMem : this._nightGradientKeyMem, GradientPointColor = x.Color, IsNightGradientColors = !isDay }.Send();
                                            break;
                                        }

                                        case PacketChangeMapSkyboxColors.ActionType.ChangeGradientPointColor:
                                        {
                                            new PacketChangeMapSkyboxColors() { MapID = state.clientMap.ID, Action = PacketChangeMapSkyboxColors.ActionType.ChangeGradientPointColor, GradientPointKey = x.Key, GradientPointColor = x.Color, IsNightGradientColors = !isDay }.Send();
                                            break;
                                        }
                                    }
                                }, ref (isDay ? ref this._dayGradientKeyMem : ref this._nightGradientKeyMem), ref (isDay ? ref this._dayGradientValueMem : ref this._nightGradientValueMem));

                                break;
                            }

                            case MapSkyboxColors.ColorsPointerType.CustomImage:
                            {
                                bool mOverCustomImageAssetRecepticle = ImGuiHelper.ImAssetRecepticle(lang, colors.GradientAssetID, this.AssetImageIcon, new Vector2(0, 24), static x => x.Type == AssetType.Texture, out _);
                                if (mOverCustomImageAssetRecepticle && this._draggedRef != null && this._draggedRef.Type == AssetType.Texture)
                                {
                                    if (isDay)
                                    {
                                        state.mapDaySkyboxColorsAssetHovered = state.clientMap;
                                    }
                                    else
                                    {
                                        state.mapNightSkyboxColorsAssetHovered = state.clientMap;
                                    }
                                }

                                if (ImGui.Button(lang.Translate($"ui.maps.sky_settings.skybox_any.asset.clear") + $"###Clear{dayNight}SkyboxGradientColorsAsset"))
                                {
                                    new PacketChangeMapSkyboxColors() { MapID = state.clientMap.ID, Action = PacketChangeMapSkyboxColors.ActionType.SetImageAssetID, AssetID = Guid.Empty, IsNightGradientColors = !isDay }.Send();
                                }

                                break;
                            }
                        }

                        ImGui.EndChild();
                    }

                    if (ImGui.TreeNode(lang.Translate("ui.maps.sky_settings") + "##SkySettings"))
                    {
                        SkyboxSettings(true);
                        ImGui.NewLine();
                        SkyboxSettings(false);
                        ImGui.TreePop();
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
                    float aintensity = state.clientMap.AmbientIntensity;
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
                        state.clientMap.AmbientIntensity = aintensity;
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

                    bool m2DShadows = state.clientMap.Has2DShadows;
                    if (ImGui.Checkbox(lang.Translate("ui.maps.shadows_2d") + "###Enable 2D Shadows", ref m2DShadows))
                    {
                        state.clientMap.Has2DShadows = m2DShadows;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = m2DShadows, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.Enable2DShadows };
                        pcmd.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.shadows_2d.tt"));
                    }

                    bool m2DShadowsStrict = state.clientMap.Shadows2DObjectVisionStrict;
                    if (ImGui.Checkbox(lang.Translate("ui.maps.shadows_2d_strict") + "###Strict 2D Shadows", ref m2DShadowsStrict))
                    {
                        state.clientMap.Shadows2DObjectVisionStrict = m2DShadowsStrict;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = m2DShadowsStrict, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.Shadows2DObjectVisionStrict };
                        pcmd.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.shadows_2d_strict.tt"));
                    }

                    float mShadow2DMod = Client.Instance.Settings.Shadows2DAdmin;
                    if (ImGui.SliderFloat(lang.Translate("ui.maps.shadows_2d_opacity") + "###2D Shadow Opacity", ref mShadow2DMod, 0.0f, 1.0f))
                    {
                        Client.Instance.Settings.Shadows2DAdmin = mShadow2DMod;
                        Client.Instance.Settings.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.maps.shadows_2d_opacity.tt"));
                    }

                    #region Ambiance

                    bool mouseOverAmbiance = ImGuiHelper.ImAssetRecepticle(lang, state.clientMap.AmbientSoundID, this.AssetSoundIcon, new Vector2(0, 24), static x => x.Type == AssetType.Sound, out bool ambianceAssetHovered);
                    if (mouseOverAmbiance && this._draggedRef != null && this._draggedRef.Type == AssetType.Sound)
                    {
                        state.mapAmbianceHovered = state.clientMap;
                    }

                    if (ambianceAssetHovered)
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
                        int oC = cMap.ObjectCountUnsafe;
                        if (this._darkvisionObjectNames.Length != (oC + 1))
                        {
                            Array.Resize(ref this._darkvisionObjectNames, oC + 1);
                            Array.Resize(ref this._darkvisionObjectIds, oC + 1);
                        }

                        this._darkvisionObjectNames[0] = "(" + Guid.Empty.ToString() + ")";
                        this._darkvisionObjectIds[0] = Guid.Empty;
                        cMap.MapObjects(null, (i, mo) =>
                        {
                            if (i + 1 < this._darkvisionObjectNames.Length)
                            {
                                this._darkvisionObjectNames[i + 1] = mo.Name + " (" + mo.ID.ToString() + ")";
                                this._darkvisionObjectIds[i + 1] = mo.ID;
                            }
                        });

                        Vector2 wC = ImGui.GetWindowSize();
                        int j = 0;
                        foreach (KeyValuePair<Guid, (Guid, float)> darkvisionData in cMap.DarkvisionData)
                        {
                            if (ImGui.BeginChild("dvEntry" + darkvisionData.Key, new Vector2(wC.X - 32, 32), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollWithMouse))
                            {
                                Client.Instance.TryGetClientNamesArray(darkvisionData.Key, out int pIdx, out string[] cNames, out Guid[] cIds);
                                int oIdx = 0;
                                float v = darkvisionData.Value.Item2;
                                for (int i = 0; i < this._darkvisionObjectIds.Length; ++i)
                                {
                                    Guid id = this._darkvisionObjectIds[i];
                                    if (id.Equals(darkvisionData.Value.Item1))
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
                                if (ImGui.Combo("##DarkvisionObject_" + j, ref oIdx, this._darkvisionObjectNames, this._darkvisionObjectNames.Length))
                                {
                                    string selected = this._darkvisionObjectNames[oIdx];
                                    Guid nId = Guid.Parse(selected.AsSpan(selected.LastIndexOf('(') + 1, 36));
                                    new PacketDarkvisionData() { Deletion = false, MapID = state.clientMap.ID, ObjectID = nId, PlayerID = darkvisionData.Key, Value = darkvisionData.Value.Item2 }.Send();
                                }

                                ImGui.SameLine();
                                ImGui.InputFloat("##DarkvisionValue_" + j, ref v, 0, 0, "%.3f");
                                if (ImGui.IsItemDeactivatedAfterEdit())
                                {
                                    new PacketDarkvisionData() { Deletion = false, MapID = state.clientMap.ID, ObjectID = darkvisionData.Value.Item1, PlayerID = darkvisionData.Key, Value = v }.Send();
                                }

                                ImGui.SameLine();
                                ImGui.PopItemWidth();
                                if (this.DeleteIcon.ImImageButton("btnDeleteDarkvision_" + j, Vec12x12))
                                {
                                    new PacketDarkvisionData() { Deletion = true, MapID = cMap.ID, PlayerID = darkvisionData.Key }.Send();
                                }
                            }

                            ImGui.EndChild();
                            ++j;
                        }

                        if (this.AddIcon.ImImageButton("btnNewDarkvision", Vec12x12))
                        {
                            new PacketDarkvisionData() { MapID = state.clientMap.ID, ObjectID = Guid.Empty, PlayerID = Guid.Empty, Value = 0 }.Send();
                        }
                    }
                    #endregion


                    void RecursivelyRenderMaps(MPMapPointer dir, bool first, Vector2 wC)
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
                                    ImGui.PushStyleColor(ImGuiCol.Border, (Vector4)Color.RoyalBlue);
                                }

                                bool hadTT = false;
                                if (ImGui.BeginChild("mapNav_" + d.MapID.ToString(), new Vector2(wC.X - 32, 32), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                                {
                                    if (selected)
                                    {
                                        ImGui.PopStyleColor();
                                    }

                                    if (this.MoveToIcon.ImImageButton("moveToBtn_" + d.MapID.ToString(), Vec12x12))
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
                                    if (this.MoveAllToIcon.ImImageButton("moveAllToBtn_" + d.MapID.ToString(), Vec12x12))
                                    {
                                        if (!Client.Instance.Frontend.GameHandle.IsAnyControlDown())
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
                                    if (this.CopyIcon.ImImageButton("duplicateMapBtn_" + d.MapID.ToString(), Vec12x12))
                                    {
                                        new PacketDuplicateMap() { MapID = d.MapID }.Send();
                                    }

                                    if (ImGui.IsItemHovered())
                                    {
                                        hadTT = true;
                                        ImGui.SetTooltip(lang.Translate("ui.maps.nav.duplicate"));
                                    }

                                    ImGui.SameLine();

                                    if (this.DeleteIcon.ImImageButton("deleteMapBtn_" + d.MapID.ToString(), Vec12x12))
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
                                }
                                else
                                {
                                    if (selected)
                                    {
                                        ImGui.PopStyleColor();
                                    }
                                }

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
                        Vector2 wC = ImGui.GetWindowSize();
                        lock (Client.Instance.ServerMapPointersLock)
                        {
                            RecursivelyRenderMaps(Client.Instance.ClientMPMapsRoot, true, wC);
                        }

                        if (this.AddIcon.ImImageButton("btnNewMap", Vec12x12))
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
