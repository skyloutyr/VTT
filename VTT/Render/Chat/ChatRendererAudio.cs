namespace VTT.Render.Chat
{
    using ImGuiNET;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Numerics;
    using VTT.Control;
    using VTT.Network;
    using VTT.Render.Gui;
    using VTT.Sound;
    using VTT.Util;

    public class ChatRendererAudio : ChatRendererBase
    {
        private Guid _soundId = Guid.Empty;
        private SoundSourceContainer _directSound = null;
        private static readonly Dictionary<string, ALSoundContainer> embedSoundBank = new Dictionary<string, ALSoundContainer>();
        private double _currentDirectSoundDuration = 0;

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
            bool isPlaying = !this._soundId.IsEmpty() || this._directSound != null;
            AssetSound sound = null;
            bool haveSoundAsset = isPlaying && Client.Instance.Frontend.Sound.TryGetAssetSound(this._soundId, out sound);
            if (isPlaying && !this._soundId.IsEmpty() && !haveSoundAsset)
            {
                this._soundId = Guid.Empty;
            }

            ImGui.SetCursorPos(here + new Vector2(24, 8));
            if (ImGui.InvisibleButton("PlaySoundAssetOfLine_" + this.Container.Index, new Vector2(48, 20)))
            {
                if (!isPlaying)
                {
                    if (this.Container.TryGetBlockAt(0, out ChatBlock cb))
                    {
                        if (Guid.TryParse(cb.Text, out Guid assetID))
                        {
                            this._soundId = Client.Instance.Frontend.Sound.PlayAsset(assetID);
                        }
                        else
                        {
                            bool isB64 = cb.Text.Length >= 89 && Base64CheckerRegex.IsMatch(cb.Text[..38]);
                            if (isB64)
                            {
                                if (!embedSoundBank.TryGetValue(cb.Text, out ALSoundContainer sc))
                                {
                                    // The problem here becomes that we need to identify MP3 vs WAV vs OGG sound files...
                                    byte[] data = Convert.FromBase64String(cb.Text);
                                    using MemoryStream ms = new MemoryStream(data);
                                    bool isMp3 = WaveAudio.ValidateMPEGFrame(ms);
                                    ms.Seek(0, SeekOrigin.Begin);
                                    bool isOgg = WaveAudio.ValidateOGGHeader(ms);
                                    ms.Seek(0, SeekOrigin.Begin);
                                    bool isWav = WaveAudio.ValidateRIFFHeader(ms);
                                    ms.Seek(0, SeekOrigin.Begin);
                                    if (isMp3 || isOgg || isWav)
                                    {
                                        WaveAudio wa = null;
                                        if (isWav)
                                        {
                                            wa = new WaveAudio();
                                            wa.Load(ms);
                                        }

                                        if (isMp3)
                                        {
                                            using NLayer.MpegFile mpeg = new NLayer.MpegFile(ms);
                                            wa = new WaveAudio(mpeg);
                                        }

                                        if (isOgg)
                                        {
                                            using NVorbis.VorbisReader vr = new NVorbis.VorbisReader(ms);
                                            wa = new WaveAudio(vr);
                                        }

                                        if (wa != null)
                                        {
                                            // Yes, this calls a callback from a callback from a callback from a callback
                                            // Yes, the above sentence is 100% correct
                                            // Multithreading moment 
                                            Client.Instance.Frontend.Sound.LoadSoundContainerAsync(embedSoundBank[cb.Text] = new ALSoundContainer(), wa, x =>
                                            {
                                                Client.Instance.Frontend.Sound.PlaySound(x, SoundCategory.Unknown, (con, obj) =>
                                                {
                                                    this._directSound = obj;
                                                    obj.TrackInfo = true;
                                                    obj.StoppedCallback = x => Client.Instance.DoTask(() => this._directSound = null);
                                                });

                                                this._currentDirectSoundDuration = wa.Duration; // Safe here, don't care for multithreading nonsense and will always stay constant
                                            });
                                        }
                                    }
                                }
                                else
                                {
                                    Client.Instance.Frontend.Sound.PlaySound(sc, SoundCategory.Unknown, (con, obj) =>
                                    {
                                        this._directSound = obj;
                                        obj.TrackInfo = true;
                                        obj.StoppedCallback = x => Client.Instance.DoTask(() => this._directSound = null);
                                    });

                                    this._currentDirectSoundDuration = sc.WaveData.Duration; // Safe here, don't care for multithreading nonsense and will always stay constant
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (haveSoundAsset)
                    {
                        if (sound != null)
                        {
                            Client.Instance.Frontend.Sound.StopAsset(sound.AssetID);
                            this._soundId = Guid.Empty;
                        }
                        else
                        {
                            this._soundId = Guid.Empty;
                        }
                    }
                    else
                    {
                        if (this._directSound != null)
                        {
                            Client.Instance.Frontend.Sound.StopBasicSound(this._directSound);
                            this._directSound = null;
                        }

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
            if (isPlaying && haveSoundAsset)
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

            if (isPlaying && !haveSoundAsset && this._directSound != null)
            {
                timeF = this._currentDirectSoundDuration;
                timeC = this._directSound.SecondsPlayed;
                fillVal = (float)(timeC / timeF);
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
