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

        private LinearGradient<Vector4>.LinearGradientPoint _editedColorPt;
        private LinearGradient<Vector4> _editedColor;
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
                                lang.Translate("ui.particle.emission.bone"),
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
                                ImAssetRecepticle(lang, "ui.properties.custom_mask.tt", pr.CurrentlyEditedSystem.MaskID, state, pr.CurrentlyEditedSystem, 2);
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
                            ImLinearGradient(pr.CurrentlyEditedSystem.ColorOverLifetime, ref npLbm, ref npRmb);

                            ImGui.Text(lang.Translate("ui.particle.scale"));
                            ImLinearGradientSingle(pr.CurrentlyEditedSystem.ScaleOverLifetime, ref npLbm, ref npRmb);

                            float f = linearEditedValueSingle?.Value ?? 0f;
                            if (ImGui.DragFloat("##DragFloatEditedValue", ref f, 0.01f))
                            {
                                if (linearEditedValueSingle != null)
                                {
                                    pr.CurrentlyEditedSystem.ScaleOverLifetime.RemoveInternalPoint(linearEditedValueSingle);
                                    linearEditedValueSingle = pr.CurrentlyEditedSystem.ScaleOverLifetime.Add(linearEditedValueSingle.Key, f);                                    
                                }
                            }

                            ImAssetRecepticle(lang, "ui.popup.model.tt", pr.CurrentlyEditedSystem.AssetID, state, pr.CurrentlyEditedSystem, 1);
                            ImAssetRecepticle(lang, "ui.properties.custom_shader.tt", pr.CurrentlyEditedSystem.CustomShaderID, state, pr.CurrentlyEditedSystem, 0);
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
                linearLastClicked = null;
                linearLastClickedSingle = null;
            }

            if (!Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Right) && _rmbDown)
            {
                _rmbDown = false;
                linearLastClicked = null;
                linearLastClickedSingle = null;
            }

            if (this.editColorPopup)
            {
                ImGui.OpenPopup("###Edit Gradient Color");
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.generic.color") + "###Edit Gradient Color"))
            {
                Vector4 c = this._editedColorPt.Value;
                if (ImGui.ColorPicker4(lang.Translate("ui.generic.color") + "###GradColor", ref c))
                {
                    this._editedColorPt = new (c, this._editedColorPt.Key, 0);
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
                    this._editedColorPt = this._editedColor.Add(this._editedColorPt.Key, c);
                }

                ImGui.EndPopup();
            }
        }

        private static float _imDraggedGradientKey;
        private static bool _lmbDown;
        private static bool _rmbDown;
        private static LinearGradient<Vector4>.LinearGradientPoint linearLastClicked;
        private static LinearGradient<float>.LinearGradientPoint linearLastClickedSingle;
        private static LinearGradient<float>.LinearGradientPoint linearEditedValueSingle;

        private bool ImLinearGradient(LinearGradient<Vector4> grad, ref bool needsProcessLmb, ref bool needsProcessRmb)
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

            for (int i = 0; i < grad.Count; i++)
            {
                LinearGradient<Vector4>.LinearGradientPoint curr = grad.GetInternalPointAt(i);
                LinearGradient<Vector4>.LinearGradientPoint next = grad.GetInternalPointAt((i + 1) % grad.Count);
                float fS = curr.Key * w;
                float fN = (next.Key < curr.Key ? 1 : next.Key) * w;
                uint clrL = curr.Value.Abgr();
                uint clrR = next.Value.Abgr();
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

                if (needsProcessLmb && hoverTri && i != 0 && i != grad.Count - 1)
                {
                    needsProcessLmb = false;
                    linearLastClicked = curr;
                    _imDraggedGradientKey = curr.Key;
                }

                if (needsProcessRmb && hoverTri && i != 0 && i != grad.Count - 1)
                {
                    needsProcessRmb = false;
                    processedRmb = true;
                    linearLastClicked = curr;
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

            if (_lmbDown && linearLastClicked != null)
            {
                float mV = Math.Clamp((Client.Instance.Frontend.MouseX - curWin.X) / w, 0f, 1f);
                if (MathF.Abs(mV - _imDraggedGradientKey) > float.Epsilon)
                {
                    grad.RemoveInternalPoint(linearLastClicked);
                    linearLastClicked = grad.Add(mV, linearLastClicked.Value);
                    _imDraggedGradientKey = mV;
                }
            }

            if (_lmbDown && needsProcessLmb)
            {
                if (ImGui.IsMouseHoveringRect(new Vector2(curWin.X, curWin.Y), new Vector2(curWin.X + w, curWin.Y + 24)))
                {
                    float mV = Math.Clamp((ImGui.GetMousePos().X - curWin.X) / w, 0f, 1f);
                    Vector4 v = grad.Interpolate(mV);
                    grad.Add(mV, v);
                    needsProcessLmb = false;
                    linearLastClicked = null;
                }
            }

            if (_rmbDown && processedRmb && linearLastClicked != null)
            {
                grad.RemoveInternalPoint(linearLastClicked);
            }

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 64);

            return false;
        }

        private bool ImLinearGradientSingle(LinearGradient<float> grad, ref bool needsProcessLmb, ref bool needsProcessRmb)
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
            float maxVal = grad.Max(x => x.Value);

            for (int i = 0; i < grad.Count; i++)
            {
                LinearGradient<float>.LinearGradientPoint curr = grad.GetInternalPointAt(i);
                LinearGradient<float>.LinearGradientPoint next = grad.GetInternalPointAt((i + 1) % grad.Count);
                float fS = curr.Key * w;
                float fN = (next.Key < curr.Key ? 1 : next.Key) * w;
                float fYS = curr.Value / maxVal;
                float fYN = next.Value / maxVal;
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

                if (needsProcessLmb && hoverTri && i != 0 && i != grad.Count - 1)
                {
                    needsProcessLmb = false;
                    linearLastClickedSingle = curr;
                    _imDraggedGradientKey = curr.Key;
                    linearEditedValueSingle = curr;
                }

                if (needsProcessRmb && hoverTri && i != 0 && i != grad.Count - 1)
                {
                    needsProcessRmb = false;
                    processedRmb = true;
                    linearLastClickedSingle = curr;
                    linearEditedValueSingle = null;
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
                    linearEditedValueSingle = curr;
                }
            }

            if (_lmbDown && linearLastClickedSingle != null)
            {
                float mV = Math.Clamp((Client.Instance.Frontend.MouseX - curWin.X) / w, 0f, 1f);
                if (MathF.Abs(mV - _imDraggedGradientKey) > float.Epsilon)
                {
                    grad.RemoveInternalPoint(linearLastClickedSingle);
                    linearEditedValueSingle = linearLastClickedSingle = grad.Add(mV, linearLastClickedSingle.Value);
                    _imDraggedGradientKey = mV;
                }
            }

            if (_lmbDown && needsProcessLmb)
            {
                if (ImGui.IsMouseHoveringRect(new Vector2(curWin.X, curWin.Y), new Vector2(curWin.X + w, curWin.Y + 24)))
                {
                    float mV = Math.Clamp((Client.Instance.Frontend.MouseX - curWin.X) / w, 0f, 1f);
                    float v = grad.Interpolate(mV);
                    linearEditedValueSingle = grad.Add(mV, v);
                    needsProcessLmb = false;
                }
            }

            if (_rmbDown && processedRmb && linearLastClickedSingle != null)
            {
                grad.RemoveInternalPoint(linearLastClickedSingle);
            }

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 64);
            return false;
        }

        private void ImAssetRecepticle(SimpleLanguage lang, string text, Guid aId, GuiState state, ParticleSystem ps, int type)
        {
            bool result = ImGuiHelper.ImAssetRecepticle(lang, aId, type switch
            {
                0 => Client.Instance.Frontend.Renderer.GuiRenderer.AssetShaderIcon,
                1 => Client.Instance.Frontend.Renderer.GuiRenderer.AssetModelIcon,
                _ => Client.Instance.Frontend.Renderer.GuiRenderer.AssetImageIcon
            }, new Vector2(0, 24), type switch 
            { 
                0 => static x => x != null && x.Type is AssetType.Shader or AssetType.GlslFragmentShader,
                1 => static x => x != null && x.Type is AssetType.Model or AssetType.Texture,
                2 => static x => x != null && x.Type == AssetType.Texture,
                _ => static x => false
            }, out bool hovered);

            if (type == 1 && result)
            {
                state.particleModelHovered = ps;
            }

            if (type == 0 && result)
            {
                state.particleShaderHovered = ps;
            }

            if (type == 2 && result)
            {
                state.particleMaskHovered = ps;
            }

            if (hovered)
            {
                ImGui.SetTooltip(Client.Instance.Lang.Translate(text));
            }
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
