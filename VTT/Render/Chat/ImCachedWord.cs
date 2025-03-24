namespace VTT.Render.Chat
{
    using System;
    using System.Numerics;
    using VTT.Asset;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;

    public readonly struct ImCachedWord
    {
        public ChatBlock Owner { get; }
        public string Text { get; }
        public float Width { get; }
        public float Height { get; }
        public Vector2 TextSize { get; }
        public bool IsExpression { get; }
        public AssetStatus ImgStatusOnConstruct { get; }

        public ImCachedWord(ChatBlock owner, string text, float minWidth = 0, float minHeight = 0)
        {
            this.Owner = owner;
            this.Text = text;
            if (owner.Type == ChatBlockType.Image)
            {
                this.IsExpression = false;
                bool isAssetRef = Guid.TryParse(this.Owner.Text, out Guid imgAssetId);
                AssetPreview ap = null;
                Asset a = null;
                AssetStatus imgStatus = this.ImgStatusOnConstruct = isAssetRef
                    ? Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(imgAssetId, AssetType.Texture, out a) 
                    : Client.Instance.AssetManager.ClientAssetLibrary.WebPictures.Get(this.Owner.Text, AssetType.Texture, out ap);
                if (imgStatus == AssetStatus.Return)
                {
                    if (isAssetRef)
                    {
                        if (a.Type == AssetType.Texture && a.Texture != null)
                        {
                            Texture tex = a.Texture.GetOrCreateGLTexture(true, out VTT.Asset.Glb.TextureAnimation animData);
                            if (tex != null && tex.IsAsyncReady)
                            {
                                if (animData != null && animData.Frames.Length > 1)
                                {
                                    int wMax = 0;
                                    int hMax = 0;
                                    foreach (VTT.Asset.Glb.TextureAnimation.Frame frame in animData.Frames)
                                    {
                                        wMax = Math.Max(wMax, (int)frame.Location.Width * tex.Size.Width);
                                        hMax = Math.Max(wMax, (int)frame.Location.Height * tex.Size.Height);
                                    }

                                    this.Width = MathF.Min(320, wMax);
                                    this.Height = MathF.Min(320, hMax);
                                }
                                else
                                {
                                    this.Width = MathF.Min(320, tex.Size.Width);
                                    this.Height = MathF.Min(320, tex.Size.Height);
                                }
                            }
                            else
                            {
                                this.Width = 24;
                                this.Height = 24;
                            }
                        }
                        else
                        {
                            this.Width = 24;
                            this.Height = 24;
                        }
                    }
                    else
                    {
                        if (ap.GLTex != null && ap.GLTex.IsAsyncReady)
                        {
                            if (ap.IsAnimated && ap.FramesTotalDelay > 0)
                            {
                                int wMax = 0;
                                int hMax = 0;
                                foreach (AssetPreview.FrameData frame in ap.Frames)
                                {
                                    wMax = Math.Max(wMax, frame.Width);
                                    hMax = Math.Max(hMax, frame.Height);
                                }

                                this.Width = MathF.Min(320, wMax);
                                this.Height = MathF.Min(320, hMax);
                            }
                            else
                            {
                                this.Width = MathF.Min(320, ap.GLTex.Size.Width);
                                this.Height = MathF.Min(320, ap.GLTex.Size.Height);
                            }
                        }
                        else
                        {
                            this.Width = 24;
                            this.Height = 24;
                        }
                    }
                }
                else
                {
                    this.Width = 24;
                    this.Height = 24;
                }

                this.TextSize = new Vector2(this.Width, this.Height);
            }
            else
            {
                Vector2 systemSize = this.TextSize = ImGuiHelper.CalcTextSize(this.Text);
                this.IsExpression = owner.Type.HasFlag(ChatBlockType.Expression);
                this.Width = MathF.Max(minWidth, systemSize.X + (this.IsExpression ? 8 : 0));
                this.Height = MathF.Max(minHeight, systemSize.Y + (this.IsExpression ? 8 : 0));
                this.ImgStatusOnConstruct = AssetStatus.NoAsset;
            }
        }
    }
}