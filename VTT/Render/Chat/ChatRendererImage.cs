namespace VTT.Render.Chat
{
    using ImGuiNET;
    using System;
    using System.Numerics;
    using VTT.Asset;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public class ChatRendererImage : ChatRendererBase
    {
        public bool IsPaused { get; set; }
        public ulong PauseFrame { get; set; }

        public ChatRendererImage(ChatLine container) : base(container)
        {
        }

        public override void Cache(Vector2 windowSize, out float width, out float height)
        {
            try
            {
                Vector2 max = ImGui.GetContentRegionAvail();
                int w = int.Parse(this.Container.Blocks[0].Text);
                int h = int.Parse(this.Container.Blocks[1].Text);
                float baseAR = (float)w / h;
                int oW = w;
                w = (int)Math.Min(w, max.X);
                float newAr = (float)w / h;
                if (MathF.Abs(newAr - baseAR) > 1e-7)
                {
                    h = (int)(h / ((float)oW / w));
                }

                width = w;
                height = h;
            }
            catch
            {
                //NOOP
                width = 32;
                height = 32;
            }
        }

        public override void ClearCache()
        {
        }

        public override void Render(Guid senderId, uint senderColorAbgr)
        {
            Vector2 max = ImGui.GetContentRegionAvail();
            int w = int.Parse(this.Container.Blocks[0].Text);
            int h = int.Parse(this.Container.Blocks[1].Text);
            float baseAR = (float)w / h;
            int oW = w;
            w = (int)Math.Min(w, max.X);
            float newAr = (float)w / h;
            if (MathF.Abs(newAr - baseAR) > 1e-7)
            {
                h = (int)(h / ((float)oW / w));
            }

            string url = this.Container.Blocks[2].Text;
            AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.WebPictures.Get(url, AssetType.Texture, out AssetPreview ap);
            if (status == AssetStatus.Return && ap != null && ap.GLTex != null)
            {
                if (ap.IsAnimated && ap.FramesTotalDelay > 0)
                {
                    Vector2 imPos = ImGui.GetCursorPos();
                    float tW = ap.GLTex.Size.Width;
                    float tH = ap.GLTex.Size.Height;
                    AssetPreview.FrameData frame = ap.GetCurrentFrame((int)(((this.IsPaused ? this.PauseFrame : Client.Instance.Frontend.UpdatesExisted) & int.MaxValue) * (100f / 60f)));
                    float progress = (float)frame.TotalDurationToHere / ap.FramesTotalDelay;
                    float sS = frame.X / tW;
                    float sE = sS + (frame.Width / tW);
                    float tS = frame.Y / tH;
                    float tE = tS + (frame.Height / tH);
                    ImGui.Image(ap.GLTex, new Vector2(w, h), new Vector2(sS, tS), new Vector2(sE, tE));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 8);
                        ImGui.ProgressBar(progress, new Vector2(w, 4));
                    }

                    Vector2 imEnd = ImGui.GetCursorPos();
                    ImGui.SetCursorPos(imPos);
                    ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                    Texture btnTex = this.IsPaused ? Client.Instance.Frontend.Renderer.GuiRenderer.PlayIcon : Client.Instance.Frontend.Renderer.GuiRenderer.PauseIcon;
                    if (ImGui.ImageButton("btnPauseChatAnimation_" + this.Container.Index, btnTex, new Vector2(20, 20), Vector2.Zero, Vector2.One, Vector4.Zero))
                    {
                        this.IsPaused = !this.IsPaused;
                        this.PauseFrame = Client.Instance.Frontend.UpdatesExisted;
                    }

                    ImGui.PopStyleColor();
                    ImGui.SetCursorPos(imEnd);
                }
                else
                {
                    ImGui.Image(ap.GLTex, new Vector2(Math.Min(w, max.X), h));
                }

                if (!string.IsNullOrEmpty(this.Container.Blocks[2].Tooltip) && ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(this.Container.Blocks[2].Tooltip);
                }
            }

            if (status == AssetStatus.Error)
            {
                ImGui.Image(Client.Instance.Frontend.Renderer.GuiRenderer.NoImageIcon, new Vector2(w, h));
            }

            if (status == AssetStatus.Await)
            {
                int frame =
                    (int)((int)Client.Instance.Frontend.UpdatesExisted % 90 / 90.0f * Client.Instance.Frontend.Renderer.GuiRenderer.LoadingSpinnerFrames);
                float texelIndexStart = (float)frame / Client.Instance.Frontend.Renderer.GuiRenderer.LoadingSpinnerFrames;
                float texelSize = 1f / Client.Instance.Frontend.Renderer.GuiRenderer.LoadingSpinnerFrames;
                ImGui.Image(Client.Instance.Frontend.Renderer.GuiRenderer.LoadingSpinner, new Vector2(w, h), new Vector2(texelIndexStart, 0), new Vector2(texelIndexStart + texelSize, 1), new Vector4(1, 1, 1, 1), new Vector4(0, 0, 0, 0));
            }
        }

        public override string ProvideTextForClipboard(DateTime dateTime, string senderName, SimpleLanguage lang) => this.Container.Blocks[2].Text;
    }
}
