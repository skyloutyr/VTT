namespace VTT.Render.Gui
{
    using ImGuiNET;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using VTT.Asset;
    using VTT.Control;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public partial class GuiRenderer
    {
        private unsafe void RenderTurnTrackerOverlay(Map cMap, ImGuiWindowFlags window_flags)
        {
            if (cMap != null && cMap.TurnTracker.Visible)
            {
                float ww = ImGui.GetMainViewport().WorkSize.X;
                if (!this._turnTrackerCollapsed)
                {
                    ImGui.SetNextWindowSize(new System.Numerics.Vector2(640, 128));
                    ImGui.SetNextWindowPos(new(ww / 4, 0));
                    if (ImGui.Begin("##TurnTracker", window_flags & ~ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        if (cMap.TurnTracker.Entries.Count > 0)
                        {
                            lock (cMap.TurnTracker.Lock)
                            {
                                ImDrawListPtr idl = ImGui.GetWindowDrawList();
                                TurnTracker.Entry currentEntry = cMap.TurnTracker.GetAt(cMap.TurnTracker.EntryIndex);
                                TurnTracker.Entry last = cMap.TurnTracker.Entries[^1];
                                TurnTracker.Entry first = cMap.TurnTracker.Entries[0];
                                ImGui.SetCursorPosX(320 - 40);
                                for (int idx = 0; idx <= 5; ++idx)
                                {
                                    TurnTracker.Entry e = cMap.TurnTracker.GetAt(cMap.TurnTracker.EntryIndex + idx);
                                    bool hasEntryInfo = cMap.TurnTracker.GetEntryInfo(e, out Color tColor, out string tName, out string eName);
                                    if (cMap.GetObject(e.ObjectID, out MapObject mo))
                                    {
                                        if (e == first)
                                        {
                                            ImGui.Image(Client.Instance.Frontend.Renderer.White, new System.Numerics.Vector2(8, 96), System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, new System.Numerics.Vector4(0.234f, 0, 0.4f, 1.0f));
                                            ImGui.SameLine();
                                        }

                                        var ccXY = ImGui.GetCursorPos();
                                        ImGui.Image(this.TurnTrackerBackground, new System.Numerics.Vector2(64, 96), System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, (System.Numerics.Vector4)tColor);
                                        if (ImGui.IsItemHovered())
                                        {
                                            ImGui.BeginTooltip();
                                            ImGui.TextUnformatted(eName);
                                            ImGui.PushStyleColor(ImGuiCol.Text, tColor.Abgr());
                                            ImGui.TextUnformatted(tName);
                                            ImGui.PopStyleColor();
                                            ImGui.EndTooltip();
                                        }

                                        if (Client.Instance.AssetManager.ClientAssetLibrary.GetOrCreatePortrait(mo.AssetID, out AssetPreview ap) == AssetStatus.Return)
                                        {
                                            ImGui.SetCursorPos(ccXY);
                                            System.Numerics.Vector4 borderColor = System.Numerics.Vector4.Zero;
                                            if (e == currentEntry)
                                            {
                                                borderColor = (System.Numerics.Vector4)Color.Gold;
                                            }

                                            System.Numerics.Vector4 portrairColor = System.Numerics.Vector4.One;
                                            if (mo.MapLayer > 0)
                                            {
                                                portrairColor = Client.Instance.IsAdmin ? new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1.0f) : ImColBlack;
                                            }

                                            System.Numerics.Vector2 cursorScreenC = ImGui.GetCursorScreenPos();
                                            ImGui.Image(ap.GLTex, new System.Numerics.Vector2(64, 96), new System.Numerics.Vector2(0.25f, 0), new System.Numerics.Vector2(0.75f, 1), portrairColor);
                                            if (e == currentEntry)
                                            {
                                                ImDrawListPtr winDrawListC = ImGui.GetWindowDrawList();

                                                winDrawListC.AddQuad(
                                                    cursorScreenC + new System.Numerics.Vector2(1, 1),
                                                    cursorScreenC + new System.Numerics.Vector2(63, 1),
                                                    cursorScreenC + new System.Numerics.Vector2(63, 95),
                                                    cursorScreenC + new System.Numerics.Vector2(1, 95),
                                                    Color.White.Abgr()
                                                );

                                                winDrawListC.AddQuad(
                                                    cursorScreenC + new System.Numerics.Vector2(2, 2),
                                                    cursorScreenC + new System.Numerics.Vector2(62, 2),
                                                    cursorScreenC + new System.Numerics.Vector2(62, 94),
                                                    cursorScreenC + new System.Numerics.Vector2(2, 94),
                                                    Color.Black.Abgr()
                                                );

                                                winDrawListC.AddQuadFilled(
                                                    cursorScreenC + new System.Numerics.Vector2(0, -3),
                                                    cursorScreenC + new System.Numerics.Vector2(64, -3),
                                                    cursorScreenC + new System.Numerics.Vector2(64, 0),
                                                    cursorScreenC + new System.Numerics.Vector2(0, 0),
                                                    Color.Gold.Abgr()
                                                );

                                                winDrawListC.AddQuadFilled(
                                                    cursorScreenC + new System.Numerics.Vector2(0, 96),
                                                    cursorScreenC + new System.Numerics.Vector2(64, 96),
                                                    cursorScreenC + new System.Numerics.Vector2(64, 99),
                                                    cursorScreenC + new System.Numerics.Vector2(0, 99),
                                                    Color.Gold.Abgr()
                                                );
                                            }
                                        }

                                        if (e == last)
                                        {
                                            ImGui.SameLine();
                                            ImGui.Image(Client.Instance.Frontend.Renderer.White, new System.Numerics.Vector2(8, 96), System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, new System.Numerics.Vector4(0.234f, 0, 0.4f, 1.0f));
                                        }
                                    }
                                    else
                                    {
                                        ImGui.Image(this.TurnTrackerBackgroundNoObject, new System.Numerics.Vector2(64, 96), System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, System.Numerics.Vector4.One);
                                    }

                                    ImGui.SameLine();
                                }

                                int cX = 320 - 40 - 70;
                                float cY = ImGui.GetCursorPosY();
                                for (int idx = 1; idx <= 5; ++idx)
                                {
                                    ImGui.SetCursorPosX(cX);
                                    ImGui.SetCursorPosY(cY);
                                    TurnTracker.Entry e = cMap.TurnTracker.GetAt(cMap.TurnTracker.EntryIndex - idx);
                                    bool hasEntryInfo = cMap.TurnTracker.GetEntryInfo(e, out Color tColor, out string tName, out string eName);
                                    if (cMap.GetObject(e.ObjectID, out MapObject mo))
                                    {
                                        ImGui.Image(this.TurnTrackerBackground, new System.Numerics.Vector2(64, 96), System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, (System.Numerics.Vector4)tColor);
                                        if (ImGui.IsItemHovered())
                                        {
                                            ImGui.BeginTooltip();
                                            ImGui.TextUnformatted(eName);
                                            ImGui.PushStyleColor(ImGuiCol.Text, tColor.Abgr());
                                            ImGui.TextUnformatted(tName);
                                            ImGui.PopStyleColor();
                                            ImGui.EndTooltip();
                                        }

                                        if (Client.Instance.AssetManager.ClientAssetLibrary.GetOrCreatePortrait(mo.AssetID, out AssetPreview ap) == AssetStatus.Return)
                                        {
                                            ImGui.SetCursorPosX(cX);
                                            ImGui.SetCursorPosY(cY);
                                            System.Numerics.Vector4 borderColor = System.Numerics.Vector4.Zero;
                                            if (e == currentEntry)
                                            {
                                                System.Numerics.Vector4 colorDark = new System.Numerics.Vector4(0.234f, 0, 0.4f, 1.0f);
                                                System.Numerics.Vector4 colorBright = new System.Numerics.Vector4(0.862f, 0, 0.292f, 1.0f);
                                                borderColor = System.Numerics.Vector4.Lerp(colorDark, colorBright, (1.0f + MathF.Sin(Client.Instance.Frontend.UpdatesExisted * MathF.PI / 180.0f)) / 2f);
                                            }

                                            System.Numerics.Vector4 portrairColor = System.Numerics.Vector4.One;
                                            if (mo.MapLayer > 0)
                                            {
                                                portrairColor = Client.Instance.IsAdmin ? new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1.0f) : ImColBlack;
                                            }

                                            ImGui.Image(ap.GLTex, new System.Numerics.Vector2(64, 96), new System.Numerics.Vector2(0.25f, 0), new System.Numerics.Vector2(0.75f, 1), portrairColor, borderColor);
                                        }

                                        if (e == first)
                                        {
                                            cX -= 16;
                                            ImGui.SetCursorPosX(cX);
                                            ImGui.SetCursorPosY(cY);
                                            ImGui.Image(Client.Instance.Frontend.Renderer.White, new System.Numerics.Vector2(8, 96), System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, new System.Numerics.Vector4(0.234f, 0, 0.4f, 1.0f));
                                        }
                                    }
                                    else
                                    {
                                        ImGui.Image(this.TurnTrackerBackgroundNoObject, new System.Numerics.Vector2(64, 96), System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, System.Numerics.Vector4.One);
                                    }

                                    cX -= 70;
                                }
                            }

                            float tW = ImGui.CalcTextSize(cMap.TurnTracker.EntryName).X;
                            ImGui.SetCursorPosX(320 - (tW / 2));
                            ImGui.SetCursorPosY(110);
                            ImGui.TextUnformatted(cMap.TurnTracker.EntryName);
                            ImGui.SetCursorPosY(110);
                            ImGui.SetCursorPosX(120);
                            ImGui.PushStyleColor(ImGuiCol.Text, cMap.TurnTracker.CurrentColor.Abgr());
                            ImGui.TextUnformatted(cMap.TurnTracker.TeamName);
                            ImGui.SetCursorPosY(110);
                            ImGui.SetCursorPosX(520);
                            ImGui.TextUnformatted(cMap.TurnTracker.TeamName);
                            ImGui.PopStyleColor();
                        }
                    }

                    ImGui.End();
                }

                ImGui.SetNextWindowPos(new((ww / 4) + 320 - 24, this._turnTrackerCollapsed ? -12 : 120));
                ImGui.SetNextWindowBgAlpha(0.0f);
                ImGui.Begin("##TurnTrackerCollapseContainer", window_flags | ImGuiWindowFlags.NoBackground);
                ImGui.PushItemWidth(48);
                if (ImGui.ArrowButton("##TurnTrackerCollapseButton", this._turnTrackerCollapsed ? ImGuiDir.Down : ImGuiDir.Up))
                {
                    this._turnTrackerCollapsed = !this._turnTrackerCollapsed;
                }

                ImGui.PopItemWidth();
                ImGui.End();
            }
        }

        private unsafe void RenderTurnTrackerControls(Map cMap, SimpleLanguage lang, GuiState state)
        {
            if (this._showingTurnOrder && cMap != null && Client.Instance.IsAdmin)
            {
                lock (cMap.TurnTracker.Lock)
                {
                    if (ImGui.Begin(lang.Translate("ui.teams") + "###Teams"))
                    {
                        for (int i = 0; i < cMap.TurnTracker.Teams.Count; i++)
                        {
                            if (i == 0)
                            {
                                ImGui.BeginDisabled();
                            }

                            TurnTracker.Team t = cMap.TurnTracker.Teams[i];
                            string tName = t.Name;
                            if (ImGui.InputText("##TeamName" + i, ref tName, ushort.MaxValue, ImGuiInputTextFlags.EnterReturnsTrue))
                            {
                                PacketTeamInfo pti = new PacketTeamInfo() { Action = PacketTeamInfo.ActionType.UpdateName, Index = i, Name = tName, Color = t.Color };
                                pti.Send();
                            }

                            ImGui.SameLine();
                            if (ImGui.ColorButton("##TeamColor" + i, (System.Numerics.Vector4)t.Color))
                            {
                                this._editedTeamName = t.Name;
                                this._editedTeamColor = (System.Numerics.Vector4)t.Color;
                                state.changeTeamColorPopup = true;
                            }

                            ImGui.SameLine();
                            if (ImGui.ImageButton("TeamDeleteButton_" + i, this.DeleteIcon, Vec12x12))
                            {
                                PacketTeamInfo pti = new PacketTeamInfo() { Action = PacketTeamInfo.ActionType.Delete, Name = t.Name };
                                pti.Send();
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.teams.delete"));
                            }

                            if (i == 0)
                            {
                                ImGui.EndDisabled();
                            }
                        }

                        if (ImGui.ImageButton("btnAddTeam", this.AddIcon, Vec12x12))
                        {
                            PacketTeamInfo pti = new PacketTeamInfo() { Action = PacketTeamInfo.ActionType.Add, Color = Color.White, Name = "New Team " + cMap.TurnTracker.Teams.Count };
                            pti.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.teams.add"));
                        }
                    }

                    ImGui.End();

                    if (this._teams.Length != cMap.TurnTracker.Teams.Count)
                    {
                        Array.Resize(ref this._teams, cMap.TurnTracker.Teams.Count);
                    }

                    for (int i = cMap.TurnTracker.Teams.Count - 1; i >= 0; --i)
                    {
                        this._teams[i] = cMap.TurnTracker.Teams[i].Name;
                    }

                    if (ImGui.Begin(lang.Translate("ui.turn_tracker") + "###Turn Tracker Controls"))
                    {
                        bool ttVisible = cMap.TurnTracker.Visible;
                        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);
                        if (ttVisible)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Border, (System.Numerics.Vector4)Color.RoyalBlue);
                        }

                        if (ImGui.ImageButton("TurnTrackerVisibilityButton", this.FOWRevealIcon, Vec12x12))
                        {
                            PacketToggleTurnTrackerVisibility ptttv = new PacketToggleTurnTrackerVisibility() { Action = !cMap.TurnTracker.Visible };
                            ptttv.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.turn_tracker.visible"));
                        }

                        ImGui.SameLine();
                        if (ttVisible)
                        {
                            ImGui.PopStyleColor();
                        }

                        if (ImGui.ImageButton("TurnTrackerAddSelectedButton", this.AddIcon, Vec12x12))
                        {
                            for (int i = 0; i < Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Count; i++)
                            {
                                MapObject mo = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects[i];
                                new PacketAddTurnEntry() { AdditionIndex = -1, ObjectID = mo.ID, Value = 0, TeamName = string.Empty }.Send();
                            }
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.turn_tracker.add_selected"));
                        }

                        ImGui.PopStyleVar();
                        ImGui.SameLine();

                        if (ImGui.Button(lang.Translate("ui.turn_tracker.sort") + "###Sort"))
                        {
                            new PacketSortTurnTracker().Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.turn_tracker.sort.tt"));
                        }

                        ImGui.SameLine();
                        if (ImGui.Button(lang.Translate("ui.turn_tracker.force_sync") + "###Force-Sync"))
                        {
                            new PacketFullTurnTrackerUpdate().Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.turn_tracker.force_sync.tt"));
                        }

                        ImGui.SameLine();
                        ImGui.PushItemWidth(12);
                        if (ImGui.ArrowButton("TurnMoveLeft", ImGuiDir.Left))
                        {
                            new PacketMoveTurnToIndex() { Index = cMap.TurnTracker.EntryIndex - 1 }.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.turn_tracker.left"));
                        }

                        ImGui.SameLine();
                        if (ImGui.ArrowButton("TurnMoveRight", ImGuiDir.Right))
                        {
                            new PacketMoveTurnToIndex() { Index = cMap.TurnTracker.EntryIndex + 1 }.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.turn_tracker.right"));
                        }

                        ImGui.PopItemWidth();
                        System.Numerics.Vector2 wC = ImGui.GetWindowSize();
                        for (int i = 0; i < cMap.TurnTracker.Entries.Count; i++)
                        {
                            TurnTracker.Entry e = cMap.TurnTracker.Entries[i];
                            bool haveObject = cMap.GetObject(e.ObjectID, out MapObject mo);
                            string oName;
                            if (haveObject)
                            {
                                oName = mo.Name;
                                if (Debugger.IsAttached)
                                {
                                    oName += " (" + mo.ID + ")";
                                }
                            }
                            else
                            {
                                oName = lang.Translate("ui.turn_tracker.nao") + e.ObjectID;
                            }

                            bool border = i == cMap.TurnTracker.EntryIndex % cMap.TurnTracker.Entries.Count;
                            if (border)
                            {
                                System.Numerics.Vector4 colorDark = new System.Numerics.Vector4(0.234f, 0, 0.4f, 1.0f);
                                System.Numerics.Vector4 colorBright = new System.Numerics.Vector4(0.862f, 0, 0.292f, 1.0f);
                                System.Numerics.Vector4 borderColor = System.Numerics.Vector4.Lerp(colorDark, colorBright, (1.0f + MathF.Sin(Client.Instance.Frontend.UpdatesExisted * MathF.PI / 180.0f)) / 2f);
                                ImGui.PushStyleColor(ImGuiCol.Border, borderColor);
                            }

                            ImGui.BeginChild("turnTrackerNav_" + i, new System.Numerics.Vector2(wC.X - 32, 32), true, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings);
                            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
                            {
                                IntPtr sIdx = new IntPtr(&i);
                                ImGui.SetDragDropPayload("TurnTrackerDragDropPayload", sIdx, sizeof(int));
                                ImGui.TextUnformatted(oName);
                                ImGui.EndDragDropSource();
                            }

                            if (ImGui.ImageButton("GotoEntryBtn_" + i + "_" + e.ObjectID.ToString(), this.GotoIcon, Vec12x12))
                            {
                                if (haveObject)
                                {
                                    Vector3 p = mo.Position;
                                    Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
                                    cam.Position = p - (cam.Direction * 5.0f);
                                    cam.RecalculateData(assumedUpAxis: Vector3.UnitZ);
                                    if (Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftShift) || Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.RightShift))
                                    {
                                        Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Clear();
                                        Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Add(mo);
                                    }
                                }
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.turn_tracker.goto"));
                            }

                            ImGui.SameLine();
                            if (ImGui.ImageButton("TurnToEntryBtn_" + i + "_" + e.ObjectID.ToString(), this.MoveToIcon, Vec12x12))
                            {
                                new PacketMoveTurnToIndex() { Index = i }.Send();
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.turn_tracker.set_turn"));
                            }

                            ImGui.SameLine();
                            int tIdx = cMap.TurnTracker.Teams.IndexOf(e.Team);
                            if (tIdx != -1)
                            {
                                ImGui.PushItemWidth(100);
                                if (ImGui.Combo("##Team" + e.ObjectID, ref tIdx, this._teams, cMap.TurnTracker.Teams.Count))
                                {
                                    string tName = this._teams[tIdx];
                                    new PacketChangeTurnEntryProperty() { EntryIndex = i, EntryRefID = e.ObjectID, NewTeam = tName, Type = PacketChangeTurnEntryProperty.ChangeType.Team }.Send();
                                }

                                ImGui.PopItemWidth();
                                ImGui.SameLine();
                                ImGui.ColorButton("##ClrBtnD" + i, (System.Numerics.Vector4)e.Team.Color);
                                ImGui.SameLine();
                            }

                            float v = e.NumericValue;
                            ImGui.PushItemWidth(100);
                            if (ImGui.InputFloat("##Value" + e.ObjectID, ref v, 0, 0, "%.3f", ImGuiInputTextFlags.EnterReturnsTrue))
                            {
                                new PacketChangeTurnEntryProperty() { EntryIndex = i, EntryRefID = e.ObjectID, NewValue = v, Type = PacketChangeTurnEntryProperty.ChangeType.Value }.Send();
                            }

                            ImGui.PopItemWidth();
                            ImGui.SameLine();
                            if (ImGui.ImageButton("DeleteTurnEntry" + i + "_" + e.ObjectID, this.DeleteIcon, Vec12x12))
                            {
                                new PacketDeleteTurnEntry() { EntryIndex = i }.Send();
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(lang.Translate("ui.turn_tracker.delete"));
                            }

                            ImGui.SameLine();
                            ImGui.TextUnformatted(oName);
                            ImGui.SameLine();

                            ImGui.EndChild();

                            if (ImGui.BeginDragDropTarget())
                            {
                                try
                                {
                                    ImGuiPayloadPtr res = ImGui.AcceptDragDropPayload("TurnTrackerDragDropPayload");
                                    if (res.NativePtr != null)
                                    {
                                        int idx = Marshal.ReadInt32(res.Data);
                                        new PacketMoveTurnTrackerEntry() { IndexFrom = idx, IndexTo = i }.Send();
                                    }
                                }
                                catch (Exception managedEx)
                                {
                                    Client.Instance.Logger.Log(LogLevel.Fatal, "Could not handle unmanaged ImGui state - restart recommended!");
                                    Client.Instance.Logger.Exception(LogLevel.Fatal, managedEx);
                                }

                                ImGui.EndDragDropTarget();
                            }


                            if (border)
                            {
                                ImGui.PopStyleColor();
                            }
                        }
                    }

                    ImGui.End();
                }
            }
        }
    }
}
