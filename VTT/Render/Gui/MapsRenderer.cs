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
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public partial class GuiRenderer
    {
        private float _dayGradientKeyMem = 0;
        private Vector4 _dayGradientValueMem = Vector4.Zero;
        private float _nightGradientKeyMem = 0;
        private Vector4 _nightGradientValueMem = Vector4.Zero;
        private float _celestialBodyPreviewTime = 12f;
        private bool _celestialBodyGradientEditedIsLightGradient = false;
        private float _celestialBodyEditedGradientKey = 0f;
        private Vector4 _celestialBodyEditedGradientValue = Vector4.Zero;
        private CelestialBody _editedGradientCelestialBodyRef;

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
                            new PacketChangeMapColorsGradient() { MapID = state.clientMap.ID, Action = PacketChangeMapColorsGradient.ActionType.SwitchKind, ColorsType = MapSkyboxColors.ColorsPointerType.FullBlack, Location = PacketChangeMapColorsGradient.GradientLocation.MapDayGradient }.Send();
                        }

                        if (state.clientMap.NightSkyboxAssetID.IsEmpty())
                        {
                            state.clientMap.NightSkyboxColors.SwitchType(MapSkyboxColors.ColorsPointerType.FullBlack);
                            new PacketChangeMapColorsGradient() { MapID = state.clientMap.ID, Action = PacketChangeMapColorsGradient.ActionType.SwitchKind, ColorsType = MapSkyboxColors.ColorsPointerType.FullBlack, Location = PacketChangeMapColorsGradient.GradientLocation.MapNightGradient }.Send();
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
                            new PacketChangeMapColorsGradient() { MapID = state.clientMap.ID, Action = PacketChangeMapColorsGradient.ActionType.SwitchKind, ColorsType = MapSkyboxColors.ColorsPointerType.DefaultSky, Location = isDay ? PacketChangeMapColorsGradient.GradientLocation.MapDayGradient : PacketChangeMapColorsGradient.GradientLocation.MapNightGradient }.Send();
                        }

                        ImGui.NewLine();
                        ImGuiHelper.ImColorGradientsResult result = ImGuiHelper.ImColorGradients(lang, colors, $"sky_colors_{isDay}", string.Empty, this.AssetImageIcon, out bool sbclrsAssetRecepticleHovered);
                        if (result.editPerformed)
                        {
                            switch (result.change)
                            {
                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.ColorsPointerType:
                                {
                                    new PacketChangeMapColorsGradient() { MapID = state.clientMap.ID, Action = PacketChangeMapColorsGradient.ActionType.SwitchKind, ColorsType = result.newPointerType, Location = isDay ? PacketChangeMapColorsGradient.GradientLocation.MapDayGradient : PacketChangeMapColorsGradient.GradientLocation.MapNightGradient }.Send();
                                    break;
                                }

                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.WantOpenSolidColorChangePopup:
                                {
                                    this._editedMapSkyboxColorIsDay = isDay;
                                    this._editedMapSkyboxColorGradientKey = float.NaN;
                                    this._editedMapSkyboxColorGradientValue = result.gradientValue;
                                    state.changeMapSkyboxColorPopup = true;
                                    break;
                                }

                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.WantOpenGradientColorChangePopup:
                                {
                                    this._editedMapSkyboxColorIsDay = isDay;
                                    this._editedMapSkyboxColorGradientKey = result.gradientKey;
                                    this._editedMapSkyboxColorGradientValue = result.gradientValue;
                                    state.changeMapSkyboxColorPopup = true;
                                    break;
                                }

                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.AddGradientPoint:
                                {
                                    new PacketChangeMapColorsGradient() { MapID = state.clientMap.ID, Action = PacketChangeMapColorsGradient.ActionType.AddGradientPoint, GradientPointKey = result.gradientKey, GradientPointColor = result.gradientValue, Location = isDay ? PacketChangeMapColorsGradient.GradientLocation.MapDayGradient : PacketChangeMapColorsGradient.GradientLocation.MapNightGradient }.Send();
                                    break;
                                }

                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.RemoveGradientPoint:
                                {
                                    new PacketChangeMapColorsGradient() { MapID = state.clientMap.ID, Action = PacketChangeMapColorsGradient.ActionType.RemoveGradientPoint, GradientPointKey = result.gradientKey, Location = isDay ? PacketChangeMapColorsGradient.GradientLocation.MapDayGradient : PacketChangeMapColorsGradient.GradientLocation.MapNightGradient }.Send();
                                    break;
                                }

                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.MoveGradientPoint:
                                {
                                    new PacketChangeMapColorsGradient() { MapID = state.clientMap.ID, Action = PacketChangeMapColorsGradient.ActionType.MoveGradientPoint, GradientPointKey = result.oldGradientKey, GradientPointDesination = result.gradientKey, GradientPointColor = result.gradientValue, Location = isDay ? PacketChangeMapColorsGradient.GradientLocation.MapDayGradient : PacketChangeMapColorsGradient.GradientLocation.MapNightGradient }.Send();
                                    break;
                                }

                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.ChangeGradientPointColor:
                                {
                                    new PacketChangeMapColorsGradient() { MapID = state.clientMap.ID, Action = PacketChangeMapColorsGradient.ActionType.ChangeGradientPointColor, GradientPointKey = result.gradientKey, GradientPointColor = result.gradientValue, Location = isDay ? PacketChangeMapColorsGradient.GradientLocation.MapDayGradient : PacketChangeMapColorsGradient.GradientLocation.MapNightGradient }.Send();
                                    break;
                                }

                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.ClearCustomImage:
                                {
                                    new PacketChangeMapColorsGradient() { MapID = state.clientMap.ID, Action = PacketChangeMapColorsGradient.ActionType.SetImageAssetID, AssetID = Guid.Empty, Location = isDay ? PacketChangeMapColorsGradient.GradientLocation.MapDayGradient : PacketChangeMapColorsGradient.GradientLocation.MapNightGradient }.Send();
                                    break;
                                }
                            }
                        }

                        if (sbclrsAssetRecepticleHovered)
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

                        ImGui.EndChild();
                    }

                    if (ImGui.TreeNode(lang.Translate("ui.maps.sky_settings") + "##SkySettings"))
                    {
                        SkyboxSettings(true);
                        ImGui.NewLine();
                        SkyboxSettings(false);
                        ImGui.TreePop();
                    }

                    void CelestialBodyColorGradient(CelestialBody cb, bool isLightGrad)
                    {
                        MapSkyboxColors colors = isLightGrad ? cb.LightColor : cb.OwnColor;
                        PacketChangeMapColorsGradient.GradientLocation location = isLightGrad ? PacketChangeMapColorsGradient.GradientLocation.MapCelestialBodyGradientLight : PacketChangeMapColorsGradient.GradientLocation.MapCelestialBodyGradientOwn;
                        ImGuiHelper.ImColorGradientsResult result = ImGuiHelper.ImColorGradients(lang, colors, $"celestial_body_colors.{cb.OwnID}.{isLightGrad}", $"ui.maps.celestial_body.color.{(isLightGrad ? "light" : "own")}", this.AssetImageIcon, out bool sbclrsAssetRecepticleHovered);
                        if (result.editPerformed)
                        {
                            switch (result.change)
                            {
                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.ColorsPointerType:
                                {
                                    new PacketChangeMapColorsGradient() { MapID = state.clientMap.ID, Action = PacketChangeMapColorsGradient.ActionType.SwitchKind, ColorsType = result.newPointerType, Location = location, CelestialBodyID = cb.OwnID }.Send();
                                    break;
                                }

                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.WantOpenSolidColorChangePopup:
                                {
                                    this._celestialBodyGradientEditedIsLightGradient = isLightGrad;
                                    this._celestialBodyEditedGradientKey = float.NaN;
                                    this._celestialBodyEditedGradientValue = result.gradientValue;
                                    this._editedGradientCelestialBodyRef = cb;
                                    state.celestialBodyChangeColorPopup = true;
                                    break;
                                }

                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.WantOpenGradientColorChangePopup:
                                {
                                    this._celestialBodyGradientEditedIsLightGradient = isLightGrad;
                                    this._celestialBodyEditedGradientKey = result.gradientKey;
                                    this._celestialBodyEditedGradientValue = result.gradientValue;
                                    this._editedGradientCelestialBodyRef = cb;
                                    state.celestialBodyChangeColorPopup = true;
                                    break;
                                }

                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.AddGradientPoint:
                                {
                                    new PacketChangeMapColorsGradient() { MapID = state.clientMap.ID, Action = PacketChangeMapColorsGradient.ActionType.AddGradientPoint, GradientPointKey = result.gradientKey, GradientPointColor = result.gradientValue, Location = location, CelestialBodyID = cb.OwnID }.Send();
                                    break;
                                }

                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.RemoveGradientPoint:
                                {
                                    new PacketChangeMapColorsGradient() { MapID = state.clientMap.ID, Action = PacketChangeMapColorsGradient.ActionType.RemoveGradientPoint, GradientPointKey = result.gradientKey, Location = location, CelestialBodyID = cb.OwnID }.Send();
                                    break;
                                }

                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.MoveGradientPoint:
                                {
                                    new PacketChangeMapColorsGradient() { MapID = state.clientMap.ID, Action = PacketChangeMapColorsGradient.ActionType.MoveGradientPoint, GradientPointKey = result.oldGradientKey, GradientPointDesination = result.gradientKey, GradientPointColor = result.gradientValue, Location = location, CelestialBodyID = cb.OwnID }.Send();
                                    break;
                                }

                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.ChangeGradientPointColor:
                                {
                                    new PacketChangeMapColorsGradient() { MapID = state.clientMap.ID, Action = PacketChangeMapColorsGradient.ActionType.ChangeGradientPointColor, GradientPointKey = result.gradientKey, GradientPointColor = result.gradientValue, Location = location, CelestialBodyID = cb.OwnID }.Send();
                                    break;
                                }

                                case ImGuiHelper.ImColorGradientsResult.ChangeKind.ClearCustomImage:
                                {
                                    new PacketChangeMapColorsGradient() { MapID = state.clientMap.ID, Action = PacketChangeMapColorsGradient.ActionType.SetImageAssetID, AssetID = Guid.Empty, Location = location, CelestialBodyID = cb.OwnID }.Send();
                                    break;
                                }
                            }
                        }

                        if (sbclrsAssetRecepticleHovered)
                        {
                            state.celestialBodyGradientAssetHovered = cb;
                            this._celestialBodyGradientEditedIsLightGradient = isLightGrad;
                        }
                    }

                    void CelestialBodySettings(CelestialBody cb, int idx)
                    {
                        if (ImGui.BeginChild($"###CelestialBodySettings_{cb.OwnID}", new Vector2(ImGui.GetContentRegionAvail().X, 0), ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.Borders))
                        {
                            bool sun = cb.IsSun;
                            ImGui.TextUnformatted(sun ? lang.Translate("ui.maps.celestial_body.sun") : lang.Translate("ui.maps.celestial_body.title", idx));
                            IntPtr previewTex = this.NoImageIcon.Texture;
                            Vector4 anim = new Vector4(0, 0, 1, 1);
                            switch (cb.RenderKind)
                            {
                                case CelestialBody.RenderPolicy.BuiltInSun:
                                case CelestialBody.RenderPolicy.BuiltInMoon:
                                case CelestialBody.RenderPolicy.BuiltInPlanetA:
                                case CelestialBody.RenderPolicy.BuiltInPlanetB:
                                case CelestialBody.RenderPolicy.BuiltInPlanetC:
                                case CelestialBody.RenderPolicy.BuiltInPlanetD:
                                case CelestialBody.RenderPolicy.BuiltInPlanetE:
                                {
                                    previewTex = Client.Instance.Frontend.Renderer.SkyRenderer.GetBuiltInTexture(cb.RenderKind);
                                    anim = new Vector4(0, 0, 1, 1);
                                    break;
                                }

                                case CelestialBody.RenderPolicy.Custom:
                                {
                                    if (!cb.AssetRef.IsEmpty())
                                    {
                                        AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.Previews.Get(cb.AssetRef, AssetType.Texture, out AssetPreview preview);
                                        if (status == AssetStatus.Return && preview != null && preview.GetGLTexture().AsyncState == AsyncLoadState.Ready)
                                        {
                                            Texture tex = preview.GetGLTexture();
                                            AssetPreview.FrameData animFrame = preview.GetCurrentFrame((int)(((Client.Instance.Frontend.UpdatesExisted) & int.MaxValue) * (100f / 60f)));
                                            previewTex = tex;
                                            anim = new Vector4(animFrame.X / (float)tex.Size.Width, animFrame.Y / (float)tex.Size.Height, (animFrame.X + animFrame.Width) / (float)tex.Size.Width, (animFrame.Y + animFrame.Height) / (float)tex.Size.Height);
                                        }
                                    }

                                    break;
                                }
                            }

                            Vector2 cHere = ImGui.GetCursorPos();
                            Vector4 color = cb.OwnColor.GetColor(null, this._celestialBodyPreviewTime);
                            ImGui.Image(previewTex, new Vector2(48, 48), anim.Xy(), anim.Zw(), color);
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.maps.celestial_body.preview.tt"));
                            }

                            Vector2 cAfter = ImGui.GetCursorPos();
                            ImGui.SetCursorPos(cHere + new Vector2(52, 0));
                            bool bEnable = cb.Enabled;
                            if (ImGui.Checkbox(lang.Translate("ui.maps.celestial_body.enabled") + "##Enabled", ref bEnable))
                            {
                                cb.Enabled = bEnable;
                                new PacketCelestialBodyInfo() { BodyID = cb.OwnID, MapID = state.clientMap.ID, ChangeKind = PacketCelestialBodyInfo.DataType.Enabled, Data = bEnable }.Send();
                            }

                            ImGui.SetCursorPos(cHere + new Vector2(52, ImGui.GetTextLineHeightWithSpacing() + 4));
                            bool bBillboard = cb.Billboard;
                            if (ImGui.Checkbox(lang.Translate("ui.maps.celestial_body.billboard") + "##Billboard", ref bBillboard))
                            {
                                cb.Billboard = bBillboard;
                                new PacketCelestialBodyInfo() { BodyID = cb.OwnID, MapID = state.clientMap.ID, ChangeKind = PacketCelestialBodyInfo.DataType.Billboard, Data = bBillboard }.Send();
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.maps.celestial_body.billboard.tt"));
                            }

                            ImGui.SetCursorPos(cHere + new Vector2(52, (ImGui.GetTextLineHeightWithSpacing() * 2) + 8));
                            bool bOwnTime = cb.UseOwnTime;
                            if (ImGui.Checkbox(lang.Translate("ui.maps.celestial_body.own_time") + "##UseOwnTime", ref bOwnTime))
                            {
                                cb.UseOwnTime = bOwnTime;
                                new PacketCelestialBodyInfo() { BodyID = cb.OwnID, MapID = state.clientMap.ID, ChangeKind = PacketCelestialBodyInfo.DataType.UseOwnTime, Data = bOwnTime }.Send();
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.maps.celestial_body.own_time.tt"));
                            }

                            ImGui.SetCursorPos(cAfter);
                            ImGui.PushItemWidth(48);
                            ImGui.SliderFloat("###OwnSimulatedTime", ref this._celestialBodyPreviewTime, 0f, 24f, string.Empty, ImGuiSliderFlags.NoRoundToFormat);
                            ImGui.PopItemWidth();
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.maps.celestial_body.time_simulation.tt"));
                            }

                            if (!sun)
                            {
                                ImGui.TextUnformatted(lang.Translate("ui.maps.celestial_body.position_policy"));
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip(lang.Translate("ui.maps.celestial_body.position_policy.tt"));
                                }

                                string[] positionPolicies = new string[] { lang.Translate("ui.maps.celestial_body.position_policy.angular"), lang.Translate("ui.maps.celestial_body.position_policy.follows_sun"), lang.Translate("ui.maps.celestial_body.position_policy.opposes_sun"), lang.Translate("ui.maps.celestial_body.position_policy.static") };
                                int ppIndex = (int)cb.PositionKind;
                                if (ImGui.Combo("###CBPositionPolicy", ref ppIndex, positionPolicies, positionPolicies.Length))
                                {
                                    cb.PositionKind = (CelestialBody.PositionPolicy)ppIndex;
                                    new PacketCelestialBodyInfo() { BodyID = cb.OwnID, MapID = state.clientMap.ID, ChangeKind = PacketCelestialBodyInfo.DataType.PositionPolicy, Data = cb.PositionKind }.Send();
                                }
                            }

                            ImGui.TextUnformatted(lang.Translate(
                                cb.PositionKind switch
                                { 
                                    CelestialBody.PositionPolicy.Angular => "ui.maps.celestial_body.yawpitchroll",
                                    CelestialBody.PositionPolicy.OpposesSun => "ui.maps.celestial_body.offset",
                                    CelestialBody.PositionPolicy.FollowsSun => "ui.maps.celestial_body.offset",
                                    CelestialBody.PositionPolicy.Static => "ui.maps.celestial_body.position",
                                    _ => "ui.maps.celestial_body.position"
                                }
                            ));

                            if (cb.PositionKind is CelestialBody.PositionPolicy.Angular or CelestialBody.PositionPolicy.FollowsSun or CelestialBody.PositionPolicy.OpposesSun)
                            {
                                float yaw = cb.Position.X;
                                float pitch = cb.Position.Y;
                                ImGui.TextUnformatted(lang.Translate("ui.maps.celestial_body.position.yaw"));
                                if (ImGui.SliderAngle("###Yaw", ref yaw, -180.0f, 180.0f))
                                {
                                    cb.SunYaw = yaw;
                                    new PacketCelestialBodyInfo() { BodyID = cb.OwnID, MapID = state.clientMap.ID, ChangeKind = PacketCelestialBodyInfo.DataType.Position, Data = cb.Position }.Send();
                                }

                                ImGui.TextUnformatted(lang.Translate("ui.maps.celestial_body.position.pitch"));
                                if (ImGui.SliderAngle("###Pitch", ref pitch, -180.0f, 180.0f))
                                {
                                    cb.SunPitch = pitch;
                                    new PacketCelestialBodyInfo() { BodyID = cb.OwnID, MapID = state.clientMap.ID, ChangeKind = PacketCelestialBodyInfo.DataType.Position, Data = cb.Position }.Send();
                                }
                            }
                            else
                            {
                                Vector3 p = cb.Position;
                                if (ImGui.SliderFloat3("###Position", ref p, -180.0f, 180.0f))
                                {
                                    cb.Position = p;
                                    new PacketCelestialBodyInfo() { BodyID = cb.OwnID, MapID = state.clientMap.ID, ChangeKind = PacketCelestialBodyInfo.DataType.Position, Data = cb.Position }.Send();
                                }
                            }

                            ImGui.TextUnformatted(lang.Translate("ui.maps.celestial_body.rotation"));
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.maps.celestial_body.rotation.tt"));
                            }

                            Vector3 hra = new Vector3(
                                cb.Rotation.X * 180.0f / MathF.PI,
                                cb.Rotation.Y * 180.0f / MathF.PI,
                                cb.Rotation.Z * 180.0f / MathF.PI
                            );

                            if (ImGui.SliderFloat3("###Rotation", ref hra, -180.0f, 180.0f))
                            {
                                cb.Rotation = new Vector3(
                                    hra.X * MathF.PI / 180.0f,
                                    hra.Y * MathF.PI / 180.0f,
                                    hra.Z * MathF.PI / 180.0f
                                );

                                new PacketCelestialBodyInfo() { BodyID = cb.OwnID, MapID = state.clientMap.ID, ChangeKind = PacketCelestialBodyInfo.DataType.Rotation, Data = cb.Rotation }.Send();
                            }

                            ImGui.TextUnformatted(lang.Translate("ui.maps.celestial_body.scale"));
                            Vector3 s = cb.Scale;
                            if (ImGui.DragFloat3("###Scale", ref s))
                            {
                                cb.Scale = s;
                                new PacketCelestialBodyInfo() { BodyID = cb.OwnID, MapID = state.clientMap.ID, ChangeKind = PacketCelestialBodyInfo.DataType.Scale, Data = cb.Scale }.Send();
                            }

                            ImGui.TextUnformatted(lang.Translate("ui.maps.celestial_body.render_type"));
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.maps.celestial_body.render_type.tt"));
                            }
                            string[] renderKinds = { lang.Translate("ui.maps.celestial_body.render.sun"), lang.Translate("ui.maps.celestial_body.render.moon"), lang.Translate("ui.maps.celestial_body.render.planetA"), lang.Translate("ui.maps.celestial_body.render.planetB"), lang.Translate("ui.maps.celestial_body.render.planetC"), lang.Translate("ui.maps.celestial_body.render.planetD"), lang.Translate("ui.maps.celestial_body.render.planetE"), lang.Translate("ui.maps.celestial_body.render.custom") };
                            int renderKind = (int)cb.RenderKind;
                            if (ImGui.Combo("###RenderKind", ref renderKind, renderKinds, renderKinds.Length))
                            {
                                cb.RenderKind = (CelestialBody.RenderPolicy)renderKind;
                                new PacketCelestialBodyInfo() { BodyID = cb.OwnID, MapID = state.clientMap.ID, ChangeKind = PacketCelestialBodyInfo.DataType.RenderPolicy, Data = cb.RenderKind }.Send();
                            }

                            if (cb.RenderKind == CelestialBody.RenderPolicy.Custom)
                            {
                                if (ImGuiHelper.ImAssetRecepticle(lang, cb.AssetRef, this.AssetModelIcon, new Vector2(0, 24), x => x.Type is AssetType.Model or AssetType.Texture, out bool assetEvalHovered))
                                {
                                    state.celestialBodyAssetHovered = cb;
                                }

                                if (assetEvalHovered)
                                {
                                    ImGui.SetTooltip(lang.Translate("ui.maps.celestial_body.custom_asset.tt"));
                                }
                            }

                            CelestialBodyColorGradient(cb, false);
                            if (cb.IsSun)
                            {
                                ImGui.NewLine();
                                CelestialBodyColorGradient(cb, true);
                                ImGui.TextUnformatted(lang.Translate("ui.maps.celestial_body.sun_shadow_policy"));
                                string[] shadowPolicies = { lang.Translate("ui.maps.celestial_body.sun_shadow_policy.normal"), lang.Translate("ui.maps.celestial_body.sun_shadow_policy.always"), lang.Translate("ui.maps.celestial_body.sun_shadow_policy.never") };
                                int sp = (int)cb.ShadowPolicy;
                                if (ImGui.Combo("###ShadowPolicy", ref sp, shadowPolicies, shadowPolicies.Length))
                                {
                                    cb.ShadowPolicy = (CelestialBody.ShadowCastingPolicy)sp;
                                    new PacketCelestialBodyInfo() { BodyID = cb.OwnID, MapID = state.clientMap.ID, ChangeKind = PacketCelestialBodyInfo.DataType.SunShadowPolicy, Data = cb.ShadowPolicy }.Send();
                                }
                            }

                            if (!cb.IsSun)
                            {
                                ImGui.NewLine();
                                if (ImGui.Button(lang.Translate("ui.maps.celestial_body.delete") + $"##DeleteBody{cb.OwnID}"))
                                {
                                    new PacketCreateOrDeleteCelestialBody() { MapID = state.clientMap.ID, BodyIDForDeletion = cb.OwnID, IsDeletion = true }.Send();
                                }
                            }
                        }

                        ImGui.EndChild();
                    }

                    if (ImGui.TreeNode(lang.Translate("ui.maps.celestial_bodies") + "##CelestialBodies"))
                    {
                        int cbidx = 0;
                        foreach (CelestialBody cb in state.clientMap.CelestialBodies)
                        {
                            CelestialBodySettings(cb, cbidx++);
                        }

                        if (ImGui.Button(lang.Translate("ui.maps.celestial_bodies.new") + "##NewCelestialBody"))
                        {
                            CelestialBody cb = new CelestialBody()
                            {
                                OwnID = Guid.NewGuid(),
                                OwnColor = new MapSkyboxColors() { OwnType = MapSkyboxColors.ColorsPointerType.FullWhite },
                                RenderKind = (CelestialBody.RenderPolicy)this.Random.Next(7),
                                Scale = new Vector3(8, 8, 8)
                            };

                            new PacketCreateOrDeleteCelestialBody() { MapID = state.clientMap.ID, IsDeletion = false, BodyForAddition = cb }.Send();
                        }

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

                    bool mLightsShadows = state.clientMap.EnablePointShadows;
                    if (ImGui.Checkbox(lang.Translate("ui.maps.point_shadows") + "###Enable Light Shadows", ref mLightsShadows))
                    {
                        state.clientMap.EnablePointShadows = mLightsShadows;
                        PacketChangeMapData pcmd = new PacketChangeMapData() { Data = mLightsShadows, IsServer = false, MapID = state.clientMap.ID, Session = Client.Instance.SessionID, Type = PacketChangeMapData.DataType.EnablePointShadows };
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
