namespace VTT.Render.Gui
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Numerics;
    using VTT.Asset;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public partial class GuiRenderer
    {
        private bool ImSidebarBtn(string id, Vector2 size, Texture image, bool active, out bool hovered)
        {
            bool ret = false;
            Vector2 cHere = ImGui.GetCursorScreenPos();
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            if (ImGui.InvisibleButton(id, size))
            {
                ret = true;
            }

            hovered = ImGui.IsItemHovered();
            drawList.AddRect(cHere, cHere + size, ImGui.GetColorU32(active ? ImGuiCol.ButtonActive : hovered ? ImGuiCol.ButtonHovered : ImGuiCol.Button), 6f);
            drawList.AddImageRounded(image, cHere + new Vector2(4, 4), cHere + size - new Vector2(4, 4), Vector2.Zero, Vector2.One, 0xffffffff, 6f);
            return ret;
        }

        private unsafe void RenderSidebar(Map m, SimpleLanguage lang, ImGuiWindowFlags window_flags, MapObjectRenderer mor)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            ImGui.SetNextWindowBgAlpha(0f);
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(4, 4));
            if (ImGui.Begin("Mode Controls", window_flags | ImGuiWindowFlags.NoBackground))
            {
                ImGui.PopStyleVar();
                for (int i = 0; i < 9; ++i)
                {
                    if (!Client.Instance.IsAdmin && (i == (int)EditMode.FOW || i == (int)EditMode.FX || i == (int)EditMode.Shadows2D))
                    {
                        continue;
                    }

                    if (i == (int)EditMode.Shadows2D && (m == null || !m.Is2D))
                    {
                        continue;
                    }

                    /* Old code
                    bool selected = (int)mor.EditMode == i;
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
                    ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
                    if (selected)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Border, *ImGui.GetStyleColorVec4(ImGuiCol.ButtonActive));
                    }

                    if (ImGui.ImageButton("btnMode_" + i, _modeTextures[i], Vec32x32, Vector2.Zero, Vector2.One, Vector4.Zero))
                    {
                        mor.EditMode = (EditMode)i;
                    }

                    if (selected)
                    {
                        ImGui.PopStyleColor();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        string modett = lang.Translate("ui.mode." + ((EditMode)i).ToString().ToLower());
                        ImGui.SetTooltip(modett);
                    }

                    ImGui.PopStyleColor();
                    ImGui.PopStyleColor();
                    ImGui.PopStyleVar();
                    ImGui.PopStyleVar();
                    */
                    if (this.ImSidebarBtn($"btnMode_{i}", Vec32x32, this._modeTextures[i], (int)mor.EditMode == i, out bool hovered))
                    {
                        mor.EditMode = (EditMode)i;
                    }

                    if (hovered)
                    {
                        ImGui.SetTooltip(lang.Translate($"ui.mode.{Enum.GetName((EditMode)i).ToLower()}"));
                    }
                }

                if (Client.Instance.IsAdmin)
                {
                    if (this.ImSidebarBtn($"btnOpenTurnTracker", Vec32x32, this.ToggleTurnOrder, this._showingTurnOrder, out bool hovered))
                    {
                        this._showingTurnOrder = !this._showingTurnOrder;
                    }

                    if (hovered)
                    {
                        ImGui.SetTooltip(lang.Translate("ui.turn_tracker"));
                    }
                }
            }
            else
            {
                ImGui.PopStyleVar();
            }

            ImGui.End();
        }

        private unsafe void RenderDebugInfo(double time, ImGuiWindowFlags window_flags, GuiState state)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            ImGui.SetNextWindowBgAlpha(0.35f);
            ImGui.SetNextWindowPos(SidebarFirstEntryPosition);
            if (DebugEnabled)
            {
                if (ImGui.Begin("SidebarDebugOverlay", window_flags))
                {
                    state.renderedDebugOverlay = true;
                    PerformanceMetrics pm = Client.Instance.Frontend.GameHandle.MetricsFramerate;
                    ImGui.TextUnformatted($"Frame: {((double)pm.LastTickAvg / TimeSpan.TicksPerMillisecond):0.000}ms, {pm.LastNumFrames} frames");
                    ImGui.Text("Cursor: " + Client.Instance.Frontend.MouseX + ", " + Client.Instance.Frontend.MouseY);

                    Vector3? cw = Client.Instance.Frontend.Renderer.MapRenderer.TerrainHit;
                    ImGui.Text("World: " + (cw.HasValue ? cw.Value.ToString() : "null"));
                }

                ImGui.End();
            }
        }
        private unsafe void RenderFOWControls(MapObjectRenderer mor, SimpleLanguage lang, ImGuiWindowFlags window_flags, GuiState state)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            if (mor.EditMode == EditMode.FOW && Client.Instance.IsAdmin)
            {
                FOWRenderer fowr = Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer;
                ImGui.SetNextWindowBgAlpha(0.2f);
                ImGui.SetNextWindowPos(state.renderedDebugOverlay ? SidebarSecondEntryPosition : SidebarFirstEntryPosition);
                if (ImGui.Begin("##FOWControls", window_flags))
                {
                    if (this.ImSidebarBtn("btnFowControls", Vec32x32, this.FOWRevealIcon, fowr.CanvasMode == FOWRenderer.RevealMode.Reveal, out bool hovered))
                    {
                        fowr.CanvasMode = FOWRenderer.RevealMode.Reveal;
                    }

                    if (hovered)
                    {
                        ImGui.SetTooltip(lang.Translate("ui.fow.reveal"));
                    }

                    ImGui.SameLine();
                    if (this.ImSidebarBtn("btnFowHide", Vec32x32, this.FOWHideIcon, fowr.CanvasMode == FOWRenderer.RevealMode.Hide, out hovered))
                    {
                        fowr.CanvasMode = FOWRenderer.RevealMode.Hide;
                    }

                    if (hovered)
                    {
                        ImGui.SetTooltip(lang.Translate("ui.fow.hide"));
                    }

                    if (this.ImSidebarBtn("btnFowBox", Vec32x32, this.FOWModeBox, fowr.PaintMode == FOWRenderer.SelectionMode.Box, out hovered))
                    {
                        fowr.PaintMode = FOWRenderer.SelectionMode.Box;
                    }

                    if (hovered)
                    {
                        ImGui.SetTooltip(lang.Translate("ui.fow.box"));
                    }

                    ImGui.SameLine();
                    if (this.ImSidebarBtn("btnFowPoly", Vec32x32, this.FOWModePolygon, fowr.PaintMode == FOWRenderer.SelectionMode.Polygon, out hovered))
                    {
                        fowr.PaintMode = FOWRenderer.SelectionMode.Polygon;
                    }

                    if (hovered)
                    {
                        ImGui.SetTooltip(lang.Translate("ui.fow.polygon"));
                    }

                    ImGui.SameLine();
                    if (this.ImSidebarBtn("btnFowBrush", Vec32x32, this.FOWModeBrush, fowr.PaintMode == FOWRenderer.SelectionMode.Brush, out hovered))
                    {
                        fowr.PaintMode = FOWRenderer.SelectionMode.Brush;
                    }

                    if (hovered)
                    {
                        ImGui.SetTooltip(lang.Translate("ui.fow.draw"));
                    }

                    float cBSize = fowr.BrushSize;
                    ImGui.PushStyleColor(ImGuiCol.FrameBg, *ImGui.GetStyleColorVec4(ImGuiCol.FrameBg) * new Vector4(1, 1, 1, 0.4f));
                    ImGui.PushStyleColor(ImGuiCol.FrameBgActive, *ImGui.GetStyleColorVec4(ImGuiCol.FrameBgActive) * new Vector4(1, 1, 1, 0.4f));
                    ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, *ImGui.GetStyleColorVec4(ImGuiCol.FrameBgHovered) * new Vector4(1, 1, 1, 0.4f));
                    if (ImGui.SliderFloat("##BrushSize", ref cBSize, 0.0625f, 8f))
                    {
                        fowr.BrushSize = cBSize;
                    }

                    ImGui.PopStyleColor(3);
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.fow.brush_size"));
                    }
                }

                ImGui.End();
            }
        }

        private unsafe void RenderMeasureControls(MapObjectRenderer mor, SimpleLanguage lang, ImGuiWindowFlags window_flags, GuiState state)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            if (mor.EditMode == EditMode.Measure)
            {
                ImGui.SetNextWindowBgAlpha(0.2f);
                ImGui.SetNextWindowPos(state.renderedDebugOverlay ? SidebarSecondEntryPosition : SidebarFirstEntryPosition);
                if (ImGui.Begin("##MeasureControls", window_flags))
                {
                    for (int i = 0; i < 9; ++i)
                    {
                        RulerType iMode = (RulerType)i;
                        if (this.ImSidebarBtn("##RulerModeBtn" + i, Vec32x32, this._rulerModeTextures[i], Client.Instance.Frontend.Renderer.RulerRenderer.CurrentMode == iMode, out bool hovered))
                        {
                            Client.Instance.Frontend.Renderer.RulerRenderer.CurrentMode = iMode;
                        }

                        if (hovered)
                        {
                            ImGui.SetTooltip(lang.Translate("ui.measure." + iMode.ToString().ToLower()));
                        }

                        if (i != 8)
                        {
                            ImGui.SameLine();
                        }
                    }

                    ImGui.PushStyleColor(ImGuiCol.FrameBg, *ImGui.GetStyleColorVec4(ImGuiCol.FrameBg) * new Vector4(1, 1, 1, 0.4f));
                    ImGui.PushStyleColor(ImGuiCol.FrameBgActive, *ImGui.GetStyleColorVec4(ImGuiCol.FrameBgActive) * new Vector4(1, 1, 1, 0.4f));
                    ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, *ImGui.GetStyleColorVec4(ImGuiCol.FrameBgHovered) * new Vector4(1, 1, 1, 0.4f));
                    float rExtraData = Client.Instance.Frontend.Renderer.RulerRenderer.CurrentExtraValue;
                    if (ImGui.DragFloat("##RulerModeExtraData", ref rExtraData, 0.1f))
                    {
                        Client.Instance.Frontend.Renderer.RulerRenderer.CurrentExtraValue = rExtraData;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.measure.extra.tt"));
                    }

                    bool bDisplayInfos = Client.Instance.Frontend.Renderer.RulerRenderer.RulersDisplayInfo;
                    if (ImGui.Checkbox(lang.Translate("ui.measure.display_infos") + "###RulersDisplayInfo", ref bDisplayInfos))
                    {
                        Client.Instance.Frontend.Renderer.RulerRenderer.RulersDisplayInfo = bDisplayInfos;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.measure.display_infos.tt"));
                    }

                    if (Client.Instance.Frontend.Renderer.RulerRenderer.CurrentMode == RulerType.Eraser)
                    {
                        Client.Instance.TryGetClientNamesArray(Client.Instance.Frontend.Renderer.RulerRenderer.CurrentEraserMask, out int id, out string[] names, out Guid[] ids);
                        if (ImGui.Combo(lang.Translate("ui.measure.eraser_mask") + "###EraserMask", ref id, names, names.Length))
                        {
                            Client.Instance.Frontend.Renderer.RulerRenderer.CurrentEraserMask = ids[id];
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.measure.eraser_mask.tt"));
                        }
                    }

                    ImGui.Separator();
                    if (ImGui.TreeNode(lang.Translate("ui.measure.color") + "###RulerColorPicker"))
                    {
                        Vector3 cclr = Client.Instance.Frontend.Renderer.RulerRenderer.CurrentColor.Xyz();
                        ImGui.PushItemWidth(150);
                        if (ImGui.ColorPicker3("##RulerColorPickerD", ref cclr, ImGuiColorEditFlags.PickerHueWheel | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoSidePreview))
                        {
                            Client.Instance.Frontend.Renderer.RulerRenderer.CurrentColor = new Vector4(cclr, 1.0f);
                        }

                        ImGui.PopItemWidth();
                        string rTt = Client.Instance.Frontend.Renderer.RulerRenderer.CurrentTooltip;
                        if (ImGui.InputText("##RulerColorPickerTT", ref rTt, 64))
                        {
                            Client.Instance.Frontend.Renderer.RulerRenderer.CurrentTooltip = rTt;
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.measure.tooltip.tt"));
                        }

                        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                        if (ImGui.Button(lang.Translate("ui.generic.reset") + "###RulerClear"))
                        {
                            Client.Instance.Frontend.Renderer.RulerRenderer.CurrentColor = Extensions.FromArgb(Client.Instance.Settings.Color).Vec4();
                            Client.Instance.Frontend.Renderer.RulerRenderer.CurrentTooltip = string.Empty;
                        }

                        ImGui.PopStyleColor();
                        ImGui.TreePop();
                    }

                    ImGui.PopStyleColor(3);
                }

                ImGui.End();
            }
        }

        private unsafe void RenderTranslationControls(MapObjectRenderer mor, SimpleLanguage lang, ImGuiWindowFlags window_flags, GuiState state)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            if (mor.EditMode == EditMode.Translate)
            {
                ImGui.SetNextWindowBgAlpha(0.2f);
                ImGui.SetNextWindowPos(state.renderedDebugOverlay ? SidebarSecondEntryPosition : SidebarFirstEntryPosition);
                ImGui.Begin("##TranslateControls", window_flags);
                for (int i = 0; i < 3; ++i)
                {
                    TranslationMode tMode = (TranslationMode)i;
                    string modeName = Enum.GetName(tMode).ToLower();
                    if (this.ImSidebarBtn($"btnMovementMode_{modeName}", Vec32x32, this._moveModeTextures[i], tMode == Client.Instance.Frontend.Renderer.ObjectRenderer.MovementMode, out bool hovered))
                    {
                        Client.Instance.Frontend.Renderer.ObjectRenderer.MovementMode = tMode;
                    }

                    if (hovered)
                    {
                        ImGui.SetTooltip(lang.Translate("ui.translate." + modeName));
                    }

                    ImGui.SameLine();
                }

                ImGui.End();
            }
        }

        private unsafe void RenderCameraControls(MapObjectRenderer mor, SimpleLanguage lang, ImGuiWindowFlags window_flags, GuiState state)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            if (mor.EditMode == EditMode.Select)
            {
                ImGui.SetNextWindowBgAlpha(0.2f);
                ImGui.SetNextWindowPos(state.renderedDebugOverlay ? SidebarSecondEntryPosition : SidebarFirstEntryPosition);
                if (ImGui.Begin("##CameraMoveControls", window_flags))
                {
                    CameraControlMode ccm = Client.Instance.Frontend.Renderer.MapRenderer.CameraControlMode;
                    if (this.ImSidebarBtn("btnSelectCameraStd", Vec32x32, this.Select, ccm == CameraControlMode.Standard, out bool hovered))
                    {
                        Client.Instance.Frontend.Renderer.MapRenderer.CameraControlMode = CameraControlMode.Standard;
                    }

                    if (hovered)
                    {
                        ImGui.SetTooltip(lang.Translate("ui.camera.standard"));
                    }

                    ImGui.SameLine();
                    if (this.ImSidebarBtn("btnSelectCameraMove", Vec32x32, this.CameraMove, ccm == CameraControlMode.Move, out hovered))
                    {
                        Client.Instance.Frontend.Renderer.MapRenderer.CameraControlMode = CameraControlMode.Move;
                    }

                    if (hovered)
                    {
                        ImGui.SetTooltip(lang.Translate("ui.camera.move"));
                    }

                    ImGui.SameLine();
                    if (this.ImSidebarBtn("btnSelectCameraRotate", Vec32x32, this.CameraRotate, ccm == CameraControlMode.Rotate, out hovered))
                    {
                        Client.Instance.Frontend.Renderer.MapRenderer.CameraControlMode = CameraControlMode.Rotate;
                    }

                    if (hovered)
                    {
                        ImGui.SetTooltip(lang.Translate("ui.camera.rotate"));
                    }
                }

                ImGui.End();
            }
        }

        private unsafe void RenderDrawControls(MapObjectRenderer mor, SimpleLanguage lang, ImGuiWindowFlags window_flags, GuiState state)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            if (mor.EditMode == EditMode.Draw)
            {
                ImGui.SetNextWindowBgAlpha(0.2f);
                ImGui.SetNextWindowPos(state.renderedDebugOverlay ? SidebarSecondEntryPosition : SidebarFirstEntryPosition);
                if (ImGui.Begin("##DrawControls", window_flags))
                {
                    if (this.ImSidebarBtn("##DrawModeBtnDraw", Vec32x32, this.FOWModeBrush, Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.IsDrawing, out bool hovered))
                    {
                        Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.IsDrawing = true;
                    }

                    if (hovered)
                    {
                        ImGui.SetTooltip(lang.Translate("ui.draw.brush.tt"));
                    }

                    ImGui.SameLine();
                    if (this.ImSidebarBtn("##DrawModeBtnErase", Vec32x32, this.MeasureModeErase, !Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.IsDrawing, out hovered))
                    {
                        Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.IsDrawing = false;
                    }

                    if (hovered)
                    {
                        ImGui.SetTooltip(lang.Translate("ui.draw.erase.tt"));
                    }

                    float radius = Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.CurrentRadius;
                    ImGui.PushStyleColor(ImGuiCol.FrameBg, *ImGui.GetStyleColorVec4(ImGuiCol.FrameBg) * new Vector4(1, 1, 1, 0.4f));
                    ImGui.PushStyleColor(ImGuiCol.FrameBgActive, *ImGui.GetStyleColorVec4(ImGuiCol.FrameBgActive) * new Vector4(1, 1, 1, 0.4f));
                    ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, *ImGui.GetStyleColorVec4(ImGuiCol.FrameBgHovered) * new Vector4(1, 1, 1, 0.4f));
                    if (ImGui.SliderFloat("##DrawModeRadius", ref radius, 0.1f, 10f))
                    {
                        Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.CurrentRadius = MathF.Max(0.025f, radius);
                    }

                    ImGui.PopStyleColor(3);
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.draw.radius.tt"));
                    }

                    if (!Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.IsDrawing)
                    {
                        Client.Instance.TryGetClientNamesArray(Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.CurrentEraserMask, out int id, out string[] names, out Guid[] ids);
                        if (ImGui.Combo(lang.Translate("ui.measure.eraser_mask") + "###DrawEraserMask", ref id, names, names.Length))
                        {
                            Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.CurrentEraserMask = ids[id];
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.measure.eraser_mask.tt"));
                        }
                    }

                    ImGui.Separator();
                    if (ImGui.TreeNode(lang.Translate("ui.generic.color") + "###DrawColorPicker"))
                    {
                        Vector3 cclr = Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.CurrentColor.Xyz();
                        ImGui.PushItemWidth(150);
                        if (ImGui.ColorPicker3("##DrawColorPickerD", ref cclr, ImGuiColorEditFlags.PickerHueWheel | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoSidePreview))
                        {
                            Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.CurrentColor = new Vector4(cclr, 1.0f);
                        }

                        if (ImGui.Button(lang.Translate("ui.generic.reset") + "###DrawClear"))
                        {
                            Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.CurrentColor = Extensions.FromArgb(Client.Instance.Settings.Color).Vec4();
                        }

                        ImGui.PopItemWidth();
                        ImGui.TreePop();
                    }
                }

                ImGui.End();
            }
        }

        private Guid _fxToEmitParticleSystemID;
        private int _fxNumToEmit = 32;

        public Guid FXToEmitParticleSystemID => this._fxToEmitParticleSystemID;
        public int FXNumToEmit => this._fxNumToEmit;

        private unsafe void RenderFXControls(MapObjectRenderer mor, SimpleLanguage lang, ImGuiWindowFlags window_flags, GuiState state)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            if (mor.EditMode == EditMode.FX)
            {
                ImGui.SetNextWindowBgAlpha(0.2f);
                ImGui.SetNextWindowPos(state.renderedDebugOverlay ? SidebarSecondEntryPosition : SidebarFirstEntryPosition);
                if (ImGui.Begin("##FXControls", window_flags))
                {
                    ImDrawListPtr drawList = ImGui.GetWindowDrawList();
                    Vector2 imScreenPos = ImGui.GetCursorScreenPos();
                    Vector2 rectEnd = imScreenPos + new Vector2(ImGui.GetContentRegionAvail().X, 24);
                    bool mouseOver = ImGui.IsMouseHoveringRect(imScreenPos, rectEnd);
                    uint bClr = mouseOver ? this._draggedRef != null && this._draggedRef.Type == AssetType.ParticleSystem ? ImGui.GetColorU32(ImGuiCol.HeaderHovered) : ImGui.GetColorU32(ImGuiCol.ButtonHovered) : ImGui.GetColorU32(ImGuiCol.Border);
                    drawList.AddRect(imScreenPos, rectEnd, bClr);
                    drawList.AddImage(this.AssetParticleIcon, imScreenPos + new Vector2(4, 4), imScreenPos + new Vector2(20, 20));
                    string mdlTxt = "";
                    int mdlTxtOffset = 0;
                    if (Client.Instance.AssetManager.Refs.ContainsKey(this._fxToEmitParticleSystemID))
                    {
                        AssetRef aRef = Client.Instance.AssetManager.Refs[this._fxToEmitParticleSystemID];
                        mdlTxt += aRef.Name;
                        if (Client.Instance.AssetManager.ClientAssetLibrary.Previews.Get(this._fxToEmitParticleSystemID, AssetType.Texture, out AssetPreview ap) == AssetStatus.Return && ap != null)
                        {
                            Texture tex = ap.GetGLTexture();
                            if (tex != null)
                            {
                                drawList.AddImage(tex, imScreenPos + new Vector2(20, 4), imScreenPos + new Vector2(36, 20));
                                mdlTxtOffset += 20;
                            }
                        }
                    }

                    if (this._fxToEmitParticleSystemID.IsEmpty())
                    {
                        mdlTxt = lang.Translate("generic.none");
                    }
                    else
                    {
                        mdlTxt += " (" + this._fxToEmitParticleSystemID.ToString() + ")\0";
                    }

                    drawList.PushClipRect(imScreenPos, rectEnd);
                    drawList.AddText(imScreenPos + new Vector2(20 + mdlTxtOffset, 4), ImGui.GetColorU32(ImGuiCol.Text), mdlTxt);
                    drawList.PopClipRect();
                    ImGui.Dummy(new Vector2(0, 28));
                    if (mouseOver)
                    {
                        state.movingParticleAssetOverFXRecepticle = true;
                        ImGui.SetTooltip(lang.Translate("ui.fx.particle.tt"));
                    }

                    int iFxToEmit = this._fxNumToEmit;
                    ImGui.PushStyleColor(ImGuiCol.FrameBg, *ImGui.GetStyleColorVec4(ImGuiCol.FrameBg) * new Vector4(1, 1, 1, 0.4f));
                    ImGui.PushStyleColor(ImGuiCol.FrameBgActive, *ImGui.GetStyleColorVec4(ImGuiCol.FrameBgActive) * new Vector4(1, 1, 1, 0.4f));
                    ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, *ImGui.GetStyleColorVec4(ImGuiCol.FrameBgHovered) * new Vector4(1, 1, 1, 0.4f));
                    if (ImGui.DragInt(lang.Translate("ui.fx.num_emit") + "###NumParticlesToEmit", ref iFxToEmit))
                    {
                        this._fxNumToEmit = iFxToEmit;
                    }

                    ImGui.PopStyleColor(3);
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.fx.num_emit.tt"));
                    }
                }

                ImGui.End();
            }
        }

        private unsafe void RenderShadows2DControls(Shadow2DRenderer renderer, SimpleLanguage lang, ImGuiWindowFlags window_flags, GuiState state)
        {
            if (renderer == null || this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            if (Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode == EditMode.Shadows2D)
            {
                ImGui.SetNextWindowBgAlpha(0.2f);
                ImGui.SetNextWindowPos(state.renderedDebugOverlay ? SidebarSecondEntryPosition : SidebarFirstEntryPosition);
                if (ImGui.Begin("##Shadow2DControls", window_flags))
                {
                    Shadow2DControlMode currentMode = renderer.ControlMode;
                    for (int i = 0; i < 9; ++i)
                    {
                        if (this.ImSidebarBtn($"##Shadow2DModeBtn_{i}", Vec32x32, Client.Instance.Frontend.Renderer.GuiRenderer.Shadow2DControlModeTextures[i], (Shadow2DControlMode)i == currentMode, out bool hovered))
                        {
                            renderer.ControlMode = (Shadow2DControlMode)i;
                        }

                        ImGui.SameLine();
                        if (hovered)
                        {
                            ImGui.SetTooltip(lang.Translate("ui.shadow2d.mode_" + Enum.GetName((Shadow2DControlMode)i).ToLower() + ".tt"));
                        }
                    }
                }

                ImGui.End();
            }
        }

        private static bool ImImageButton(string id, Texture image, Vector2 size, bool border)
        {
            if (border)
            {
                ImGui.PushStyleColor(ImGuiCol.Border, (Vector4)Color.RoyalBlue);
            }

            bool ret = ImGui.ImageButton(id, image, size, Vector2.Zero, Vector2.One, Vector4.Zero);

            if (border)
            {
                ImGui.PopStyleColor();
            }

            return ret;
        }
    }
}
