﻿namespace VTT.Render.Gui
{
    using ImGuiNET;
    using System.Numerics;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using VTT.Asset;
    using VTT.Control;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public partial class GuiRenderer
    {
        private ulong _tickMenuOpened;
        private unsafe void RenderPopups(GuiState state, SimpleLanguage lang, string ok, string cancel, string close)
        {
            List<MapObject> os = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects;
            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.new_folder") + "###Create New Folder"))
            {
                ImGui.InputText(lang.Translate("ui.popup.new_folder.name") + "###Folder Name", ref this._newFolderNameString, 255);
                bool bc = ImGui.Button(cancel);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                bool bo = ImGui.Button(ok);

                if (bo || bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                if (bo && !string.IsNullOrEmpty(this._newFolderNameString))
                {
                    PacketAddRemoveAssetFolder paraf = new PacketAddRemoveAssetFolder() { IsServer = false, Name = this._newFolderNameString, Path = this.CurrentFolder.GetPath(), Remove = false, Session = Client.Instance.SessionID };
                    paraf.Send(Client.Instance.NetClient);
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.rename_folder") + "###Rename Folder"))
            {
                ImGui.InputText(lang.Translate("ui.popup.rename_folder.name") + "###Folder Name", ref this._newFolderNameString, 255);
                bool bc = ImGui.Button(cancel);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                bool bo = ImGui.Button(ok);

                if (bo || bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                if (bo && !string.IsNullOrEmpty(this._newFolderNameString))
                {
                    PacketRenameAssetFolder praf = new PacketRenameAssetFolder() { IsServer = false, Name = this._newFolderNameString, Path = this._contextDir.GetPath(), Session = Client.Instance.SessionID };
                    praf.Send(Client.Instance.NetClient);
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.confirm_delete") + "###Confirm Delete"))
            {
                ImGui.TextWrapped(lang.Translate("ui.popup.confirm_delete.text"));
                bool bc = ImGui.Button(cancel);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                bool bo = ImGui.Button(ok);

                if (bo || bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                if (bo)
                {
                    PacketAddRemoveAssetFolder paraf = new PacketAddRemoveAssetFolder() { IsServer = false, Name = string.Empty, Path = this._contextDir.GetPath(), Remove = true, Session = Client.Instance.SessionID };
                    paraf.Send(Client.Instance.NetClient);
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.change_bar_color") + "###Change Bar Color"))
            {
                DisplayBar db = this._editedMapObject.Bars[this._editedBarIndex];
                ImGui.ColorPicker4(lang.Translate("ui.generic.color") + "###Color", ref this._editedBarColor);
                bool bc = ImGui.Button(cancel);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                bool bo = ImGui.Button(ok);

                if (bo || bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                if (bo)
                {
                    db.DrawColor = Extensions.FromVec4(this._editedBarColor);
                    PacketMapObjectBar pmob = new PacketMapObjectBar() { BarAction = PacketMapObjectBar.Action.Change, Index = this._editedBarIndex, MapID = this._editedMapObject.MapID, ContainerID = this._editedMapObject.ID, Session = Client.Instance.SessionID, IsServer = false, Bar = db };
                    pmob.Send();
                }

                ImGui.EndPopup();
            }

            if (state.clientMap != null && ImGui.BeginPopupModal(lang.Translate("ui.popup.change_map_color") + "###Change Map Color"))
            {
                if (ImGui.ColorPicker4(lang.Translate("ui.generic.colour") + "###Map Color", ref this._editedMapColor))
                {
                    Color c = Extensions.FromVec4(this._editedMapColor);
                    switch (this._editedMapColorIndex)
                    {
                        case 0:
                        {
                            state.clientMap.GridColor = c;
                            break;
                        }

                        case 1:
                        {
                            state.clientMap.BackgroundColor = c;
                            break;
                        }

                        case 2:
                        {
                            state.clientMap.AmbientColor = c;
                            break;
                        }

                        case 3:
                        {
                            state.clientMap.SunColor = c;
                            break;
                        }
                    }
                }

                bool bc = ImGui.Button(cancel);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                bool bo = ImGui.Button(ok);
                if (bo || bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                if (bo)
                {
                    PacketChangeMapData pcmd = new PacketChangeMapData()
                    {
                        Data = Extensions.FromVec4(this._editedMapColor),
                        MapID = state.clientMap.ID,
                        Type = this._editedMapColorIndex switch
                        {
                            0 => PacketChangeMapData.DataType.GridColor,
                            1 => PacketChangeMapData.DataType.SkyColor,
                            2 => PacketChangeMapData.DataType.AmbientColor,
                            _ => PacketChangeMapData.DataType.SunColor
                        }
                    };

                    pcmd.Send();
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.change_team_color") + "###Change Team Color"))
            {
                ImGui.ColorPicker4(lang.Translate("ui.generic.color") + "###Map Team", ref this._editedTeamColor);
                bool bc = ImGui.Button(cancel);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                bool bo = ImGui.Button(ok);
                if (bo || bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                if (bo)
                {
                    PacketTeamInfo pti = new PacketTeamInfo() { Name = this._editedTeamName, Color = Extensions.FromVec4(this._editedTeamColor), Action = PacketTeamInfo.ActionType.UpdateColor };
                    pti.Send();
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.change_tint_color") + "###Change Tint Color"))
            {
                Vector4 tColor = ((Vector4)this._editedMapObject.TintColor);
                if (ImGui.ColorPicker4(lang.Translate("ui.generic.color") + "###Color", ref tColor))
                {
                    this._editedMapObject.TintColor = Extensions.FromVec4(tColor);
                }

                bool bc = ImGui.Button(cancel);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                bool bo = ImGui.Button(ok);

                if (bo || bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                if (bo)
                {
                    MapObject mo = this._editedMapObject;
                    new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.TintColor, Data = SelectedToPacket3(os, mo.TintColor) }.Send();
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.change_name_color") + "###Change Name Color"))
            {
                Vector4 tColor = ((Vector4)this._editedMapObject.NameColor);
                if (ImGui.ColorPicker4(lang.Translate("ui.generic.color") + "###Color", ref tColor))
                {
                    this._editedMapObject.NameColor = Extensions.FromVec4(tColor);
                }

                bool bc = ImGui.Button(cancel);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                bool bo = ImGui.Button(ok);

                if (bo || bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                if (bo)
                {
                    MapObject mo = this._editedMapObject;
                    new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.NameColor, Data = SelectedToPacket3(os, mo.NameColor) }.Send();
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.change_aura_color") + "###Change Aura Color"))
            {
                if (this._editedBarIndex >= this._editedMapObject.Auras.Count)
                {
                    ImGui.CloseCurrentPopup();
                }
                else
                {
                    (float, Color) aDat = this._editedMapObject.Auras[this._editedBarIndex];
                    ImGui.ColorPicker4(lang.Translate("ui.generic.color") + "###Color", ref this._editedBarColor);
                    bool bc = ImGui.Button(cancel);
                    ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                    bool bo = ImGui.Button(ok);

                    if (bo || bc)
                    {
                        ImGui.CloseCurrentPopup();
                    }

                    if (bo)
                    {
                        MapObject mo = this._editedMapObject;
                        new PacketAura() { ActionType = PacketAura.Action.Update, AuraColor = Extensions.FromVec4(this._editedBarColor), AuraRange = aDat.Item1, Index = this._editedBarIndex, ObjectID = mo.ID, MapID = mo.MapID }.Send();
                    }
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.change_fast_light_color") + "###Change Fast Light Color"))
            {
                if (this._editedBarIndex >= this._editedMapObject.FastLights.Count)
                {
                    ImGui.CloseCurrentPopup();
                }
                else
                {
                    FastLight aDat = this._editedMapObject.FastLights[this._editedBarIndex];
                    ImGui.ColorPicker4(lang.Translate("ui.generic.color") + "###Color", ref this._editedBarColor);
                    aDat.LightColor = this._editedBarColor.Xyz();
                    bool bc = ImGui.Button(cancel);
                    ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                    bool bo = ImGui.Button(ok);

                    if (bo || bc)
                    {
                        ImGui.CloseCurrentPopup();
                    }

                    if (bo)
                    {
                        MapObject mo = this._editedMapObject;
                        new PacketFastLight() { ActionType = PacketFastLight.Action.Update, Light = aDat.Clone(), Index = this._editedBarIndex, ObjectID = mo.ID, MapID = mo.MapID }.Send();
                    }

                    if (bc)
                    {
                        aDat.LightColor = this._initialEditedFastLightColor;
                    }
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.delete_map") + "###Delete Map"))
            {
                ImGui.TextWrapped(lang.Translate("ui.popup.delete_map.text"));
                bool bc = ImGui.Button(cancel);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                bool bo = ImGui.Button(ok);

                if (bo || bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                if (bo)
                {
                    PacketDeleteMap pdm = new PacketDeleteMap() { MapID = this._deletedMapId };
                    pdm.Send();
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.roll_dice") + "###Roll Dice"))
            {
                bool anyPress = false;
                string resultingRolls = string.Empty;
                if (ImGui.BeginTable("##DiceRollTable", 3))
                {
                    ImGui.TableSetupColumn("Singular Die");
                    ImGui.TableSetupColumn("Dice");
                    ImGui.TableSetupColumn("Separate Dice");

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.TextUnformatted(lang.Translate("ui.popup.roll_singular"));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.popup.roll_singular.tt"));
                    }

                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextUnformatted(lang.Translate("ui.popup.roll_compound"));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.popup.roll_compound.tt"));
                    }

                    ImGui.TableSetColumnIndex(2);
                    ImGui.TextUnformatted(lang.Translate("ui.popup.roll_separate"));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.popup.roll_separate.tt"));
                    }

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(1);
                    ImGui.InputInt("##Dice Amount", ref this._numDiceSingular);
                    ImGui.TableSetColumnIndex(2);
                    ImGui.PushID("Dice Amount Separate");
                    ImGui.InputInt("##Dice Amount", ref this._numDiceSeparate);
                    ImGui.PopID();

                    int[] dieSide = { 2, 4, 6, 8, 10, 12, 20, 100 };
                    for (int i = 0; i < dieSide.Length; ++i)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.PushID("dieBtnID_" + i + "s");
                        if (ImGui.Button("1d" + dieSide[i]))
                        {
                            anyPress = true;
                            resultingRolls = $"[m:DiceRoll][roll(1, {dieSide[i]})]";
                        }

                        ImGui.PopID();
                        ImGui.TableSetColumnIndex(1);
                        ImGui.PushID("dieBtnID_" + i + "c");
                        if (ImGui.Button(this._numDiceSingular + "d" + dieSide[i]))
                        {
                            anyPress = true;
                            resultingRolls = $"[m:DiceRoll][roll({this._numDiceSingular}, {dieSide[i]})]";
                        }

                        ImGui.PopID();
                        ImGui.TableSetColumnIndex(2);
                        ImGui.PushID("dieBtnID_" + i + "m");
                        if (ImGui.Button(this._numDiceSeparate + "d" + dieSide[i]))
                        {
                            anyPress = true;
                            if (this._numDiceSeparate > 1)
                            {
                                this._numDiceSeparate = Math.Min(this._numDiceSeparate, 10000);
                                resultingRolls = $"[m:DiceRolls]";
                                for (int j = 0; j < this._numDiceSeparate; ++j)
                                {
                                    resultingRolls += $"[roll(1, {dieSide[i]})]";
                                }
                            }
                            else
                            {
                                resultingRolls = $"[m:DiceRoll][roll(1, {dieSide[i]})]";
                            }
                        }

                        ImGui.PopID();
                    }

                    ImGui.EndTable();
                }

                ImGui.Text(lang.Translate("ui.popup.roll_dice.custom"));
                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("##customRollNumDie", ref this._numDiceCustom);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("##customRollDieSide", ref this._dieSideCustom);
                ImGui.SameLine();
                ImGui.Text(" + ");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("##customRollDieExtra", ref this._dieExtraCustom);
                ImGui.SameLine();
                ImGui.PushID("customRollActionButton");
                if (ImGui.Button(lang.Translate("ui.popup.roll_dice.roll") + "###Roll"))
                {
                    anyPress = true;
                    string rollsStr = "";
                    if (this._numDiceCustom <= 1)
                    {
                        rollsStr = $"[roll({this._numDiceCustom}, {this._dieSideCustom})]";
                    }
                    else
                    {
                        rollsStr = "(";
                        for (int i = 0; i < this._numDiceCustom; ++i)
                        {
                            rollsStr += $"[roll(1, {this._dieSideCustom})]";
                            if (i != this._numDiceCustom - 1)
                            {
                                rollsStr += "+";
                            }
                        }

                        rollsStr += ")";
                    }

                    resultingRolls = $"[m:RollExpression]{rollsStr}+{this._dieExtraCustom}";
                }

                ImGui.PopID();
                if (anyPress && (Client.Instance.NetClient?.IsConnected ?? false))
                {
                    PacketChatMessage pcm = new PacketChatMessage() { Message = resultingRolls };
                    pcm.Send();
                    this._chat.Add(resultingRolls);
                    this._cChatIndex = this._chat.Count;
                }

                bool bc = ImGui.Button(close);

                if (bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.link_image") + "###Link Image"))
            {
                ImGui.InputText(lang.Translate("ui.popup.link_image.url") + "###URL", ref this._imgUrl, ushort.MaxValue);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.popup.link_image.url.tt"));
                }

                ImGui.InputInt(lang.Translate("ui.popup.link_image.width") + "###ImgWidth", ref this._imgWidth);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.popup.link_image.width.tt"));
                }

                ImGui.InputInt(lang.Translate("ui.popup.link_image.height") + "###ImGHeight", ref this._imgHeight);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.popup.link_image.height.tt"));
                }

                ImGui.TextUnformatted(lang.Translate("ui.popup.link_image.tooltip"));
                ImGui.InputTextMultiline("##ImageTooltip", ref this._imgTooltip, ushort.MaxValue, new Vector2(ImGui.GetContentRegionAvail().X - 32, 300));

                bool bc = ImGui.Button(cancel);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                bool bo = ImGui.Button(ok);

                if (bo || bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                if (bo && !string.IsNullOrEmpty(this._imgUrl))
                {
                    this._imgWidth = Math.Clamp(this._imgWidth, 1, 680);
                    this._imgHeight = Math.Clamp(this._imgHeight, 1, 680);
                    string cStr = $"[m:Image][p:{this._imgWidth}][p:{this._imgHeight}][t:{this._imgTooltip}][p:{this._imgUrl}]";
                    new PacketChatMessage() { Message = cStr }.Send();
                    this._chat.Add(cStr);
                    this._cChatIndex = this._chat.Count;
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.journal") + "###Journal"))
            {
                bool canEdit = Client.Instance.IsAdmin || this._editedJournal.IsEditable || this._editedJournal.OwnerID == Client.Instance.ID;
                string jTitle = this._editedJournal.Title;
                if (!canEdit)
                {
                    ImGui.BeginDisabled();
                }

                if (ImGui.InputText(lang.Translate("ui.journal.title"), ref jTitle, 255, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    this._editedJournal.Title = jTitle;
                    new PacketChangeJournal() { Change = PacketChangeJournal.FieldType.Title, JournalID = this._editedJournal.SelfID, Value = jTitle }.Send();
                }

                if (!canEdit)
                {
                    ImGui.EndDisabled();
                }

                if (!Client.Instance.IsAdmin)
                {
                    ImGui.BeginDisabled();
                }

                bool editable = this._editedJournal.IsEditable;
                bool publ = this._editedJournal.IsPublic;
                if (ImGui.Checkbox(lang.Translate("ui.journal.public"), ref publ))
                {
                    this._editedJournal.IsPublic = publ;
                    new PacketChangeJournal() { Change = PacketChangeJournal.FieldType.IsPublic, JournalID = this._editedJournal.SelfID, Value = publ }.Send();
                }

                ImGui.SameLine();
                if (ImGui.Checkbox(lang.Translate("ui.journal.editable"), ref editable))
                {
                    this._editedJournal.IsEditable = editable;
                    new PacketChangeJournal() { Change = PacketChangeJournal.FieldType.IsEditable, JournalID = this._editedJournal.SelfID, Value = publ }.Send();
                }

                if (!Client.Instance.IsAdmin)
                {
                    ImGui.EndDisabled();
                }

                Vector2 wC = ImGui.GetWindowSize();
                string jText = this._editedJournal.Text;
                if (ImGui.InputTextMultiline(lang.Translate("ui.journal.text"), ref jText, ushort.MaxValue, new Vector2(wC.X - 8, 300)))
                {
                    if (canEdit)
                    {
                        this._journalTextEdited = true;
                        this._editedJournal.Text = jText;
                    }
                }

                if (ImGui.Button(close))
                {
                    ImGui.CloseCurrentPopup();
                    if (this._journalTextEdited)
                    {
                        new PacketChangeJournal() { Change = PacketChangeJournal.FieldType.Text, JournalID = this._editedJournal.SelfID, Value = jText }.Send();
                    }
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.rename_asset") + "###Rename Asset"))
            {
                ImGui.InputText(lang.Translate("ui.popup.rename_asset.name") + "###Asset Name", ref this._newFolderNameString, 255);
                bool bc = ImGui.Button(cancel);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                bool bo = ImGui.Button(ok);

                if (bo || bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                if (bo && !string.IsNullOrEmpty(this._newFolderNameString))
                {
                    new PacketAssetRename() { Name = this._newFolderNameString, RefID = this._editedRef.AssetID }.Send();
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.confirm_delete_asset") + "###Confirm Delete Asset"))
            {
                ImGui.TextWrapped(lang.Translate("ui.popup.confirm_delete_asset.text"));
                bool bc = ImGui.Button(cancel);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                bool bo = ImGui.Button(ok);

                if (bo || bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                if (bo)
                {
                    new PacketAssetDelete() { RefID = this._editedRef.AssetID }.Send(); // AssetID and RefID match
                }

                ImGui.EndPopup();
            }

            if (state.clientMap != null && ImGui.BeginPopupModal(lang.Translate("ui.popup.status") + "###Create Status Effect", ref this._statusOpen))
            {
                string rF = this._statusSortString;
                if (ImGui.InputText(lang.Translate("ui.popup.status.filter"), ref rF, 256))
                {
                    this._statusSortString = rF;
                    this._sortedStatuses.Clear();
                    string test = rF.ToLower();
                    foreach ((string, float, float) s in this._allStatuses)
                    {
                        if (s.Item1.Contains(test))
                        {
                            this._sortedStatuses.Add(s);
                        }
                    }
                }

                IEnumerable<(string, float, float)> iterableBtns = string.IsNullOrEmpty(rF) ? this._allStatuses : this._sortedStatuses;
                Vector2 cursorStart = ImGui.GetCursorPos();
                float cX = 0;
                float cY = 0;
                float w = ImGui.GetWindowWidth();
                float scrollMin = ImGui.GetScrollY();
                float scrollMax = scrollMin + ImGui.GetWindowHeight();
                foreach ((string, float, float) btn in iterableBtns)
                {
                    ImGui.SetCursorPos(cursorStart + new Vector2(cX, cY));
                    Vector2 st = new Vector2(btn.Item2, btn.Item3);
                    if (ImGui.ImageButton("##BtnStatus_" + btn.Item1, this.StatusAtlas, Vec32x32, st, st + new Vector2(this._statusStepX, this._statusStepY)))
                    {
                        new PacketObjectStatusEffect() { MapID = state.clientMap.ID, ObjectID = this._editedMapObject.ID, EffectName = btn.Item1, S = btn.Item2, T = btn.Item3, Remove = false }.Send();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(btn.Item1);
                    }

                    cX += 32 + 16;
                    if (cX + 48 >= w)
                    {
                        cX = 0;
                        cY += 32 + 16;
                    }
                }

                ImGui.EndPopup();
            }

            bool open = true;
            ImGui.SetNextWindowBgAlpha(0.35f);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetWorkCenter() - (new Vector2(136, 200) / 2), ImGuiCond.Appearing);
            if (ImGui.BeginPopupModal(lang.Translate("ui.menu") + "###Menu", ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking))
            {
                if (ImGui.Button(lang.Translate("ui.menu.disconnect") + "###Disconnect", new Vector2(128, 32)))
                {
                    Client.Instance.Frontend.EnqueueTask(() =>
                    {
                        Client.Instance.Disconnect(DisconnectReason.ManualDisconnect);
                        if (Server.Instance != null)
                        {
                            Server.Instance.DisconnectAll();
                            Server.Instance.Delete();
                        }
                    });

                    ImGui.CloseCurrentPopup();
                }

                Vector4 color = Client.Instance.VSCCIntegration.IsConnected ? (Vector4)Color.DarkGreen : Client.Instance.VSCCIntegration.Running ? (Vector4)Color.Yellow : *ImGui.GetStyleColorVec4(ImGuiCol.Button);
                ImGui.PushStyleColor(ImGuiCol.Button, color);
                if (ImGui.Button(lang.Translate("ui.menu.vscc") + "###Connect VSCC", new Vector2(128, 32)))
                {
                    if (Client.Instance.Frontend.GameHandle.IsAnyControlDown())
                    {
                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = "https://github.com/skyloutyr/VSCC/releases",
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    }
                    else
                    {
                        if (!Client.Instance.VSCCIntegration.Running)
                        {
                            Client.Instance.VSCCIntegration.Create();
                        }
                    }
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.menu.vscc.tt"));
                }

                ImGui.PopStyleColor();

                if (ImGui.Button(lang.Translate("ui.menu.back") + "###Back", new Vector2(128, 32)))
                {
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.CollapsingHeader(lang.Translate("menu.settings") + "###Settings"))
                {
                    MainMenu.MainMenuRenderer.DrawSettings(lang, state);
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.texture_properties") + "###Edit Texture"))
            {
                AssetRef cRef = this._editedRef;
                GL.WrapParam wrapS = this._editedTextureMetadataCopy.WrapS;
                GL.WrapParam wrapT = this._editedTextureMetadataCopy.WrapT;
                GL.FilterParam filterMin = this._editedTextureMetadataCopy.FilterMin;
                GL.FilterParam filterMag = this._editedTextureMetadataCopy.FilterMag;
                bool alpha = this._editedTextureMetadataCopy.EnableBlending;
                bool compress = this._editedTextureMetadataCopy.Compress;
                bool gamma = this._editedTextureMetadataCopy.GammaCorrect;
                bool a2e = this._editedTextureMetadataCopy.AlbedoIsEmissive;

                string[] wrapModes = { lang.Translate("ui.texture.wrap.repeat"), lang.Translate("ui.texture.wrap.mirror"), lang.Translate("ui.texture.wrap.clamp") };
                string[] filterModes = { lang.Translate("ui.texture.filter.nearest"), lang.Translate("ui.texture.filter.linear"), lang.Translate("ui.texture.filter.linear_mipmaps_nearest"), lang.Translate("ui.texture.filter.linear_mipmaps_linear") };
                int cWrapS = this._editedTextureMetadataCopy.WrapS == GL.WrapParam.Repeat ? 0 : this._editedTextureMetadataCopy.WrapS == GL.WrapParam.Mirror ? 1 : 2;
                int cWrapT = this._editedTextureMetadataCopy.WrapT == GL.WrapParam.Repeat ? 0 : this._editedTextureMetadataCopy.WrapT == GL.WrapParam.Mirror ? 1 : 2;
                int cfMin = this._editedTextureMetadataCopy.FilterMin == GL.FilterParam.Nearest ? 0 : this._editedTextureMetadataCopy.FilterMin == GL.FilterParam.Linear ? 1 : this._editedTextureMetadataCopy.FilterMin == GL.FilterParam.LinearMipmapNearest ? 2 : 3;
                int cfMag = this._editedTextureMetadataCopy.FilterMag == GL.FilterParam.Nearest ? 0 : 1;

                ImGui.Text(lang.Translate("ui.texture.wrap_s"));
                if (ImGui.Combo("##Wrap S", ref cWrapS, wrapModes, 3))
                {
                    wrapS = cWrapS == 0 ? GL.WrapParam.Repeat : cWrapS == 1 ? GL.WrapParam.Mirror : GL.WrapParam.ClampToEdge;
                    this._editedTextureMetadataCopy.WrapS = wrapS;
                }

                ImGui.Text(lang.Translate("ui.texture.wrap_t"));
                if (ImGui.Combo("##Wrap T", ref cWrapT, wrapModes, 3))
                {
                    wrapT = cWrapT == 0 ? GL.WrapParam.Repeat : cWrapT == 1 ? GL.WrapParam.Mirror : GL.WrapParam.ClampToEdge;
                    this._editedTextureMetadataCopy.WrapT = wrapT;
                }

                ImGui.Text(lang.Translate("ui.texture.filter_min"));
                if (ImGui.Combo("##Min Filter", ref cfMin, filterModes, 4))
                {
                    this._editedTextureMetadataCopy.FilterMin = filterMin =
                        cfMin == 0 ? GL.FilterParam.Nearest :
                        cfMin == 1 ? GL.FilterParam.Linear :
                        cfMin == 2 ? GL.FilterParam.LinearMipmapNearest : GL.FilterParam.LinearMipmapLinear;
                }

                ImGui.Text(lang.Translate("ui.texture.filter_mag"));
                if (ImGui.Combo("##Mag Filter", ref cfMag, filterModes, 2))
                {
                    this._editedTextureMetadataCopy.FilterMag = filterMag = cfMag == 0 ? GL.FilterParam.Nearest : GL.FilterParam.Linear;
                }

                if (ImGui.Checkbox(lang.Translate("ui.texture.blend"), ref alpha))
                {
                    this._editedTextureMetadataCopy.EnableBlending = alpha;
                }

                if (ImGui.Checkbox(lang.Translate("ui.texture.compress"), ref compress))
                {
                    this._editedTextureMetadataCopy.Compress = compress;
                }

                if (ImGui.Checkbox(lang.Translate("ui.texture.gamma"), ref gamma))
                {
                    this._editedTextureMetadataCopy.GammaCorrect = gamma;
                }

                if (ImGui.Checkbox(lang.Translate("ui.texture.albedo_is_emissive"), ref a2e))
                {
                    this._editedTextureMetadataCopy.AlbedoIsEmissive = a2e;
                }

                bool bc = ImGui.Button(cancel);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                bool bo = ImGui.Button(ok);

                if (bo || bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                if (bo)
                {
                    new PacketChangeTextureMetadata() { AssetID = cRef.AssetID, RefID = cRef.AssetID, Metadata = this._editedTextureMetadataCopy }.Send();
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal(lang.Translate("ui.popup.model_properties") + "###Edit Model"))
            {
                AssetRef cRef = this._editedRef;
                bool cAlbedo = this._editedModelMetadataCopy.CompressAlbedo;
                bool cEmissive = this._editedModelMetadataCopy.CompressEmissive;
                bool cNormal = this._editedModelMetadataCopy.CompressNormal;
                bool cAOMR = this._editedModelMetadataCopy.CompressAOMR;
                bool fullRangeNormals = this._editedModelMetadataCopy.FullRangeNormals;

                if (ImGui.Checkbox(lang.Translate("ui.model.compress.albedo"), ref cAlbedo))
                {
                    this._editedModelMetadataCopy.CompressAlbedo = cAlbedo;
                }

                if (ImGui.Checkbox(lang.Translate("ui.model.compress.normal"), ref cNormal))
                {
                    this._editedModelMetadataCopy.CompressNormal = cNormal;
                }

                if (ImGui.Checkbox(lang.Translate("ui.model.compress.aomrg"), ref cAOMR))
                {
                    this._editedModelMetadataCopy.CompressAOMR = cAOMR;
                }

                if (ImGui.Checkbox(lang.Translate("ui.model.compress.emissive"), ref cEmissive))
                {
                    this._editedModelMetadataCopy.CompressAlbedo = cAlbedo;
                }

                if (ImGui.Checkbox(lang.Translate("ui.model.full_range_normals"), ref fullRangeNormals))
                {
                    this._editedModelMetadataCopy.FullRangeNormals = fullRangeNormals;
                }

                bool bc = ImGui.Button(cancel);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
                bool bo = ImGui.Button(ok);

                if (bo || bc)
                {
                    ImGui.CloseCurrentPopup();
                }

                if (bo)
                {
                    new PacketChangeModelMetadata() { AssetID = cRef.AssetID, RefID = cRef.AssetID, Metadata = this._editedModelMetadataCopy }.Send();
                }

                ImGui.EndPopup();
            }
        }

        private TextureData.Metadata _editedTextureMetadataCopy;
        private ModelData.Metadata _editedModelMetadataCopy;
        private Vector3 _initialEditedFastLightColor;
        private bool _escDown;

        private unsafe void HandlePopupRequests(GuiState state)
        {
            if (state.openNewFolderPopup)
            {
                ImGui.OpenPopup("###Create New Folder");
                this._newFolderNameString = "New Folder";
            }

            if (state.editFolderPopup)
            {
                ImGui.OpenPopup("###Rename Folder");
            }

            if (state.deleteFolderPopup)
            {
                ImGui.OpenPopup("###Confirm Delete");
            }

            if (state.editAssetPopup)
            {
                ImGui.OpenPopup("###Rename Asset");
            }

            if (state.deleteAssetPopup)
            {
                ImGui.OpenPopup("###Confirm Delete Asset");
            }

            if (state.changeColorPopup)
            {
                ImGui.OpenPopup("###Change Bar Color");
            }

            if (state.changeMapColorPopup)
            {
                ImGui.OpenPopup("###Change Map Color");
            }

            if (state.deleteMapPopup)
            {
                ImGui.OpenPopup("###Delete Map");
            }

            if (state.rollPopup)
            {
                ImGui.OpenPopup("###Roll Dice");
            }

            if (state.changeTeamColorPopup)
            {
                ImGui.OpenPopup("###Change Team Color");
            }

            if (state.linkPopup)
            {
                ImGui.OpenPopup("###Link Image");
            }

            if (state.journalPopup)
            {
                ImGui.OpenPopup("###Journal");
            }

            if (!ImGui.GetIO().WantCaptureKeyboard && ImGui.IsKeyDown(ImGuiKey.Escape) && !this._escDown)
            {
                bool manipulatingFowPolygonNow = 
                    Client.Instance.Frontend?.Renderer?.MapRenderer?.FOWRenderer?.FowSelectionPoints?.Count > 0 && 
                    Client.Instance.Frontend?.Renderer?.MapRenderer?.FOWRenderer?.PaintMode == FOWRenderer.SelectionMode.Polygon && 
                    Client.Instance.Frontend?.Renderer?.ObjectRenderer?.EditMode == EditMode.FOW;

                state.menu = !manipulatingFowPolygonNow && !this._escapeCapturedThisFrame;
                this._escDown = true;
            }

            if (!ImGui.GetIO().WantCaptureKeyboard && !ImGui.IsKeyDown(ImGuiKey.Escape) && this._escDown)
            {
                this._escDown = false;
            }

            if (state.menu)
            {
                this._tickMenuOpened = Client.Instance.Frontend.UpdatesExisted;
                ImGui.OpenPopup("###Menu");
            }

            if (state.inspectPopup)
            {
                ImGui.OpenPopup("Inspect Window");
            }

            if (state.newStatusEffectPopup)
            {
                this._statusOpen = true;
                ImGui.OpenPopup("###Create Status Effect");
            }

            if (state.changeTintColorPopup)
            {
                ImGui.OpenPopup("###Change Tint Color");
            }

            if (state.changeNameColorPopup)
            {
                ImGui.OpenPopup("###Change Name Color");
            }

            if (state.changeAuraColorPopup)
            {
                ImGui.OpenPopup("###Change Aura Color");
            }

            if (state.changeFastLightColorPopup)
            {
                ImGui.OpenPopup("###Change Fast Light Color");
            }

            if (state.editTexturePopup)
            {
                ImGui.OpenPopup("###Edit Texture");
            }

            if (state.editModelPopup)
            {
                ImGui.OpenPopup("###Edit Model");
            }

            if (state.editParticleSystemPopup && !this.ParticleEditorRenderer.popupState)
            {
                this.ParticleEditorRenderer.popupState = true;
            }

            if (state.editShaderPopup && !this.ShaderEditorRenderer.popupState)
            {
                this.ShaderEditorRenderer.popupState = true;
                this.ShaderEditorRenderer.EditedGraph = null;
            }
        }
    }
}
