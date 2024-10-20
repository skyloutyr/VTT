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
        private unsafe void RenderSidebar(Map m, SimpleLanguage lang, ImGuiWindowFlags window_flags, MapObjectRenderer mor)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            ImGui.SetNextWindowBgAlpha(0.35f);
            ImGui.SetNextWindowPos(Vector2.Zero);
            if (ImGui.Begin("Mode Controls", window_flags))
            {
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

                    bool selected = (int)mor.EditMode == i;
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);
                    ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                    if (selected)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Border, (Vector4)Color.RoyalBlue);
                    }

                    if (ImGui.ImageButton("btnMode_" + i, _modeTextures[i], Vec32x32, Vector2.Zero, Vector2.One, Vector4.Zero))
                    {
                        mor.EditMode = (EditMode)i;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        string modett = lang.Translate("ui.mode." + ((EditMode)i).ToString().ToLower());
                        ImGui.SetTooltip(modett);
                    }

                    if (selected)
                    {
                        ImGui.PopStyleColor();
                    }

                    ImGui.PopStyleColor();
                    ImGui.PopStyleVar();
                    if (i != 8)
                    {
                        ImGui.NewLine();
                    }
                }

                if (Client.Instance.IsAdmin)
                {
                    ImGui.NewLine();
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);
                    ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                    if (this._showingTurnOrder)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Border, (Vector4)Color.RoyalBlue);
                    }

                    bool sto = this._showingTurnOrder;
                    if (ImGui.ImageButton("btnOpenTurnTracker", this.ToggleTurnOrder, Vec32x32, Vector2.Zero, Vector2.One, Vector4.Zero))
                    {
                        this._showingTurnOrder = !this._showingTurnOrder;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.turn_tracker"));
                    }

                    if (sto)
                    {
                        ImGui.PopStyleColor();
                    }

                    ImGui.PopStyleColor();
                    ImGui.PopStyleVar();
                }
            }

            ImGui.End();
        }

        private unsafe void RenderDebugInfo(double time, ImGuiWindowFlags window_flags)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            ImGui.SetNextWindowBgAlpha(0.35f);
            ImGui.SetNextWindowPos(Vec56x0);
            if (DebugEnabled)
            {
                if (ImGui.Begin("Debug", window_flags))
                {
                    PerformanceMetrics pm = Client.Instance.Frontend.GameHandle.MetricsFramerate;
                    ImGui.TextUnformatted($"Frame: {((double)pm.LastTickAvg / TimeSpan.TicksPerMillisecond):0.000}ms, {pm.LastNumFrames} frames");
                    ImGui.Text("Cursor: " + Client.Instance.Frontend.MouseX + ", " + Client.Instance.Frontend.MouseY);

                    Vector3? cw = Client.Instance.Frontend.Renderer.MapRenderer.TerrainHit;
                    ImGui.Text("World: " + (cw.HasValue ? cw.Value.ToString() : "null"));
                }

                ImGui.End();
            }
        }
        private unsafe void RenderFOWControls(MapObjectRenderer mor, SimpleLanguage lang, ImGuiWindowFlags window_flags)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            if (mor.EditMode == EditMode.FOW && Client.Instance.IsAdmin)
            {
                FOWRenderer fowr = Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer;
                ImGui.SetNextWindowBgAlpha(0.35f);
                ImGui.SetNextWindowPos(Vec56x70);
                ImGui.Begin("##FOWControls", window_flags);

                ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);

                bool selected = fowr.CanvasMode == FOWRenderer.RevealMode.Reveal;
                if (selected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Border, (Vector4)Color.RoyalBlue);
                }

                if (ImGui.ImageButton("btnFowControls", this.FOWRevealIcon, Vec32x32, Vector2.Zero, Vector2.One, Vector4.Zero))
                {
                    fowr.CanvasMode = FOWRenderer.RevealMode.Reveal;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.fow.reveal"));
                }

                if (selected)
                {
                    ImGui.PopStyleColor();
                }

                ImGui.SameLine();

                if (!selected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Border, (Vector4)Color.RoyalBlue);
                }

                if (ImGui.ImageButton("btnFowHide", this.FOWHideIcon, Vec32x32, Vector2.Zero, Vector2.One, Vector4.Zero))
                {
                    fowr.CanvasMode = FOWRenderer.RevealMode.Hide;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.fow.hide"));
                }

                if (!selected)
                {
                    ImGui.PopStyleColor();
                }

                selected = fowr.PaintMode == FOWRenderer.SelectionMode.Box;
                if (selected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Border, (Vector4)Color.RoyalBlue);
                }

                if (ImGui.ImageButton("btnFowBox", this.FOWModeBox, Vec32x32, Vector2.Zero, Vector2.One, Vector4.Zero))
                {
                    fowr.PaintMode = FOWRenderer.SelectionMode.Box;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.fow.box"));
                }

                if (selected)
                {
                    ImGui.PopStyleColor();
                }

                ImGui.SameLine();
                selected = fowr.PaintMode == FOWRenderer.SelectionMode.Polygon;
                if (selected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Border, (Vector4)Color.RoyalBlue);
                }

                if (ImGui.ImageButton("btnFowPoly", this.FOWModePolygon, Vec32x32, Vector2.Zero, Vector2.One, Vector4.Zero))
                {
                    fowr.PaintMode = FOWRenderer.SelectionMode.Polygon;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.fow.polygon"));
                }

                if (selected)
                {
                    ImGui.PopStyleColor();
                }

                ImGui.SameLine();

                selected = fowr.PaintMode == FOWRenderer.SelectionMode.Brush;
                if (selected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Border, (Vector4)Color.RoyalBlue);
                }

                if (ImGui.ImageButton("btnFowBrush", this.FOWModeBrush, Vec32x32, Vector2.Zero, Vector2.One, Vector4.Zero))
                {
                    fowr.PaintMode = FOWRenderer.SelectionMode.Brush;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.fow.draw"));
                }

                if (selected)
                {
                    ImGui.PopStyleColor();
                }

                ImGui.PopStyleColor();
                ImGui.PopStyleVar();

                float cBSize = fowr.BrushSize;
                if (ImGui.SliderFloat("##BrushSize", ref cBSize, 0.0625f, 8f))
                {
                    fowr.BrushSize = cBSize;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.fow.brush_size"));
                }

                ImGui.End();
            }
        }

        private unsafe void RenderMeasureControls(MapObjectRenderer mor, SimpleLanguage lang, ImGuiWindowFlags window_flags)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            if (mor.EditMode == EditMode.Measure)
            {
                ImGui.SetNextWindowBgAlpha(0.35f);
                ImGui.SetNextWindowPos(Vec56x70);
                ImGui.Begin("##MeasureControls", window_flags);

                ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);

                for (int i = 0; i < 9; ++i)
                {
                    RulerType iMode = (RulerType)i;
                    bool selected = Client.Instance.Frontend.Renderer.RulerRenderer.CurrentMode == iMode;
                    if (ImImageButton("##RulerModeBtn" + i, this._rulerModeTextures[i], Vec32x32, selected))
                    {
                        Client.Instance.Frontend.Renderer.RulerRenderer.CurrentMode = iMode;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.measure." + iMode.ToString().ToLower()));
                    }

                    if (i != 8)
                    {
                        ImGui.SameLine();
                    }
                }

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
                if (ImGui.TreeNode(lang.Translate("ui.generic.color") + "###RulerColorPicker"))
                {
                    Vector3 cclr = Client.Instance.Frontend.Renderer.RulerRenderer.CurrentColor.Xyz();
                    ImGui.PushItemWidth(200);
                    if (ImGui.ColorPicker3("##RulerColorPickerD", ref cclr, ImGuiColorEditFlags.PickerHueWheel | ImGuiColorEditFlags.NoInputs))
                    {
                        Client.Instance.Frontend.Renderer.RulerRenderer.CurrentColor = new Vector4(cclr, 1.0f);
                    }

                    string rTt = Client.Instance.Frontend.Renderer.RulerRenderer.CurrentTooltip;
                    if (ImGui.InputText("##RulerColorPickerTT", ref rTt, 64))
                    {
                        Client.Instance.Frontend.Renderer.RulerRenderer.CurrentTooltip = rTt;
                    }

                    if (ImGui.Button(lang.Translate("ui.generic.reset") + "###RulerClear"))
                    {
                        Client.Instance.Frontend.Renderer.RulerRenderer.CurrentColor = Extensions.FromArgb(Client.Instance.Settings.Color).Vec4();
                        Client.Instance.Frontend.Renderer.RulerRenderer.CurrentTooltip = string.Empty;
                    }

                    ImGui.PopItemWidth();
                    ImGui.TreePop();
                }

                ImGui.PopStyleColor();
                ImGui.PopStyleVar();
                ImGui.End();
            }
        }

        private unsafe void RenderTranslationControls(MapObjectRenderer mor, SimpleLanguage lang, ImGuiWindowFlags window_flags)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            if (mor.EditMode == EditMode.Translate)
            {
                ImGui.SetNextWindowBgAlpha(0.35f);
                ImGui.SetNextWindowPos(Vec56x70);
                ImGui.Begin("##TranslateControls", window_flags);

                ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);

                bool selected = Client.Instance.Frontend.Renderer.ObjectRenderer.MoveModeArrows;
                if (selected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Border, (Vector4)Color.RoyalBlue);
                }

                if (ImGui.ImageButton("btnMoveArrows", this.MoveArrows, Vec32x32, Vector2.Zero, Vector2.One, Vector4.Zero))
                {
                    Client.Instance.Frontend.Renderer.ObjectRenderer.MoveModeArrows = true;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.translate.arrows"));
                }

                if (selected)
                {
                    ImGui.PopStyleColor();
                }

                ImGui.SameLine();

                if (!selected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Border, (Vector4)Color.RoyalBlue);
                }

                if (ImGui.ImageButton("btnMoveGizmo", this.MoveGizmo, Vec32x32, Vector2.Zero, Vector2.One, Vector4.Zero))
                {
                    Client.Instance.Frontend.Renderer.ObjectRenderer.MoveModeArrows = false;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.translate.gizmo"));
                }

                if (!selected)
                {
                    ImGui.PopStyleColor();
                }

                ImGui.PopStyleColor();
                ImGui.PopStyleVar();
                ImGui.End();
            }
        }

        private unsafe void RenderCameraControls(MapObjectRenderer mor, SimpleLanguage lang, ImGuiWindowFlags window_flags)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            if (mor.EditMode == EditMode.Select)
            {
                ImGui.SetNextWindowBgAlpha(0.35f);
                ImGui.SetNextWindowPos(Vec56x70);
                ImGui.Begin("##CameraMoveControls", window_flags);

                ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);

                CameraControlMode ccm = Client.Instance.Frontend.Renderer.MapRenderer.CameraControlMode;
                if (ccm == CameraControlMode.Standard)
                {
                    ImGui.PushStyleColor(ImGuiCol.Border, (Vector4)Color.RoyalBlue);
                }

                if (ImGui.ImageButton("btnSelectCameraStd", this.Select, Vec32x32, Vector2.Zero, Vector2.One, Vector4.Zero))
                {
                    Client.Instance.Frontend.Renderer.MapRenderer.CameraControlMode = CameraControlMode.Standard;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.camera.standard"));
                }

                if (ccm == CameraControlMode.Standard)
                {
                    ImGui.PopStyleColor();
                }

                ImGui.SameLine();

                if (ccm == CameraControlMode.Move)
                {
                    ImGui.PushStyleColor(ImGuiCol.Border, (Vector4)Color.RoyalBlue);
                }

                if (ImGui.ImageButton("btnSelectCameraMove", this.CameraMove, Vec32x32, Vector2.Zero, Vector2.One, Vector4.Zero))
                {
                    Client.Instance.Frontend.Renderer.MapRenderer.CameraControlMode = CameraControlMode.Move;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.camera.move"));
                }

                if (ccm == CameraControlMode.Move)
                {
                    ImGui.PopStyleColor();
                }

                ImGui.SameLine();

                if (ccm == CameraControlMode.Rotate)
                {
                    ImGui.PushStyleColor(ImGuiCol.Border, (Vector4)Color.RoyalBlue);
                }

                if (ImGui.ImageButton("btnSelectCameraRotate", this.CameraRotate, Vec32x32, Vector2.Zero, Vector2.One, Vector4.Zero))
                {
                    Client.Instance.Frontend.Renderer.MapRenderer.CameraControlMode = CameraControlMode.Rotate;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.camera.rotate"));
                }

                if (ccm == CameraControlMode.Rotate)
                {
                    ImGui.PopStyleColor();
                }

                ImGui.PopStyleColor();
                ImGui.PopStyleVar();
                ImGui.End();
            }
        }

        private unsafe void RenderDrawControls(MapObjectRenderer mor, SimpleLanguage lang, ImGuiWindowFlags window_flags)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            if (mor.EditMode == EditMode.Draw)
            {
                ImGui.SetNextWindowBgAlpha(0.35f);
                ImGui.SetNextWindowPos(Vec56x70);
                ImGui.Begin("##DrawControls", window_flags);

                ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);

                bool selected = Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.IsDrawing;
                if (ImImageButton("##DrawModeBtnDraw", this.FOWModeBrush, Vec32x32, selected))
                {
                    Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.IsDrawing = true;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.draw.brush.tt"));
                }

                ImGui.SameLine();

                if (ImImageButton("##DrawModeBtnErase", this.MeasureModeErase, Vec32x32, !selected))
                {
                    Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.IsDrawing = false;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.draw.erase.tt"));
                }

                float radius = Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.CurrentRadius;
                if (ImGui.SliderFloat("##DrawModeRadius", ref radius, 0.1f, 10f))
                {
                    Client.Instance.Frontend.Renderer.MapRenderer.DrawingRenderer.CurrentRadius = MathF.Max(0.025f, radius);
                }

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
                    ImGui.PushItemWidth(200);
                    if (ImGui.ColorPicker3("##DrawColorPickerD", ref cclr, ImGuiColorEditFlags.PickerHueWheel | ImGuiColorEditFlags.NoInputs))
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

                ImGui.PopStyleColor();
                ImGui.PopStyleVar();
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
                ImGui.SetNextWindowBgAlpha(0.35f);
                ImGui.SetNextWindowPos(Vec56x70);
                ImGui.Begin("##FXControls", window_flags);

                ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);

                ImDrawListPtr drawList = ImGui.GetWindowDrawList();
                var imScreenPos = ImGui.GetCursorScreenPos();
                var rectEnd = imScreenPos + new Vector2(320, 24);
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
                    if (Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestPreview(this._fxToEmitParticleSystemID, out AssetPreview ap) == AssetStatus.Return && ap != null)
                    {
                        GL.Texture tex = ap.GetGLTexture();
                        if (tex != null)
                        {
                            drawList.AddImage(tex, imScreenPos + new Vector2(20, 4), imScreenPos + new Vector2(36, 20));
                            mdlTxtOffset += 20;
                        }
                    }
                }

                if (Guid.Equals(Guid.Empty, this._fxToEmitParticleSystemID))
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
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 28);
                if (mouseOver)
                {
                    state.movingParticleAssetOverFXRecepticle = true;
                    ImGui.SetTooltip(lang.Translate("ui.fx.particle.tt"));
                }

                int iFxToEmit = this._fxNumToEmit;
                if (ImGui.DragInt(lang.Translate("ui.fx.num_emit") + "###NumParticlesToEmit", ref iFxToEmit))
                {
                    this._fxNumToEmit = iFxToEmit;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.fx.num_emit.tt"));
                }

                ImGui.PopStyleColor();
                ImGui.PopStyleVar();
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
                ImGui.SetNextWindowBgAlpha(0.35f);
                ImGui.SetNextWindowPos(Vec56x70);
                ImGui.Begin("##Shadow2DControls", window_flags);

                ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);

                Shadow2DControlMode currentMode = renderer.ControlMode;
                for (int i = 0; i < 7; ++i)
                {
                    bool selected = (Shadow2DControlMode)i == currentMode;
                    if (ImImageButton("##Shadow2DModeBtn_" + i, Client.Instance.Frontend.Renderer.GuiRenderer.Shadow2DControlModeTextures[i], Vec32x32, selected))
                    {
                        renderer.ControlMode = (Shadow2DControlMode)i;
                    }

                    ImGui.SameLine();

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.shadow2d.mode_" + Enum.GetName((Shadow2DControlMode)i).ToLower() + ".tt"));
                    }
                }

                ImGui.PopStyleColor();
                ImGui.PopStyleVar();
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
