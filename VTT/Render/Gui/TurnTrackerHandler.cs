namespace VTT.Render.Gui
{
    using ImGuiNET;
    using System.Numerics;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using VTT.Asset;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;
    using Vec2 = System.Numerics.Vector2;
    using Vec4 = System.Numerics.Vector4;

    public partial class GuiRenderer
    {
        private readonly List<TurnTrackerParticle> _particles = new List<TurnTrackerParticle>();
        private bool _turnTrackerVisible;
        private float _scrollDYChange;
        private int _ttOffset = 0;

        public void HandleScrollWheelExtra(float dx, float dy) => this._scrollDYChange += dy;

        private unsafe void RenderTurnTrackerOverlay(Map cMap, ImGuiWindowFlags window_flags, GuiState state)
        {
            this._turnTrackerVisible = false;
            if (this.ShaderEditorRenderer.popupState || this.ParticleEditorRenderer.popupState)
            {
                return;
            }

            if (cMap != null && cMap.TurnTracker.Visible)
            {
                float ww = ImGui.GetMainViewport().WorkSize.X;
                if (!this._turnTrackerCollapsed)
                {
                    this._turnTrackerVisible = true;
                    ImGui.SetNextWindowSize(new Vec2(640, 132));
                    ImGui.SetNextWindowPos(new(ww / 4, 0));
                    if (ImGui.Begin("##TurnTracker", (window_flags | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollWithMouse) & ~ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        if (cMap.TurnTracker.Entries.Count > 0)
                        {
                            lock (cMap.TurnTracker.Lock)
                            {
                                ImDrawListPtr idl = ImGui.GetWindowDrawList();
                                Vec2 cursor = ImGui.GetCursorScreenPos();
                                ImGui.PushClipRect(cursor, cursor + new Vec2(640, 132), false);
                                TurnTracker.Entry currentEntry = cMap.TurnTracker.GetAt(cMap.TurnTracker.EntryIndex);
                                TurnTracker.Entry last = cMap.TurnTracker.Entries[^1];
                                TurnTracker.Entry first = cMap.TurnTracker.Entries[0];
                                Vec2 bgSize = new Vec2(64, 96);
                                Vec2 smSize = new Vec2(48, 72);
                                Vec2 separatorSize = new Vec2(12, 72);
                                Vec2 pen = new Vec2(0, 0);
                                float oX = 0;
                                if (this._scrollDYChange != 0 && ImGui.IsMouseHoveringRect(cursor, cursor + new Vec2(640, 132)))
                                {
                                    this._ttOffset -= Math.Sign(this._scrollDYChange);
                                }

                                int ttLenMax = Math.Clamp(Client.Instance.Settings.TurnTrackerSize, 2, 6);
                                for (int i = -ttLenMax; i <= ttLenMax; ++i)
                                {
                                    TurnTracker.Entry e = cMap.TurnTracker.GetAt(cMap.TurnTracker.EntryIndex + i + this._ttOffset);
                                    Vec2 sz = e == currentEntry ? bgSize : smSize;
                                    oX += sz.X + 2;
                                    if (e == last)
                                    {
                                        oX += 14;
                                    }
                                }

                                cursor.X += (640 - oX) / 2f;
                                for (int i = -ttLenMax; i <= ttLenMax; ++i)
                                {
                                    TurnTracker.Entry e = cMap.TurnTracker.GetAt(cMap.TurnTracker.EntryIndex + i + this._ttOffset);
                                    Vec2 sz = e == currentEntry ? bgSize : smSize;
                                    bool hasEntryInfo = cMap.TurnTracker.GetEntryInfo(e, out Color tColor, out string tName, out string eName);
                                    if (cMap.GetObject(e.ObjectID, out MapObject mo))
                                    {
                                        idl.AddImage(this.TurnTrackerBackground, cursor + pen, cursor + pen + sz, Vec2.Zero, Vec2.One, tColor.Abgr());
                                        Vec4 hColor = (Vec4)tColor;
                                        if (e == currentEntry)
                                        {
                                            hColor = Vec4.Lerp((Vec4)Color.Gold, (Vec4)Color.White, (1.0f + MathF.Sin(Client.Instance.Frontend.UpdatesExisted * MathF.PI / 180.0f)) / 2f);
                                            uint particleColor = tColor.Abgr();
                                            for (int j = this._particles.Count - 1; j >= 0; j--)
                                            {
                                                TurnTrackerParticle ttp = this._particles[j];
                                                if (ttp.IsDead)
                                                {
                                                    this._particles.RemoveAt(j);
                                                }
                                                else
                                                {
                                                    idl.AddImage(this.TurnTrackerParticle, ttp.RelativePosition + cursor + pen - (new Vec2(4, 4) * ttp.Scale), ttp.RelativePosition + cursor + pen + (new Vec2(4, 4) * ttp.Scale), Vec2.Zero, Vec2.One, ttp.Color == 0xffffffffu ? particleColor : ttp.Color);
                                                }
                                            }
                                        }

                                        if (Client.Instance.AssetManager.ClientAssetLibrary.GetOrCreatePortrait(mo.AssetID, out AssetPreview ap) == AssetStatus.Return)
                                        {
                                            Vec4 borderColor = Vec4.Zero;
                                            if (e == currentEntry)
                                            {
                                                borderColor = (Vec4)Color.Gold;
                                            }

                                            Vec4 portrairColor = Vec4.One;
                                            if (mo.MapLayer > 0)
                                            {
                                                portrairColor = !Client.Instance.IsAdmin ? ImColBlack : new Vec4(0.5f, 0.5f, 0.5f, 1.0f);
                                            }

                                            float ar = 64f / 96f;
                                            ar *= 0.5f;
                                            Texture glTex = ap.GetGLTexture();
                                            if (glTex != null && glTex.IsAsyncReady)
                                            {
                                                if (ap.IsAnimated)
                                                {
                                                    float atW = glTex.Size.Width;
                                                    float atH = glTex.Size.Height;
                                                    AssetPreview.FrameData frame = ap.GetCurrentFrame((int)(Client.Instance.Frontend.UpdatesExisted % (ulong)ap.FramesTotalDelay));
                                                    float sS = frame.X / atW;
                                                    float sE = sS + (frame.Width / atW);
                                                    float tS = frame.Y / atH;
                                                    float tE = tS + (frame.Height / atH);
                                                    idl.AddImage(ap.GLTex, cursor + pen, cursor + pen + sz, new Vec2(sS + (frame.Width / atW * 0.25f), tS), new Vec2(sE - (frame.Width / atW * 0.25f), tE), new Color(portrairColor).Abgr());
                                                }
                                                else
                                                {
                                                    idl.AddImage(ap.GLTex, cursor + pen, cursor + pen + sz, new Vec2(0.25f, 0), new Vec2(0.75f, 1), new Color(portrairColor).Abgr());
                                                }
                                            }
                                        }

                                        idl.AddImage(this.TurnTrackerForeground, cursor + pen, cursor + pen + sz, Vec2.Zero, Vec2.One, new Color(hColor).Abgr());
                                        if (ImGui.IsMouseHoveringRect(cursor + pen, cursor + pen + sz))
                                        {
                                            ImGui.BeginTooltip();
                                            ImGui.TextUnformatted(eName);
                                            ImGui.PushStyleColor(ImGuiCol.Text, tColor.Abgr());
                                            ImGui.TextUnformatted(tName);
                                            ImGui.PopStyleColor();
                                            ImGui.EndTooltip();
                                            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                                            {
                                                if (mo.MapLayer <= 0 && !mo.DoNotRender && !mo.HideFromSelection)
                                                {
                                                    if (Client.Instance.Frontend.GameHandle.IsAnyAltDown())
                                                    {
                                                        Ping p = new Ping() { DeathTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 10000, OwnerColor = Extensions.FromArgb(Client.Instance.Settings.Color), OwnerID = Client.Instance.ID, OwnerName = Client.Instance.Settings.Name, Position = mo.Position, Type = Ping.PingType.Generic };
                                                        new PacketPing() { Ping = p }.Send();
                                                    }   
                                                }
                                            }
                                        }

                                        pen.X += sz.X + 2;
                                        if (e == last)
                                        {
                                            idl.AddImage(this.TurnTrackerSeparator, cursor + pen, cursor + pen + separatorSize);
                                            pen.X += 14;
                                        }
                                    }
                                    else
                                    {
                                        idl.AddImage(this.TurnTrackerBackgroundNoObject, cursor + pen, cursor + pen + sz, Vec2.Zero, Vec2.One, tColor.Abgr());
                                    }
                                }

                                ImGui.PopClipRect();

                                /* Old Rendering code
                                ImGui.SetCursorPosX(320 - 40);
                                for (int idx = 0; idx <= 5; ++idx)
                                {
                                    TurnTracker.Entry e = cMap.TurnTracker.GetAt(cMap.TurnTracker.EntryIndex + idx);
                                    bool hasEntryInfo = cMap.TurnTracker.GetEntryInfo(e, out Color tColor, out string tName, out string eName);
                                    if (cMap.GetObject(e.ObjectID, out MapObject mo))
                                    {
                                        if (e == first)
                                        {
                                            ImGui.Image(this.TurnTrackerSeparator, new System.Numerics.Vector2(12, 96), System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, ((System.Numerics.Vector4)Color.Silver));
                                            ImGui.SameLine();
                                        }

                                        var ccXY = ImGui.GetCursorPos();
                                        System.Numerics.Vector2 cursorScreenC = ImGui.GetCursorScreenPos();
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

                                        Vector4 tclV4 = tColor.Vec4();
                                        if (e == currentEntry)
                                        {
                                            uint particleColor = tColor.Abgr();
                                            tclV4 = Vector4.Lerp(Color.Gold.Vec4(), Color.White.Vec4(), (1.0f + MathF.Sin(Client.Instance.Frontend.UpdatesExisted * MathF.PI / 180.0f)) / 2f);
                                            for (int i = this._particles.Count - 1; i >= 0; i--)
                                            {
                                                TurnTrackerParticle ttp = this._particles[i];
                                                if (ttp.IsDead)
                                                {
                                                    this._particles.RemoveAt(i);
                                                }
                                                else
                                                {
                                                    idl.AddImage(this.TurnTrackerParticle, ttp.RelativePosition + cursorScreenC - (new System.Numerics.Vector2(4, 4) * ttp.Scale), ttp.RelativePosition + cursorScreenC + (new System.Numerics.Vector2(4, 4) * ttp.Scale), System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, ttp.Color == 0xffffffffu ? particleColor : ttp.Color);
                                                }
                                            }
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

                                            cursorScreenC = ImGui.GetCursorScreenPos();
                                            ImGui.Image(ap.GLTex, new System.Numerics.Vector2(64, 96), new System.Numerics.Vector2(0.25f, 0), new System.Numerics.Vector2(0.75f, 1), portrairColor);
                                        }

                                        idl.AddImage(this.TurnTrackerForeground, cursorScreenC - new System.Numerics.Vector2(4, 4), cursorScreenC + new System.Numerics.Vector2(68, 100), System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, new Color(tclV4.SystemVector()).Abgr());
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

                                        System.Numerics.Vector2 cursorScreenC = ImGui.GetCursorScreenPos();
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

                                            cursorScreenC = ImGui.GetCursorScreenPos();
                                            ImGui.Image(ap.GLTex, new System.Numerics.Vector2(64, 96), new System.Numerics.Vector2(0.25f, 0), new System.Numerics.Vector2(0.75f, 1), portrairColor, borderColor);
                                        }

                                        idl.AddImage(this.TurnTrackerForeground, cursorScreenC - new System.Numerics.Vector2(4, 4), cursorScreenC + new System.Numerics.Vector2(68, 100), System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, tColor.Abgr());

                                        if (e == first)
                                        {
                                            cX -= 18;
                                            ImGui.SetCursorPosX(cX);
                                            ImGui.SetCursorPosY(cY);
                                            ImGui.Image(this.TurnTrackerSeparator, new System.Numerics.Vector2(12, 96), System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, ((System.Numerics.Vector4)Color.Silver));
                                        }
                                    }
                                    else
                                    {
                                        ImGui.Image(this.TurnTrackerBackgroundNoObject, new System.Numerics.Vector2(64, 96), System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, System.Numerics.Vector4.One);
                                    }

                                    cX -= 70;
                                }
                                */
                            }

                            float tW = ImGuiHelper.CalcTextSize(cMap.TurnTracker.EntryName).X;
                            ImGui.PushStyleColor(ImGuiCol.Text, (Vec4)cMap.TurnTracker.CurrentColor.Darker(0.3f));
                            if (Client.Instance.Settings.TextThickDropShadow)
                            {
                                for (int i = 0; i < 4; ++i)
                                {
                                    ImGui.SetCursorPosX(319 - (tW / 2) + ((i & 1) * 2));
                                    ImGui.SetCursorPosY(109 + ((i >> 1) * 2));
                                    ImGui.TextUnformatted(cMap.TurnTracker.EntryName);
                                }
                            }
                            else
                            {
                                ImGui.SetCursorPosX(321 - (tW / 2));
                                ImGui.SetCursorPosY(111);
                                ImGui.TextUnformatted(cMap.TurnTracker.EntryName);
                            }

                            ImGui.PopStyleColor();
                            ImGui.SetCursorPosX(320 - (tW / 2));
                            ImGui.SetCursorPosY(110);
                            ImGui.TextUnformatted(cMap.TurnTracker.EntryName);
                        }
                    }

                    ImGui.End();
                }

                ImGui.SetNextWindowPos(new((ww / 4) + 640, -12));
                ImGui.SetNextWindowBgAlpha(0.0f);
                ImGui.Begin("##TurnTrackerCollapseContainer", window_flags | ImGuiWindowFlags.NoBackground);
                ImGui.PushItemWidth(48);
                if (ImGui.ArrowButton("##TurnTrackerCollapseButton", this._turnTrackerCollapsed ? ImGuiDir.Down : ImGuiDir.Up))
                {
                    this._turnTrackerCollapsed = !this._turnTrackerCollapsed;
                }

                ImGui.PopItemWidth();

                if (!this._turnTrackerCollapsed && this._ttOffset != 0)
                {
                    ImGui.SetNextWindowPos(new((ww / 4) - 24, -12));
                    ImGui.SetNextWindowBgAlpha(0.0f);
                    ImGui.Begin("##TurnTrackerResetContainer", window_flags | ImGuiWindowFlags.NoBackground);
                    ImGui.PushItemWidth(48);
                    if (ImGui.Button("↻###TurnTrackerResetButton", new Vec2(24, 24)))
                    {
                        this._ttOffset = 0;
                    }

                    ImGui.PopItemWidth();
                    ImGui.End();
                }
            }

            this._scrollDYChange = 0;
        }

        private unsafe void UpdateTurnTrackerParticles()
        {
            for (int i = this._particles.Count - 1; i >= 0; i--)
            {
                TurnTrackerParticle ttp = this._particles[i];
                ttp.Update();
            }

            if (this._turnTrackerVisible && Client.Instance.Settings.TurnTrackerParticlesEnabled && Client.Instance.Settings.ParticlesEnabled)
            {
                int d = 0;
                int lt;
                TurnTrackerParticle ttp;
                while (this.Random.Next(3) == 0 && d++ < 32)
                {
                    bool isSpecial = this.Random.Next(12) == 0;
                    lt = 360 + this.Random.Next(60) + (isSpecial ? 120 : 0);
                    ttp = new TurnTrackerParticle()
                    {
                        Lifetime = lt,
                        LifetimeMax = lt,
                        Color = isSpecial ? Color.Gold.Abgr() : 0xffffffffu,
                        Motion = new Vec2(-0.05f + (this.Random.NextSingle() * 0.1f), -0.3f + (this.Random.NextSingle() * 0.1f)),
                        RelativePosition = new Vec2(4 + (this.Random.NextSingle() * 56), 92),
                        ScaleMultiplier = isSpecial ? 0.5f : 0.8f + (this.Random.NextSingle() * 0.4f),
                    };

                    this._particles.Add(ttp);
                }

                lt = 60 + this.Random.Next(60);
                ttp = new TurnTrackerParticle()
                {
                    Lifetime = lt,
                    LifetimeMax = lt,
                    Color = 0xffffffffu,
                    Motion = new Vec2(-0.05f + (this.Random.NextSingle() * 0.1f), -0.3f + (this.Random.NextSingle() * 0.1f)),
                    RelativePosition = new Vec2(4 + (this.Random.NextSingle() * 56), 92),
                    ScaleMultiplier = 1.0f + (this.Random.NextSingle() * 0.5f),
                };

                this._particles.Add(ttp);
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
                            if (ImGui.InputText("##TeamName" + i, ref tName, ushort.MaxValue))
                            {
                                if (!string.IsNullOrEmpty(tName))
                                {
                                    t.Name = tName;
                                    PacketTeamInfo pti = new PacketTeamInfo() { Action = PacketTeamInfo.ActionType.UpdateName, Index = i, Name = tName, Color = t.Color };
                                    pti.Send();
                                }
                            }

                            ImGui.SameLine();
                            if (ImGui.ColorButton("##TeamColor" + i, (Vec4)t.Color))
                            {
                                this._editedTeamName = t.Name;
                                this._editedTeamColor = (Vec4)t.Color;
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
                            ImGui.PushStyleColor(ImGuiCol.Border, (Vec4)Color.RoyalBlue);
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
                        Vec2 wC = ImGui.GetWindowSize();
                        ImGui.BeginChild("##TirnTrackerInnerEntryList");
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
                                Vec4 colorDark = new Vec4(0.234f, 0, 0.4f, 1.0f);
                                Vec4 colorBright = new Vec4(0.862f, 0, 0.292f, 1.0f);
                                Vec4 borderColor = System.Numerics.Vector4.Lerp(colorDark, colorBright, (1.0f + MathF.Sin(Client.Instance.Frontend.UpdatesExisted * MathF.PI / 180.0f)) / 2f);
                                ImGui.PushStyleColor(ImGuiCol.Border, borderColor);
                            }

                            ImGui.BeginChild("turnTrackerNav_" + i, new Vec2(wC.X - 32, 32), ImGuiChildFlags.Border, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings);
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
                                    cam.Position = cMap.Is2D ? new Vector3(p.X, p.Y, cam.Position.Z) : p - (cam.Direction * 5.0f);
                                    cam.RecalculateData();
                                    if (Client.Instance.Frontend.GameHandle.IsAnyShiftDown())
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
                                ImGui.ColorButton("##ClrBtnD" + i, (Vec4)e.Team.Color);
                                ImGui.SameLine();
                            }

                            float v = e.NumericValue;
                            ImGui.PushItemWidth(100);
                            if (ImGui.InputFloat("##Value" + e.ObjectID, ref v, 0, 0, "%.3f"))
                            {
                                e.NumericValue = v;
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

                        ImGui.EndChild();
                    }

                    ImGui.End();
                }
            }
        }
    }

    public class TurnTrackerParticle
    {
        public int Lifetime { get; set; }
        public int LifetimeMax { get; set; }
        public uint Color { get; set; }
        public bool IsDead => this.Lifetime < 0;
        public float ScaleMultiplier { get; set; } = 1;
        public float Scale => (float)this.Lifetime / this.LifetimeMax * this.ScaleMultiplier;

        public Vec2 RelativePosition { get; set; }
        public Vec2 Motion { get; set; }

        public void Update()
        {
            this.RelativePosition += this.Motion;
            --this.Lifetime;
        }
    }
}
