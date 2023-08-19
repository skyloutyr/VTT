namespace VTT.Render.Gui
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System.Collections.Generic;
    using System;
    using System.Numerics;
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public partial class GuiRenderer
    {
        private unsafe void RenderSidebar(SimpleLanguage lang, ImGuiWindowFlags window_flags, MapObjectRenderer mor)
        {
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            ImGui.SetNextWindowBgAlpha(0.35f);
            ImGui.SetNextWindowPos(Vector2.Zero);
            if (ImGui.Begin("Mode Controls", window_flags))
            {
                for (int i = 0; i < 6; ++i)
                {
                    if (!Client.Instance.IsAdmin && i == (int)EditMode.FOW)
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
                    if (i != 5)
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
                    ImGui.Text("Frame: " + (time * 1000).ToString("0.000") + "ms");
                    ImGui.Text("Cursor: " + Client.Instance.Frontend.MouseX + ", " + Client.Instance.Frontend.MouseY);

                    OpenTK.Mathematics.Vector3? cw = Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld;
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
                    Vector3 cclr = Client.Instance.Frontend.Renderer.RulerRenderer.CurrentColor.Xyz.SystemVector();
                    ImGui.PushItemWidth(200);
                    if (ImGui.ColorPicker3("##RulerColorPickerD", ref cclr, ImGuiColorEditFlags.PickerHueWheel | ImGuiColorEditFlags.NoInputs))
                    {
                        Client.Instance.Frontend.Renderer.RulerRenderer.CurrentColor = new OpenTK.Mathematics.Vector4(cclr.GLVector(), 1.0f);
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
