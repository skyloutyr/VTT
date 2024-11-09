namespace VTT.Asset
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using VTT.Asset.Glb;
    using VTT.GL;
    using VTT.GL.Bindings;

    public class AssetPreview // Client-only
    {
        public Texture GLTex { get; set; }
        public byte[] Data { get; set; }

        public bool IsAnimated { get; set; }
        public FrameData[] Frames { get; set; }
        public int FramesTotalDelay { get; set; }

        public FrameData GetCurrentFrame(int timeframe)
        {
            if (!this.IsAnimated || this.FramesTotalDelay == 0)
            {
                return this.Frames == null || this.Frames.Length == 0 ? new FrameData() : this.Frames[0];
            }

            timeframe %= this.FramesTotalDelay;
            for (int i = 0; i < this.Frames.Length; ++i)
            {
                timeframe -= this.Frames[i].Duration;
                if (timeframe < 0)
                {
                    return this.Frames[i];
                }
            }

            return this.Frames[0];
        }

        public void CopyFromAnimation(TextureAnimation anim, Size imgTotalSizeInPixels)
        {
            if (anim != null && anim.Frames.Length > 1)
            {
                this.IsAnimated = true;
                this.Frames = new FrameData[anim.Frames.Length];
                for (int i = 0; i < this.Frames.Length; ++i)
                {
                    TextureAnimation.Frame f = anim.Frames[i];
                    int sX = (int)(f.Location.X * imgTotalSizeInPixels.Width);
                    int sY = (int)(f.Location.Y * imgTotalSizeInPixels.Height);
                    int w = (int)(f.Location.Width * imgTotalSizeInPixels.Width);
                    int h = (int)(f.Location.Height * imgTotalSizeInPixels.Height);
                    this.Frames[i] = new FrameData(sX, sY, h, w, (int)f.Duration, this.FramesTotalDelay);
                    this.FramesTotalDelay += (int)f.Duration;
                }
            }
        }

        public Texture GetGLTexture()
        {
            if (this.GLTex == null && (this.Data == null || this.Data.Length == 0))
            {
                return null;
            }

            if (this.GLTex == null)
            {
                Image<Rgba32> img = Image.Load<Rgba32>(this.Data);
                Texture tex = new Texture(TextureTarget.Texture2D);
                tex.Bind();
                tex.SetWrapParameters(WrapParam.Repeat, WrapParam.Repeat, WrapParam.Repeat);
                tex.SetFilterParameters(FilterParam.LinearMipmapLinear, FilterParam.Linear);
                tex.SetImage(img, SizedInternalFormat.Rgba8);
                tex.GenerateMipMaps();
                img.Dispose();
                this.Data = null;
                this.GLTex = tex;
            }

            return this.GLTex;
        }

        public void Free() => this.GLTex?.Dispose();

        public struct FrameData
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Height { get; set; }
            public int Width { get; set; }
            public int Duration { get; set; }
            public int TotalDurationToHere { get; set; }

            public readonly bool IsValidFrame => this.Width + this.Height > 0;

            public FrameData(int x, int y, int height, int width, int length, int tdTH)
            {
                this.X = x;
                this.Y = y;
                this.Height = height;
                this.Width = width;
                this.Duration = length;
                this.TotalDurationToHere = tdTH;
            }
        }
    }
}
