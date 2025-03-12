namespace VTT.Render.Gui
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using System;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using VTT.Asset;
    using VTT.GLFW;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public class ParticleEditorRenderer
    {
        internal bool popupState = false;

        private GradientPoint<Vector4> _editedColorPt;
        private Gradient<Vector4> _editedColor;
        private bool editColorPopup = false;

        public void Render(Guid particleSystemId, AssetRef draggedRef, GuiState state)
        {
            editColorPopup = false;
            SimpleLanguage lang = Client.Instance.Lang;
            if (this.popupState)
            {
                if (ImGui.Begin(lang.Translate("ui.particle.title") + "###Edit Particle System"))
                {
                    ParticleRenderer pr = Client.Instance.Frontend.Renderer.ParticleRenderer;
                    Vector2 winSize = ImGui.GetWindowSize();
                    Vector2 texSize = winSize - new Vector2(340, 40);
                    if (texSize.X != texSize.Y)
                    {
                        if (texSize.X < texSize.Y)
                        {
                            texSize.Y = texSize.X;
                        }
                        else
                        {
                            texSize.X = texSize.Y;
                        }
                    }

                    bool doRender = false;
                    if (Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(particleSystemId, AssetType.ParticleSystem, out Asset a) == AssetStatus.Return && a.ParticleSystem != null)
                    {
                        doRender = true;
                        if (pr.CurrentlyEditedSystem == null)
                        {
                            pr.CurrentlyEditedSystem = a.ParticleSystem.Copy();
                            pr.CurrentlyEditedSystemInstance = new ParticleSystemInstance(pr.CurrentlyEditedSystem, null);
                        }
                    }

                    bool npLbm = false;
                    bool npRmb = false;

                    ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
                    ImGui.Image(pr.RenderTexture, texSize, new Vector2(0, 1), new Vector2(1, 0));
                    ImGui.PopStyleVar();
                    ImGui.SetCursorPos(new Vector2(winSize.X - 328, 28));
                    bool focused = ImGui.IsWindowFocused();
                    if (ImGui.BeginChild("##ParamsEditor", new Vector2(320, winSize.Y - 36), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar))
                    {
                        if (doRender)
                        {
                            if (Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Left) && !_lmbDown && ImGui.IsWindowFocused())
                            {
                                _lmbDown = true;
                                npLbm = true;
                            }

                            if (Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Right) && !_rmbDown && ImGui.IsWindowFocused())
                            {
                                _rmbDown = true;
                                npRmb = true;
                            }

                            ImGui.Text(lang.Translate("ui.particle.emission_type"));
                            string[] emissionTypes = {
                        lang.Translate("ui.particle.emission.point"),
                        lang.Translate("ui.particle.emission.sphere"),
                        lang.Translate("ui.particle.emission.sphere_surface"),
                        lang.Translate("ui.particle.emission.cube"),
                        lang.Translate("ui.particle.emission.cube_surface"),
                        lang.Translate("ui.particle.emission.square_volume"),
                        lang.Translate("ui.particle.emission.square_boundary"),
                        lang.Translate("ui.particle.emission.circle_volume"),
                        lang.Translate("ui.particle.emission.circle_boundary"),
                        lang.Translate("ui.particle.emission.volume"),
                        lang.Translate("ui.particle.emission.surface"),
                        lang.Translate("ui.particle.emission.mask"),
                    };

                            int cEmissionType = (int)pr.CurrentlyEditedSystem.EmissionType;
                            if (ImGui.Combo("##ParticleEmissionType", ref cEmissionType, emissionTypes, emissionTypes.Length))
                            {
                                pr.CurrentlyEditedSystem.EmissionType = (ParticleSystem.EmissionMode)cEmissionType;
                            }

                            if (cEmissionType is 1 or 2)
                            {
                                ImGui.Text(lang.Translate("ui.particle.emission_radius"));
                                float cRad = pr.CurrentlyEditedSystem.EmissionRadius;
                                if (ImGui.DragFloat("##ParticleRadius", ref cRad, 0.01f))
                                {
                                    pr.CurrentlyEditedSystem.EmissionRadius = cRad;
                                }

                                ImGui.Text(lang.Translate("ui.particle.emission_volume_sphere"));
                                Vector3 v3 = pr.CurrentlyEditedSystem.EmissionVolume;
                                if (ImGui.DragFloat3("##ParticleVolume", ref v3, 0.01f))
                                {
                                    pr.CurrentlyEditedSystem.EmissionVolume = v3;
                                }
                            }

                            if (cEmissionType is (>= 3 and <= 8) or 11)
                            {
                                ImGui.Text(lang.Translate("ui.particle.emission_volume"));
                                Vector3 v3 = pr.CurrentlyEditedSystem.EmissionVolume;
                                if (ImGui.DragFloat3("##ParticleVolume", ref v3, 0.01f))
                                {
                                    pr.CurrentlyEditedSystem.EmissionVolume = v3;
                                }
                            }

                            if (cEmissionType == 11)
                            {
                                ImAssetRecepticle("ui.properties.custom_mask.tt", draggedRef, pr.CurrentlyEditedSystem.MaskID, state, pr.CurrentlyEditedSystem, 2);
                            }

                            ImGui.Text(lang.Translate("ui.particle.emission_chance"));
                            float eChance = pr.CurrentlyEditedSystem.EmissionChance;
                            if (ImGui.SliderFloat("##ParticleEmissionChance", ref eChance, 0, 1))
                            {
                                pr.CurrentlyEditedSystem.EmissionChance = eChance;
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.particle.emission_chance.tt"));
                            }

                            ImGui.Text(lang.Translate("ui.particle.emission_amount"));
                            int min = pr.CurrentlyEditedSystem.EmissionAmount.Min;
                            int max = pr.CurrentlyEditedSystem.EmissionAmount.Max;
                            if (ImDragInt2("##EmissionNum", ref min, ref max, 0, 128))
                            {
                                pr.CurrentlyEditedSystem.EmissionAmount.Min = min;
                                pr.CurrentlyEditedSystem.EmissionAmount.Max = max;
                                pr.CurrentlyEditedSystemInstance.Resize();
                            }

                            ImGui.Text(lang.Translate("ui.particle.emission_cooldown"));
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.particle.emission_cooldown.tt"));
                            }

                            min = pr.CurrentlyEditedSystem.EmissionCooldown.Min;
                            max = pr.CurrentlyEditedSystem.EmissionCooldown.Max;
                            if (ImDragInt2("##EmissionCD", ref min, ref max, 0, ushort.MaxValue))
                            {
                                pr.CurrentlyEditedSystem.EmissionCooldown.Min = min;
                                pr.CurrentlyEditedSystem.EmissionCooldown.Max = max;
                            }

                            ImGui.Text(lang.Translate("ui.particle.cluster_emission"));
                            ImGui.SameLine();
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.particle.cluster_emission.tt"));
                            }

                            bool pClusters = pr.CurrentlyEditedSystem.ClusterEmission;
                            if (ImGui.Checkbox("##DoClusters", ref pClusters))
                            {
                                pr.CurrentlyEditedSystem.ClusterEmission = pClusters;
                            }

                            ImGui.Text(lang.Translate("ui.particle.lifetime"));
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.particle.lifetime.tt"));
                            }

                            min = pr.CurrentlyEditedSystem.Lifetime.Min;
                            max = pr.CurrentlyEditedSystem.Lifetime.Max;
                            if (ImDragInt2("##Lifetime", ref min, ref max, 0, 600))
                            {
                                pr.CurrentlyEditedSystem.Lifetime.Min = min;
                                pr.CurrentlyEditedSystem.Lifetime.Max = max;
                                pr.CurrentlyEditedSystemInstance.Resize();
                            }

                            ImGui.Text(lang.Translate("ui.particle.scale_mod"));
                            float minF = pr.CurrentlyEditedSystem.ScaleVariation.Min;
                            float maxF = pr.CurrentlyEditedSystem.ScaleVariation.Max;
                            if (ImDragSingle2("##ScaleVariation", ref minF, ref maxF, 0, 100))
                            {
                                pr.CurrentlyEditedSystem.ScaleVariation.Min = minF;
                                pr.CurrentlyEditedSystem.ScaleVariation.Max = maxF;
                            }

                            ImGui.Text(lang.Translate("ui.particle.max"));
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.particle.max.tt"));
                            }

                            int pMax = pr.CurrentlyEditedSystem.MaxParticles;
                            if (ImGui.DragInt("##MaxParticles", ref pMax, 1, 0, 20000))
                            {
                                pr.CurrentlyEditedSystem.MaxParticles = Math.Clamp(pMax, 0, 20000);
                                pr.CurrentlyEditedSystemInstance.Resize();
                            }

                            ImGui.Text(lang.Translate("ui.particle.billboard"));
                            ImGui.SameLine();
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.particle.billboard.tt"));
                            }

                            bool pBillboard = pr.CurrentlyEditedSystem.DoBillboard;
                            if (ImGui.Checkbox("##DoBillboard", ref pBillboard))
                            {
                                pr.CurrentlyEditedSystem.DoBillboard = pBillboard;
                            }

                            ImGui.Text(lang.Translate("ui.particle.pixel_fow"));
                            ImGui.SameLine();
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.particle.pixel_fow.tt"));
                            }

                            bool pDoFow = pr.CurrentlyEditedSystem.DoFow;
                            if (ImGui.Checkbox("##DoFow", ref pDoFow))
                            {
                                pr.CurrentlyEditedSystem.DoFow = pDoFow;
                            }

                            ImGui.Text(lang.Translate("ui.particle.velocity"));
                            Vector3 vVec = pr.CurrentlyEditedSystem.InitialVelocity.Min;
                            if (ImGui.DragFloat3("##VelMin", ref vVec, 0.01f))
                            {
                                pr.CurrentlyEditedSystem.InitialVelocity.Min = vVec;
                            }

                            ImGui.SameLine();
                            ImGui.Text(lang.Translate("ui.generic.min"));
                            vVec = pr.CurrentlyEditedSystem.InitialVelocity.Max;
                            if (ImGui.DragFloat3("##VelMax", ref vVec, 0.01f))
                            {
                                pr.CurrentlyEditedSystem.InitialVelocity.Max = vVec;
                            }

                            ImGui.SameLine();
                            ImGui.Text(lang.Translate("ui.generic.max"));
                            ImGui.Text(lang.Translate("ui.particle.velocity_angle"));
                            float fAng = pr.CurrentlyEditedSystem.InitialVelocityRandomAngle;
                            if (ImGui.SliderAngle("##VelAngle", ref fAng))
                            {
                                pr.CurrentlyEditedSystem.InitialVelocityRandomAngle = fAng;
                            }

                            ImGui.Text(lang.Translate("ui.particle.gravity"));
                            vVec = pr.CurrentlyEditedSystem.Gravity;
                            if (ImGui.DragFloat3("##Grav", ref vVec, 0.01f))
                            {
                                pr.CurrentlyEditedSystem.Gravity = vVec;
                            }

                            ImGui.Text(lang.Translate("ui.particle.velocity_dampen"));
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.particle.velocity_dampen.tt"));
                            }

                            float fVDamp = pr.CurrentlyEditedSystem.VelocityDampenFactor;
                            if (ImGui.DragFloat("##VelDamp", ref fVDamp, 0.01f))
                            {
                                pr.CurrentlyEditedSystem.VelocityDampenFactor = fVDamp;
                            }

                            ImGui.Text(lang.Translate("ui.particle.color"));
                            ImGradient(pr.CurrentlyEditedSystem.ColorOverLifetime, ref npLbm, ref npRmb);

                            ImGui.Text(lang.Translate("ui.particle.scale"));
                            ImGradientSingle(pr.CurrentlyEditedSystem.ScaleOverLifetime, ref npLbm, ref npRmb);

                            float f = _editedValueSingle?.Color ?? 0f;
                            if (ImGui.DragFloat("##DragFloatEditedValue", ref f, 0.01f))
                            {
                                if (_editedValueSingle.HasValue)
                                {
                                    pr.CurrentlyEditedSystem.ScaleOverLifetime.Remove(_editedValueSingle.Value.Key);
                                    _editedValueSingle = new GradientPoint<float>(_editedValueSingle.Value.Key, f);
                                    pr.CurrentlyEditedSystem.ScaleOverLifetime.Add(_editedValueSingle.Value);
                                }
                            }

                            ImAssetRecepticle("ui.popup.model.tt", draggedRef, pr.CurrentlyEditedSystem.AssetID, state, pr.CurrentlyEditedSystem, 1);
                            ImAssetRecepticle("ui.properties.custom_shader.tt", draggedRef, pr.CurrentlyEditedSystem.CustomShaderID, state, pr.CurrentlyEditedSystem, 0);
                            if (ImGui.Button(lang.Translate("ui.properties.custom_shader.delete")))
                            {
                                pr.CurrentlyEditedSystem.CustomShaderID = Guid.Empty;
                            }

                            ImGui.Spacing();
                            bool isSS = pr.CurrentlyEditedSystem.IsSpriteSheet;
                            if (ImGui.Checkbox(lang.Translate("ui.particle.is_sprite_sheet") + "###IsSpriteSheet", ref isSS))
                            {
                                pr.CurrentlyEditedSystem.IsSpriteSheet = isSS;
                                if (isSS)
                                {
                                    pr.CurrentlyEditedSystem.SpriteData.Init();
                                }
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.particle.is_sprite_sheet.tt"));
                            }

                            if (isSS)
                            {
                                if (ImGui.TreeNode(lang.Translate("ui.particle.sprite_sheet_data") + "###SpriteSheetData"))
                                {
                                    int iSSNC = pr.CurrentlyEditedSystem.SpriteData.NumColumns;
                                    int iSSNR = pr.CurrentlyEditedSystem.SpriteData.NumRows;
                                    if (ImGui.InputInt(lang.Translate("ui.particle.sprite_size_num_columns") + "###SheetColumns", ref iSSNC))
                                    {
                                        pr.CurrentlyEditedSystem.SpriteData.NumColumns = iSSNC;
                                        pr.CurrentlyEditedSystem.SpriteData.NumSprites = Math.Min(pr.CurrentlyEditedSystem.SpriteData.NumSprites, iSSNR * iSSNC);
                                        pr.CurrentlyEditedSystem.SpriteData.ReallocateSelectionWeights();
                                    }

                                    if (ImGui.InputInt(lang.Translate("ui.particle.sprite_size_num_rows") + "###SheetRows", ref iSSNR))
                                    {
                                        pr.CurrentlyEditedSystem.SpriteData.NumRows = iSSNR;
                                        pr.CurrentlyEditedSystem.SpriteData.NumSprites = Math.Min(pr.CurrentlyEditedSystem.SpriteData.NumSprites, iSSNR * iSSNC);
                                        pr.CurrentlyEditedSystem.SpriteData.ReallocateSelectionWeights();
                                    }

                                    int iSSNS = pr.CurrentlyEditedSystem.SpriteData.NumSprites;
                                    ImGui.Text(lang.Translate("ui.particle.sprite_size_num_sprites"));
                                    if (ImGui.SliderInt("###NumSprites", ref iSSNS, 0, iSSNR * iSSNC))
                                    {
                                        pr.CurrentlyEditedSystem.SpriteData.NumSprites = iSSNS;
                                        pr.CurrentlyEditedSystem.SpriteData.ReallocateSelectionWeights();
                                    }

                                    int cM = (int)pr.CurrentlyEditedSystem.SpriteData.Selection;
                                    string[] modeNames = new string[] { lang.Translate("ui.particle.sprite_selection_mode.progressive"), lang.Translate("ui.particle.sprite_selection_mode.regressive"), lang.Translate("ui.particle.sprite_selection_mode.random"), lang.Translate("ui.particle.sprite_selection_mode.first") };
                                    ImGui.Text(lang.Translate("ui.particle.sprite_selection_mode"));
                                    if (ImGui.Combo("##SpriteSelectionMode", ref cM, modeNames, 4))
                                    {
                                        pr.CurrentlyEditedSystem.SpriteData.Selection = (ParticleSystem.SpriteSheetData.SelectionMode)cM;
                                    }

                                    if (pr.CurrentlyEditedSystem.SpriteData.Selection == ParticleSystem.SpriteSheetData.SelectionMode.Random)
                                    {
                                        ImGui.Text(lang.Translate("ui.particle.sprite_selection_weights"));
                                        if (ImGui.IsItemHovered())
                                        {
                                            ImGui.SetTooltip(lang.Translate("ui.particle.sprite_selection_weights.tt"));
                                        }

                                        for (int i = 0; i < pr.CurrentlyEditedSystem.SpriteData.NumSprites; ++i)
                                        {
                                            int vF = pr.CurrentlyEditedSystem.SpriteData.SelectionWeights[i];
                                            if (ImGui.DragInt(lang.Translate("ui.particle.sprite_selection_weight_indexed", i) + "###SpriteSelectionWeight_" + i, ref vF))
                                            {
                                                pr.CurrentlyEditedSystem.SpriteData.SelectionWeights[i] = vF;
                                                pr.CurrentlyEditedSystem.SpriteData.SelectionWeightsList[i] = new WeightedItem<int>(i, vF);
                                            }
                                        }
                                    }

                                    bool issA = pr.CurrentlyEditedSystem.SpriteSheetIsAnimation;
                                    if (ImGui.Checkbox(lang.Translate("ui.particle.sprite_sheet_is_animation") + "###SpriteSheetIsAnimation", ref issA))
                                    {
                                        pr.CurrentlyEditedSystem.SpriteSheetIsAnimation = issA;
                                    }

                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(lang.Translate("ui.particle.sprite_sheet_is_animation.tt"));
                                    }

                                    ImGui.TreePop();
                                }
                            }
                        }

                        ImGui.Spacing();
                        bool bc = ImGui.Button(lang.Translate("ui.generic.cancel"));
                        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 24);
                        bool bo = ImGui.Button(lang.Translate("ui.generic.ok"));
                        if (bo)
                        {
                            AssetRef aRef = Client.Instance.AssetManager.FindRefForAsset(a);
                            using MemoryStream ms = new MemoryStream();
                            using BinaryWriter bw = new BinaryWriter(ms);
                            pr.CurrentlyEditedSystem.WriteV2(bw);
                            byte[] abin = a.ToBinary(ms.ToArray());
                            pr.RenderTexture.Bind();
                            using Image<Rgba32> img = pr.RenderTexture.GetImage<Rgba32>();
                            img.Mutate(x => x.Resize(256, 256));
                            img.Mutate(x => x.Flip(FlipMode.Vertical));
                            using MemoryStream ms2 = new MemoryStream();
                            img.SaveAsPng(ms2);
                            byte[] pbin = ms2.ToArray();
                            if (aRef != null && aRef.Meta.Version == 1)
                            {
                                aRef.Meta.Version = 2;
                                new PacketChangeAssetMetadata() { AssetID = a.ID, RefID = aRef.AssetID, NewMeta = aRef.Meta }.Send();
                            }

                            new PacketAssetUpdate() { AssetID = a.ID, NewBinary = abin, NewPreviewBinary = pbin }.Send();
                        }

                        if (bo || bc)
                        {
                            popupState = false;
                            pr.CurrentlyEditedSystem = null;
                            pr.CurrentlyEditedSystemInstance.Free();
                            pr.CurrentlyEditedSystemInstance = null;
                        }
                    }

                    ImGui.EndChild();
                }

                ImGui.End();
            }

            if (!Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Left) && _lmbDown)
            {
                _lmbDown = false;
                _imDraggedGradientKey = 0;
                _lastClicked = null;
                _lastClickedSingle = null;
            }

            if (!Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Right) && _rmbDown)
            {
                _rmbDown = false;
                _lastClicked = null;
                _lastClickedSingle = null;
            }

            if (this.editColorPopup)
            {
                ImGui.OpenPopup("###Edit Gradient Color");
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.generic.color") + "###Edit Gradient Color"))
            {
                Vector4 c = this._editedColorPt.Color;
                if (ImGui.ColorPicker4(lang.Translate("ui.generic.color") + "###GradColor", ref c))
                {
                    this._editedColorPt = new GradientPoint<Vector4>(this._editedColorPt.Key, c);
                }

                bool bc = ImGui.Button(lang.Translate("ui.generic.cancel"));
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                bool bo = ImGui.Button(lang.Translate("ui.generic.ok"));

                if (bo || bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                if (bo)
                {
                    this._editedColor.Remove(this._editedColorPt.Key);
                    this._editedColor.Add(this._editedColorPt);
                }

                ImGui.EndPopup();
            }
        }

        private static float _imDraggedGradientKey;
        private static bool _lmbDown;
        private static bool _rmbDown;
        private static GradientPoint<Vector4>? _lastClicked;
        private static GradientPoint<float>? _lastClickedSingle;
        private static GradientPoint<float>? _editedValueSingle;

        private bool ImGradient(Gradient<Vector4> grad, ref bool needsProcessLmb, ref bool needsProcessRmb)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            float w = 208;
            Vector2 curWin = ImGui.GetCursorScreenPos() + new Vector2(0, 16);
            drawList.AddRect(
                new Vector2(curWin.X, curWin.Y),
                new Vector2(curWin.X + w, curWin.Y + 24),
                ImGui.GetColorU32(ImGuiCol.Border)
            );

            bool processedRmb = false;

            for (int i = 0; i < grad.InternalList.Count; i++)
            {
                GradientPoint<Vector4> curr = grad.InternalList[i];
                GradientPoint<Vector4> next = grad.InternalList[(i + 1) % grad.InternalList.Count];
                float fS = curr.Key * w;
                float fN = (next.Key < curr.Key ? 1 : next.Key) * w;
                uint clrL = Extensions.FromVec4(curr.Color).Abgr();
                uint clrR = Extensions.FromVec4(next.Color).Abgr();
                drawList.AddRectFilledMultiColor(
                    new Vector2(curWin.X + fS, curWin.Y),
                    new Vector2(curWin.X + fN, curWin.Y + 24),
                    clrL, clrR, clrR, clrL
                );

                bool hoverTri = PointInTriangle(
                    Client.Instance.Frontend.GameHandle.MousePosition,
                    new Vector2(curWin.X + fS, curWin.Y + 24),
                    new Vector2(curWin.X + fS - 4, curWin.Y + 40),
                    new Vector2(curWin.X + fS + 4, curWin.Y + 40)
                );

                if (needsProcessLmb && hoverTri && i != 0 && i != grad.InternalList.Count - 1)
                {
                    needsProcessLmb = false;
                    _lastClicked = curr;
                    _imDraggedGradientKey = curr.Key;
                }

                if (needsProcessRmb && hoverTri && i != 0 && i != grad.InternalList.Count - 1)
                {
                    needsProcessRmb = false;
                    processedRmb = true;
                    _lastClicked = curr;
                }

                drawList.AddTriangleFilled(
                    new Vector2(curWin.X + fS, curWin.Y + 24),
                    new Vector2(curWin.X + fS - 4, curWin.Y + 40),
                    new Vector2(curWin.X + fS + 4, curWin.Y + 40),
                    hoverTri ? ImGui.GetColorU32(ImGuiCol.ButtonHovered) : ImGui.GetColorU32(ImGuiCol.Button)
                );

                drawList.AddTriangle(
                    new Vector2(curWin.X + fS, curWin.Y + 24),
                    new Vector2(curWin.X + fS - 4, curWin.Y + 40),
                    new Vector2(curWin.X + fS + 4, curWin.Y + 40),
                    ImGui.GetColorU32(ImGuiCol.Border)
                );

                drawList.AddRectFilled(
                    new Vector2(curWin.X + fS - 4, curWin.Y - 10),
                    new Vector2(curWin.X + fS + 4, curWin.Y - 2),
                    clrL
                );

                bool hoverQuad = ImGui.IsMouseHoveringRect(new Vector2(curWin.X + fS - 4, curWin.Y - 10), new Vector2(curWin.X + fS + 4, curWin.Y - 2));
                drawList.AddRect(
                    new Vector2(curWin.X + fS - 4, curWin.Y - 10),
                    new Vector2(curWin.X + fS + 4, curWin.Y - 2),
                    hoverQuad ? ImGui.GetColorU32(ImGuiCol.ButtonHovered) : ImGui.GetColorU32(ImGuiCol.Border)
                );

                if (needsProcessLmb && hoverQuad)
                {
                    needsProcessLmb = false;
                    this._editedColorPt = curr;
                    editColorPopup = true;
                    this._editedColor = grad;
                }
            }

            if (_lmbDown && _lastClicked.HasValue)
            {
                float mV = Math.Clamp((Client.Instance.Frontend.MouseX - curWin.X) / w, 0f, 1f);
                if (MathF.Abs(mV - _imDraggedGradientKey) > float.Epsilon)
                {
                    grad.InternalList.Remove(_lastClicked.Value);
                    GradientPoint<Vector4> nPt = new GradientPoint<Vector4>(mV, _lastClicked.Value.Color);
                    grad.Add(nPt);
                    _lastClicked = nPt;
                    _imDraggedGradientKey = mV;
                }
            }

            if (_lmbDown && needsProcessLmb)
            {
                if (ImGui.IsMouseHoveringRect(new Vector2(curWin.X, curWin.Y), new Vector2(curWin.X + w, curWin.Y + 24)))
                {
                    float mV = Math.Clamp((ImGui.GetMousePos().X - curWin.X) / w, 0f, 1f);
                    Vector4 v = grad.Interpolate(mV, GradientInterpolators.LerpVec4);
                    grad.Add(mV, v);
                    needsProcessLmb = false;
                    _lastClicked = null;
                }
            }

            if (_rmbDown && processedRmb && _lastClicked.HasValue)
            {
                grad.Remove(_lastClicked.Value.Key);
            }

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 64);

            return false;
        }

        private bool ImGradientSingle(Gradient<float> grad, ref bool needsProcessLmb, ref bool needsProcessRmb)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            float w = 208;
            Vector2 curWin = ImGui.GetCursorScreenPos() + new Vector2(0, 16);
            drawList.AddRect(
                new Vector2(curWin.X, curWin.Y),
                new Vector2(curWin.X + w, curWin.Y + 24),
                ImGui.GetColorU32(ImGuiCol.Border)
            );

            bool processedRmb = false;
            float maxVal = grad.Values.Max();

            for (int i = 0; i < grad.InternalList.Count; i++)
            {
                GradientPoint<float> curr = grad.InternalList[i];
                GradientPoint<float> next = grad.InternalList[(i + 1) % grad.InternalList.Count];
                float fS = curr.Key * w;
                float fN = (next.Key < curr.Key ? 1 : next.Key) * w;
                float fYS = curr.Color / maxVal;
                float fYN = next.Color / maxVal;
                drawList.AddLine(
                    new Vector2(curWin.X + fS, curWin.Y + 24 - (fYS * 24)),
                    new Vector2(curWin.X + fN, curWin.Y + 24 - (fYN * 24)),
                    ImGui.GetColorU32(ImGuiCol.PlotLines)
                );

                bool hoverTri = PointInTriangle(
                    Client.Instance.Frontend.GameHandle.MousePosition,
                    new Vector2(curWin.X + fS, curWin.Y + 24),
                    new Vector2(curWin.X + fS - 4, curWin.Y + 40),
                    new Vector2(curWin.X + fS + 4, curWin.Y + 40)
                );

                if (needsProcessLmb && hoverTri && i != 0 && i != grad.InternalList.Count - 1)
                {
                    needsProcessLmb = false;
                    _lastClickedSingle = curr;
                    _imDraggedGradientKey = curr.Key;
                    _editedValueSingle = curr;
                }

                if (needsProcessRmb && hoverTri && i != 0 && i != grad.InternalList.Count - 1)
                {
                    needsProcessRmb = false;
                    processedRmb = true;
                    _lastClickedSingle = curr;
                    _editedValueSingle = null;
                }

                drawList.AddTriangleFilled(
                    new Vector2(curWin.X + fS, curWin.Y + 24),
                    new Vector2(curWin.X + fS - 4, curWin.Y + 40),
                    new Vector2(curWin.X + fS + 4, curWin.Y + 40),
                    hoverTri ? ImGui.GetColorU32(ImGuiCol.ButtonHovered) : ImGui.GetColorU32(ImGuiCol.Button)
                );

                drawList.AddTriangle(
                    new Vector2(curWin.X + fS, curWin.Y + 24),
                    new Vector2(curWin.X + fS - 4, curWin.Y + 40),
                    new Vector2(curWin.X + fS + 4, curWin.Y + 40),
                    ImGui.GetColorU32(ImGuiCol.Border)
                );

                drawList.AddRectFilled(
                    new Vector2(curWin.X + fS - 4, curWin.Y - 10),
                    new Vector2(curWin.X + fS + 4, curWin.Y - 2),
                    Extensions.FromVec4(Extensions.FromArgb(ImGui.GetColorU32(ImGuiCol.PlotHistogram)).Vec4() * fYS).Abgr()
                );

                bool hoverQuad = ImGui.IsMouseHoveringRect(new Vector2(curWin.X + fS - 4, curWin.Y - 10), new Vector2(curWin.X + fS + 4, curWin.Y - 2));
                drawList.AddRect(
                    new Vector2(curWin.X + fS - 4, curWin.Y - 10),
                    new Vector2(curWin.X + fS + 4, curWin.Y - 2),
                    hoverQuad ? ImGui.GetColorU32(ImGuiCol.ButtonHovered) : ImGui.GetColorU32(ImGuiCol.Border)
                );

                if (needsProcessLmb && hoverQuad)
                {
                    needsProcessLmb = false;
                    _editedValueSingle = curr;
                }
            }

            if (_lmbDown && _lastClickedSingle.HasValue)
            {
                float mV = Math.Clamp((Client.Instance.Frontend.MouseX - curWin.X) / w, 0f, 1f);
                if (MathF.Abs(mV - _imDraggedGradientKey) > float.Epsilon)
                {
                    grad.InternalList.Remove(_lastClickedSingle.Value);
                    GradientPoint<float> nPt = new GradientPoint<float>(mV, _lastClickedSingle.Value.Color);
                    grad.Add(nPt);
                    _lastClickedSingle = nPt;
                    _imDraggedGradientKey = mV;
                    _editedValueSingle = nPt;
                }
            }

            if (_lmbDown && needsProcessLmb)
            {
                if (ImGui.IsMouseHoveringRect(new Vector2(curWin.X, curWin.Y), new Vector2(curWin.X + w, curWin.Y + 24)))
                {
                    float mV = Math.Clamp((Client.Instance.Frontend.MouseX - curWin.X) / w, 0f, 1f);
                    float v = grad.Interpolate(mV, GradientInterpolators.Lerp);
                    GradientPoint<float> pt = new GradientPoint<float>(mV, v);
                    grad.Add(pt);
                    _editedValueSingle = pt;
                    needsProcessLmb = false;
                }
            }

            if (_rmbDown && processedRmb && _lastClickedSingle.HasValue)
            {
                grad.Remove(_lastClickedSingle.Value.Key);
            }

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 64);
            return false;
        }

        private void ImAssetRecepticle(string text, AssetRef draggedRef, Guid aId, GuiState state, ParticleSystem ps, int type)
        {
            float w = ImGui.GetContentRegionAvail().X;
            if (ImGui.BeginChild("asset_recepticle_" + text, new Vector2(w, 24), ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImDrawListPtr drawList = ImGui.GetWindowDrawList();
                Vector2 imScreenPos = ImGui.GetCursorScreenPos();
                Vector2 contentSize = Vector2.Min(new Vector2(w, 24), ImGui.GetContentRegionAvail());
                Vector2 rectEnd = imScreenPos + contentSize;
                bool mouseOver = ImGui.IsMouseHoveringRect(imScreenPos, rectEnd);
                bool acceptShader = type == 0 && draggedRef != null && draggedRef.Type is AssetType.Shader or AssetType.GlslFragmentShader;
                bool acceptModel = type == 1 && draggedRef != null && (draggedRef.Type == AssetType.Model || draggedRef.Type == AssetType.Texture);
                bool acceptMask = type == 2 && draggedRef != null && draggedRef.Type == AssetType.Texture;
                uint bClr = mouseOver ? draggedRef != null && (acceptShader || acceptModel || acceptMask) ? ImGui.GetColorU32(ImGuiCol.HeaderHovered) : ImGui.GetColorU32(ImGuiCol.ButtonHovered) : ImGui.GetColorU32(ImGuiCol.Border);
                drawList.AddRect(imScreenPos, rectEnd, bClr);
                drawList.AddImage(type switch
                {
                    0 => Client.Instance.Frontend.Renderer.GuiRenderer.AssetShaderIcon,
                    1 => Client.Instance.Frontend.Renderer.GuiRenderer.AssetModelIcon,
                    _ => Client.Instance.Frontend.Renderer.GuiRenderer.AssetImageIcon
                }, imScreenPos + new Vector2(4, 4), imScreenPos + new Vector2(20, 20));
                string mdlTxt = "";
                int mdlTxtOffset = 0;
                if (Client.Instance.AssetManager.Refs.ContainsKey(aId))
                {
                    AssetRef aRef = Client.Instance.AssetManager.Refs[aId];
                    mdlTxt += aRef.Name;
                    if (Client.Instance.AssetManager.ClientAssetLibrary.Previews.Get(aId, AssetType.Texture, out AssetPreview ap) == AssetStatus.Return && ap != null)
                    {
                        GL.Texture tex = ap.GetGLTexture();
                        if (tex != null)
                        {
                            drawList.AddImage(tex, imScreenPos + new Vector2(20, 4), imScreenPos + new Vector2(36, 20));
                            mdlTxtOffset += 20;
                        }
                    }
                }

                mdlTxt += " (" + aId.ToString() + ")\0";
                drawList.AddText(imScreenPos + new Vector2(20 + mdlTxtOffset, 4), ImGui.GetColorU32(ImGuiCol.Text), mdlTxt);
                ImGui.Dummy(new Vector2(w, 28));
                if (mouseOver && draggedRef != null && acceptModel)
                {
                    state.particleModelHovered = ps;
                }

                if (mouseOver && draggedRef != null && acceptShader)
                {
                    state.particleShaderHovered = ps;
                }

                if (mouseOver && draggedRef != null && acceptMask)
                {
                    state.particleMaskHovered = ps;
                }

                if (mouseOver)
                {
                    ImGui.SetTooltip(Client.Instance.Lang.Translate(text));
                }
            }

            ImGui.EndChild();
        }

        private static bool ImDragInt2(string label, ref int min, ref int max, int mmin, int mmax)
        {
            ImGui.PushItemWidth(86);
            bool b0 = ImGui.DragInt(label + "_min", ref min, 1, mmin, mmax);
            ImGui.SameLine();
            ImGui.Text(" - ");
            ImGui.SameLine();
            bool b1 = ImGui.DragInt(label + "_max", ref max, 1, mmin, mmax);
            ImGui.PopItemWidth();
            return b0 || b1;
        }

        private static bool ImDragSingle2(string label, ref float min, ref float max, float mmin, float mmax)
        {
            ImGui.PushItemWidth(86);
            bool b0 = ImGui.DragFloat(label + "_min", ref min, 0.01f, mmin, mmax);
            ImGui.SameLine();
            ImGui.Text(" - ");
            ImGui.SameLine();
            bool b1 = ImGui.DragFloat(label + "_max", ref max, 0.01f, mmin, mmax);
            ImGui.PopItemWidth();
            return b0 || b1;
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) => ((p1.X - p3.X) * (p2.Y - p3.Y)) - ((p2.X - p3.X) * (p1.Y - p3.Y));

        private static bool PointInTriangle(Vector2 pt, Vector2 v1, Vector2 v2, Vector2 v3)
        {
            float d1, d2, d3;
            bool has_neg, has_pos;

            d1 = Sign(pt, v1, v2);
            d2 = Sign(pt, v2, v3);
            d3 = Sign(pt, v3, v1);

            has_neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            has_pos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(has_neg && has_pos);
        }
    }
}
