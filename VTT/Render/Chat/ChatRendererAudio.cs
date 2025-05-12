namespace VTT.Render.Chat
{
    using ImGuiNET;
    using System;
    using System.Numerics;
    using VTT.Control;
    using VTT.Network;
    using VTT.Render.Gui;
    using VTT.Sound;
    using VTT.Util;

    public class ChatRendererAudio : ChatRendererBase
    {
        private Guid _soundId = Guid.Empty;

        public ChatRendererAudio(ChatLine container) : base(container)
        {
        }

        public override void Cache(Vector2 windowSize, out float width, out float height)
        {
            width = 320;
            height = 40;
        }

        public override void ClearCache()
        {
        }

        public override void Render(Guid senderId, uint senderColorAbgr)
        {
            GuiRenderer uiRoot = GuiRenderer.Instance;
            Vector2 lHere = ImGui.GetCursorScreenPos() + new Vector2(24, 8);
            Vector2 here = ImGui.GetCursorPos();
            bool isPlaying = !this._soundId.IsEmpty();
            AssetSound sound = null;
            bool haveSound = isPlaying && Client.Instance.Frontend.Sound.TryGetAssetSound(this._soundId, out sound);
            if (isPlaying && !haveSound)
            {
                this._soundId = Guid.Empty;
                isPlaying = false;
            }

            ImGui.SetCursorPos(here + new Vector2(24, 8));
            if (ImGui.InvisibleButton("PlaySoundAssetOfLine_" + this.Container.Index, new Vector2(48, 20)))
            {
                if (!isPlaying)
                {
                    if (this.Container.TryGetBlockAt(0, out ChatBlock cb) && Guid.TryParse(cb.Text, out Guid assetID))
                    {
                        this._soundId = Client.Instance.Frontend.Sound.PlayAsset(assetID);
                    }
                }
                else
                {
                    if (haveSound && sound != null)
                    {
                        Client.Instance.Frontend.Sound.StopAsset(sound.AssetID);
                        this._soundId = Guid.Empty;
                    }
                    else
                    {
                        this._soundId = Guid.Empty;
                    }
                }
            }

            bool active = ImGui.IsItemActive();
            bool hover = ImGui.IsItemHovered();

            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(lHere, lHere + new Vector2(48, 20), ImGui.GetColorU32(active ? ImGuiCol.ButtonActive : hover ? ImGuiCol.ButtonHovered : ImGuiCol.Button), 20f);
            drawList.AddImage(isPlaying ? uiRoot.PlayerStop : uiRoot.PlayIcon, lHere, lHere + new Vector2(20, 20));

            float fillVal = 0;
            double timeC = 0;
            double timeF = 0;
            if (isPlaying && haveSound)
            {
                if (Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(sound.AssetID, Asset.AssetType.Sound, out Asset.Asset a) == Asset.AssetStatus.Return && a.Type == Asset.AssetType.Sound && a.Sound?.Meta != null)
                {
                    fillVal = (float)(sound.SecondsPlayed / a.Sound.Meta.TotalDuration);
                    timeC = sound.SecondsPlayed;
                    timeF = a.Sound.Meta.TotalDuration;
                    if (float.IsNaN(fillVal) || float.IsInfinity(fillVal))
                    {
                        fillVal = 0;
                    }

                    if (double.IsNaN(timeC) || double.IsInfinity(timeC))
                    {
                        timeC = 0;
                    }

                    if (double.IsNaN(timeF) || double.IsInfinity(timeF))
                    {
                        timeF = 0;
                    }

                    timeC = Math.Max(timeC, 0);
                    timeF = Math.Max(timeF, 0);
                }
            }

            string timeVal = $"{TimeSpan.FromSeconds(timeC):hh\\:mm\\:ss}/{TimeSpan.FromSeconds(timeF):hh\\:mm\\:ss}";
            fillVal = Math.Clamp(fillVal, 0, 1);
            Vector2 avail = ImGui.GetContentRegionAvail();
            float tw = MathF.Max(100, avail.X - 76);
            drawList.AddRectFilled(lHere + new Vector2(24, 0), lHere + new Vector2(tw, 20), ImGui.GetColorU32(ImGuiCol.FrameBg));
            drawList.AddRectFilled(lHere + new Vector2(26, 2), lHere + new Vector2(26, 2) + new Vector2((tw - 26) * fillVal, 16), ImGui.GetColorU32(ImGuiCol.ButtonActive));
            Vector2 musicValSize = ImGui.CalcTextSize(timeVal);
            drawList.AddText(lHere + new Vector2((tw / 2) - (musicValSize.X / 2), 10 - (musicValSize.Y / 2)), ImGui.GetColorU32(ImGuiCol.Text), timeVal);
            ImGui.SetCursorPos(here);
            ImGui.Dummy(new Vector2(320, 40));
        }

        public override string ProvideTextForClipboard(DateTime dateTime, string senderName, SimpleLanguage lang) => string.Empty;
    }
}
