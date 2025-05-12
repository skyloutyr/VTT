namespace VTT.Render.Gui
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Numerics;
    using VTT.Asset;
    using VTT.Control;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public partial class GuiRenderer
    {
        private int _lastSelectedTrackIndex = -1;
        private bool _isDraggingTrack;
        private bool _dragLmbStatus;
        private Vector2 _dragMouseStartingPos;
        private (Guid, int, string) _draggedTrackData;

        private unsafe void RenderMusicPlayer(SimpleLanguage lang, GuiState state, double delta)
        {
            void DrawScrollingText(ImDrawListPtr idlp, Vector2 pos, Vector2 rect, string text, bool addDummy)
            {
                Vector2 textSize = ImGuiHelper.CalcTextSize(text);
                idlp.PushClipRect(pos, pos + rect);
                if (textSize.X <= rect.X)
                {
                    idlp.AddText(new Vector2(pos.X + (rect.X / 2) - (textSize.X / 2), pos.Y + (rect.Y / 2) - (textSize.Y / 2)), ImGui.GetColorU32(ImGuiCol.Text), text);
                }
                else
                {
                    float oX = (float)((uint)Client.Instance.Frontend.UpdatesExisted + delta);
                    //float missingPortion = textSize.X - rect.X + 64;
                    oX %= textSize.X + 32;

                    idlp.AddText(new Vector2(pos.X - oX, pos.Y + (rect.Y / 2) - (textSize.Y / 2)), ImGui.GetColorU32(ImGuiCol.Text), text);
                    idlp.AddText(new Vector2(pos.X + 32 + textSize.X - oX, pos.Y + (rect.Y / 2) - (textSize.Y / 2)), ImGui.GetColorU32(ImGuiCol.Text), text);
                }

                idlp.PopClipRect();
                if (addDummy)
                {
                    ImGui.Dummy(rect);
                }
            }

            Vector2 winContentRegion = ImGui.GetContentRegionAvail();
            float paddingX = ImGui.GetStyle().FramePadding.X;
            if (ImGui.Begin(lang.Translate("ui.music_player") + "###MusicPlayer"))
            {
                MusicPlayer mp = Client.Instance.Frontend?.Sound?.MusicPlayer;
                if (mp != null)
                {
                    string musicName;
                    string musicDuration;
                    float musicDurationPercentage;
                    Asset a;
                    AssetStatus aStatus;
                    if (mp.CurrentTrackPosition != -1)
                    {
                        (Guid, float) d = mp.Tracks[mp.CurrentTrackPosition];
                        aStatus = Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(d.Item1, AssetType.Sound, out a);
                        float sNow = float.NaN;
                        float sMax = float.NaN;
                        if (aStatus == AssetStatus.Return && a?.Type == AssetType.Sound && a?.Sound?.Meta != null)
                        {
                            AssetRef aref = Client.Instance.IsAdmin ? Client.Instance.AssetManager.FindRefForAsset(a) : null;
                            if (aref != null)
                            {
                                musicName = aref.Name;
                                sMax = (float)(aref.Meta?.SoundInfo?.TotalDuration ?? float.NaN);
                            }
                            else
                            {
                                musicName = !string.IsNullOrEmpty((a?.Sound?.Meta?.SoundAssetName ?? string.Empty).Trim())
                                    ? a.Sound.Meta.SoundAssetName
                                    : lang.Translate("ui.music_player.name_unknown", d.Item1.ToString());

                                sMax = (float)(a?.Sound?.Meta?.TotalDuration ?? float.NaN);
                            }
                        }
                        else
                        {
                            musicName = lang.Translate("ui.music_player.name_unknown", d.Item1.ToString());
                        }

                        if (Client.Instance.Frontend.Sound?.TryGetAssetSound(mp.CurrentSoundID, out Sound.AssetSound asound) ?? false)
                        {
                            if (asound != null)
                            {
                                sNow = asound.SecondsPlayed;
                            }
                        }

                        musicDurationPercentage = float.IsNaN(sNow) || float.IsNaN(sMax) ? 0 : MathF.Abs(sMax) < 0.00001f ? 0 : MathF.Min(sNow / sMax, 1);
                        if (float.IsNaN(sNow) && float.IsNaN(sMax))
                        {
                            musicDuration = " ";
                        }
                        else
                        {
                            if (float.IsNaN(sNow))
                            {
                                TimeSpan ts = TimeSpan.FromSeconds(sMax);
                                musicDuration = $"{lang.Translate("ui.music_player.duration.unknown")}/{ts:hh\\:mm\\:ss}";
                            }
                            else
                            {
                                if (float.IsNaN(sMax) || MathF.Abs(sMax) < 0.00001f)
                                {
                                    TimeSpan ts = TimeSpan.FromSeconds(sNow);
                                    musicDuration = $"{ts:hh\\:mm\\:ss}/{lang.Translate("ui.music_player.duration.unknown")}";
                                }
                                else
                                {
                                    TimeSpan tsn = TimeSpan.FromSeconds(sNow);
                                    TimeSpan tsm = TimeSpan.FromSeconds(sMax);
                                    musicDuration = $"{tsn:hh\\:mm\\:ss}/{tsm:hh\\:mm\\:ss}";
                                }
                            }
                        }
                    }
                    else
                    {
                        musicName = lang.Translate("ui.music_player.no_music");
                        musicDuration = " ";
                        musicDurationPercentage = 0;
                        a = null;
                        aStatus = AssetStatus.NoAsset;
                    }

                    DrawScrollingText(ImGui.GetWindowDrawList(), ImGui.GetCursorScreenPos(), new Vector2(390, 32), musicName, true);
                    ImGui.ProgressBar(musicDurationPercentage, new Vector2(winContentRegion.X - paddingX, 16), " ");
                    Vector2 musicNameSize = ImGuiHelper.CalcTextSize(musicDuration);
                    ImGui.SetCursorPosX((winContentRegion.X / 2) - (musicNameSize.X / 2));
                    ImGui.TextUnformatted(musicDuration);
                    if (Client.Instance.IsAdmin)
                    {
                        ImGui.NewLine();
                        if (this.PlayerStop.ImImageButton("MusicPlayerStop", new Vector2(24, 24)))
                        {
                            new PacketMusicPlayerSetIndex() { Index = -1 }.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.music_player.stop.tt"));
                        }

                        ImGui.SameLine();
                        if (this.PlayIcon.ImImageButton("MusicPlayerPlay", new Vector2(24, 24)))
                        {
                            new PacketMusicPlayerSetIndex() { Index = this._lastSelectedTrackIndex }.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.music_player.play.tt"));
                        }

                        ImGui.SameLine();
                        if (this.PlayerNext.ImImageButton("MusicPlayerNext", new Vector2(24, 24)))
                        {
                            new PacketMusicPlayerAction() { ActionType = PacketMusicPlayerAction.Type.ForceNext }.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.music_player.next.tt"));
                        }

                        ImGui.SameLine();
                        string[] texts = { lang.Translate("ui.music_player.loop.none"), lang.Translate("ui.music_player.loop.loop"), lang.Translate("ui.music_player.loop.single"), lang.Translate("ui.music_player.loop.random") };
                        int i = (int)mp.RepeatState;
                        ImGui.SetNextItemWidth(160);
                        if (ImGui.Combo("##MusicPlayerPlayMode", ref i, texts, 4))
                        {
                            new PacketMusicPlayerAction() { ActionType = PacketMusicPlayerAction.Type.SetMode, LoopMode = (MusicPlayer.LoopMode)i }.Send();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.music_player.loop.tt"));
                        }

                        string volumeText = lang.Translate("ui.music_player.volume");
                        ImGui.TextUnformatted(volumeText);
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(winContentRegion.X - paddingX - ImGuiHelper.CalcTextSize(volumeText).X - 16);
                        float fPV = mp.Volume;
                        if (ImGui.SliderFloat("###MusicPlayerVolumeSlider", ref fPV, 0, 1))
                        {
                            fPV = Math.Clamp(fPV, 0, 1);
                            mp.Volume = fPV;
                            new PacketMusicPlayerAction() { ActionType = PacketMusicPlayerAction.Type.PlayerVolumeChange, IndexMain = -1, Data = (Guid.Empty, fPV) }.Send();
                        }

                        ImGui.NewLine();
                        if (ImGui.BeginChild("MusicPlayerTracks", new Vector2(winContentRegion.X - paddingX, 0), ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.Borders, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking))
                        {
                            i = 0;
                            Vector4 clrInactiveV = *ImGui.GetStyleColorVec4(ImGuiCol.FrameBg);
                            Vector4 clrActiveV = *ImGui.GetStyleColorVec4(ImGuiCol.FrameBgActive);
                            clrActiveV = Vector4.Lerp(clrInactiveV, clrActiveV, MathF.Abs(MathF.Sin(
                                ((int)Client.Instance.Frontend.UpdatesExisted + (float)delta) % 360f * MathF.PI / 180.0f
                            )));

                            uint clrInactive = new Color(clrInactiveV).Abgr();
                            uint clrActive = new Color(clrActiveV).Abgr();
                            int idxToRemove = -1;
                            lock (mp.@lock)
                            {
                                foreach ((Guid, float) track in mp.Tracks)
                                {
                                    Guid aId = track.Item1;
                                    AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(aId, AssetType.Sound, out a);
                                    string soundName = lang.Translate("ui.music_player.name_unknown", track.Item1);
                                    ImCustomTexturedRect icon = this.AssetMusicIcon;
                                    ImCustomTexturedRect assetTypeIcon = this.AssetMusicIcon;
                                    if (status == AssetStatus.Return)
                                    {
                                        AssetRef aref = Client.Instance.AssetManager.FindRefForAsset(a);
                                        if (aref != null)
                                        {
                                            soundName = aref.Name;
                                            if (aref.Meta.SoundInfo != null)
                                            {
                                                assetTypeIcon =
                                                    aref.Meta.SoundInfo.IsFullData ? this.AssetSoundIcon :
                                                    aref.Meta.SoundInfo.SoundType == SoundData.Metadata.StorageType.Mpeg ? this.AssetCompressedMusicIcon :
                                                    this.AssetMusicIcon;
                                            }
                                        }
                                        else
                                        {
                                            if (!string.IsNullOrEmpty((a?.Sound?.Meta?.SoundAssetName ?? string.Empty).Trim()))
                                            {
                                                soundName = a.Sound.Meta.SoundAssetName;
                                            }

                                            if (a?.Sound?.Meta != null)
                                            {
                                                assetTypeIcon =
                                                    a.Sound.Meta.IsFullData ? this.AssetSoundIcon :
                                                    a.Sound.Meta.SoundType == SoundData.Metadata.StorageType.Mpeg ? this.AssetCompressedMusicIcon :
                                                    this.AssetMusicIcon;
                                            }
                                        }

                                        status = Client.Instance.AssetManager.ClientAssetLibrary.Previews.Get(aId, AssetType.Texture, out AssetPreview preview);
                                        if (status == AssetStatus.Return && preview != null)
                                        {
                                            icon = ImCustomTexturedRect.WrapCustomTexture(preview.GetGLTexture());
                                        }
                                    }

                                    ImDrawListPtr idlp = ImGui.GetWindowDrawList();
                                    Vector2 cPos = ImGui.GetCursorScreenPos();
                                    bool hover = ImGui.IsMouseHoveringRect(cPos, cPos + new Vector2(360, 32));
                                    bool selected = this._lastSelectedTrackIndex == i;
                                    bool playing = mp.CurrentTrackPosition == i;

                                    idlp.AddRectFilledMultiColor(
                                        cPos, cPos + new Vector2(360, 32),
                                        clrInactive, clrInactive,
                                        playing ? clrActive : clrInactive, playing ? clrActive : clrInactive
                                    );

                                    idlp.AddRect(cPos, cPos + new Vector2(360, 32), ImGui.GetColorU32(
                                        hover ? ImGuiCol.ButtonHovered :
                                        selected ? ImGuiCol.ButtonActive :
                                        ImGuiCol.Button
                                    ));

                                    idlp.PushClipRect(cPos, cPos + new Vector2(360, 32));
                                    idlp.AddImage(icon, cPos + new Vector2(4, 4), cPos + new Vector2(28, 28));
                                    idlp.AddImage(assetTypeIcon, cPos + new Vector2(20, 20), cPos + new Vector2(32, 32));
                                    DrawScrollingText(idlp, cPos + new Vector2(32, 0), new Vector2(360 - 32, 32), soundName, false);
                                    idlp.PopClipRect();

                                    if (this._isDraggingTrack && hover)
                                    {
                                        float deltaY = cPos.Y + 16 - ImGui.GetMousePos().Y;
                                        uint clrOrange = ColorAbgr.Orange;
                                        if (deltaY > 0)
                                        {
                                            idlp.AddLine(cPos - new Vector2(0, 2), cPos + new Vector2(380, -2), clrOrange);
                                        }
                                        else
                                        {
                                            idlp.AddLine(cPos + new Vector2(0, 33), cPos + new Vector2(380, 33), clrOrange);
                                        }

                                        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
                                        {
                                            int idxTo = deltaY > 0 ? i : i + 1;
                                            if (idxTo != this._draggedTrackData.Item2)
                                            {
                                                new PacketMusicPlayerAction() { ActionType = PacketMusicPlayerAction.Type.Move, IndexMain = this._draggedTrackData.Item2, IndexMoveTo = idxTo, Data = (this._draggedTrackData.Item1, 1) }.Send();
                                                this._dragLmbStatus = false;
                                                this._dragMouseStartingPos = default;
                                                this._draggedTrackData = default;
                                                this._isDraggingTrack = false;
                                            }
                                        }
                                    }

                                    if (hover && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                                    {
                                        this._lastSelectedTrackIndex = i;
                                    }

                                    if (hover && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                                    {
                                        idxToRemove = i;
                                    }

                                    if (hover && !this._isDraggingTrack)
                                    {
                                        ImGui.SetTooltip(soundName);
                                        if (!this._isDraggingTrack && ImGui.IsMouseDown(ImGuiMouseButton.Left))
                                        {
                                            cPos = ImGui.GetMousePos();
                                            if (!this._dragLmbStatus)
                                            {
                                                this._dragLmbStatus = true;
                                                this._dragMouseStartingPos = cPos;
                                            }
                                            else
                                            {
                                                if ((cPos - this._dragMouseStartingPos).Length() >= 4)
                                                {
                                                    this._isDraggingTrack = true;
                                                    this._draggedTrackData = (aId, i, soundName);
                                                }
                                            }
                                        }
                                    }

                                    ImGui.Dummy(new Vector2(380, 32));

                                    ++i;
                                }
                            }

                            if (idxToRemove != -1)
                            {
                                new PacketMusicPlayerAction() { ActionType = PacketMusicPlayerAction.Type.Remove, IndexMain = idxToRemove }.Send();
                                idxToRemove = -1;
                            }
                        }

                        ImGui.EndChild();

                        bool mouseOverRecepticle = ImGuiHelper.ImAssetRecepticleCustomText(lang.Translate("ui.music_player.add_track"), this.AssetMusicIcon, new Vector2(0, 28), static x => x.Type == AssetType.Sound, out bool musicAddTrackHovered);
                        if (mouseOverRecepticle && this._draggedRef?.Type == AssetType.Sound)
                        {
                            state.movingAssetOverMusicPlayerAddPoint = true;
                        }

                        if (musicAddTrackHovered)
                        {
                            ImGui.SetTooltip(lang.Translate("ui.music_player.add_track.tt"));
                        }

                        mouseOverRecepticle = ImGuiHelper.ImAssetRecepticleCustomText(lang.Translate("ui.music_player.remove_track"), this.AssetMusicIcon, new Vector2(0, 28), static x => true, out bool musicRemoveTrackHovered);
                        if (mouseOverRecepticle)
                        {
                            if (this._dragLmbStatus && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
                            {
                                this._dragLmbStatus = false;
                                this._dragMouseStartingPos = default;
                                if (mp.Tracks.Count > this._draggedTrackData.Item2)
                                {
                                    new PacketMusicPlayerAction() { ActionType = PacketMusicPlayerAction.Type.Remove, IndexMain = this._draggedTrackData.Item2 }.Send();
                                }

                                this._draggedTrackData = default;
                                this._isDraggingTrack = false;
                            }
                        }

                        if (musicRemoveTrackHovered)
                        {
                            ImGui.SetTooltip(lang.Translate("ui.music_player.remove_track.tt"));
                        }

                        if (this._dragLmbStatus && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
                        {
                            this._dragLmbStatus = false;
                            this._dragMouseStartingPos = default;
                            this._draggedTrackData = default;
                            this._isDraggingTrack = false;
                        }
                    }
                }
            }

            ImGui.End();

            if (this._isDraggingTrack)
            {
                ImGui.SetNextWindowBgAlpha(0.5f);
                ImGui.SetNextWindowPos(new(Client.Instance.Frontend.MouseX, Client.Instance.Frontend.MouseY));
                if (ImGui.Begin("###PopupDraggedTrackSubWindow", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoTitleBar))
                {
                    AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(this._draggedTrackData.Item1, AssetType.Sound, out Asset a);
                    string soundName = this._draggedTrackData.Item3;
                    ImCustomTexturedRect icon = this.AssetMusicIcon;
                    ImCustomTexturedRect assetTypeIcon = this.AssetMusicIcon;
                    if (status == AssetStatus.Return)
                    {
                        AssetRef aref = Client.Instance.AssetManager.FindRefForAsset(a);
                        if (aref != null)
                        {
                            soundName = aref.Name;
                            if (aref.Meta.SoundInfo != null)
                            {
                                assetTypeIcon =
                                    aref.Meta.SoundInfo.IsFullData ? this.AssetSoundIcon :
                                    aref.Meta.SoundInfo.SoundType == SoundData.Metadata.StorageType.Mpeg ? this.AssetCompressedMusicIcon :
                                    this.AssetMusicIcon;
                            }
                        }

                        status = Client.Instance.AssetManager.ClientAssetLibrary.Previews.Get(this._draggedTrackData.Item1, AssetType.Texture, out AssetPreview preview);
                        if (status == AssetStatus.Return && preview != null)
                        {
                            icon = ImCustomTexturedRect.WrapCustomTexture(preview.GetGLTexture());
                        }
                    }

                    ImDrawListPtr idlp = ImGui.GetWindowDrawList();
                    Vector2 cPos = ImGui.GetCursorScreenPos();
                    idlp.AddRectFilled(cPos, cPos + new Vector2(360, 32), ImGui.GetColorU32(ImGuiCol.FrameBg));
                    idlp.AddRect(cPos, cPos + new Vector2(360, 32), ImGui.GetColorU32(ImGuiCol.Button));
                    idlp.PushClipRect(cPos, cPos + new Vector2(360, 32));
                    idlp.AddImage(icon, cPos + new Vector2(4, 4), cPos + new Vector2(28, 28));
                    idlp.AddImage(assetTypeIcon, cPos + new Vector2(20, 20), cPos + new Vector2(32, 32));
                    DrawScrollingText(idlp, cPos + new Vector2(32, 0), new Vector2(360 - 32, 32), soundName, false);
                    idlp.PopClipRect();
                    ImGui.Dummy(new Vector2(390, 32));
                }

                ImGui.End();
            }
        }
    }
}
