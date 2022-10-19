namespace VTT.Asset
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using VTT.GL;

    public class AssetPreview // Client-only
    {
        public Texture GLTex { get; set; }
        public byte[] Data { get; set; }

        public bool IsAnimated { get; set; }
        public FrameData[] Frames { get; set; }
        public int FramesTotalDelay { get; set; }

        public FrameData GetCurrentFrame(int timeframe)
        {
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

        public Texture GetGLTexture()
        {
            if (this.GLTex == null && (this.Data == null || this.Data.Length == 0))
            {
                return null;
            }

            if (this.GLTex == null)
            {
                Image<Rgba32> img = Image.Load<Rgba32>(this.Data);
                Texture tex = new Texture(OpenTK.Graphics.OpenGL.TextureTarget.Texture2D);
                tex.Bind();
                tex.SetWrapParameters(WrapParam.Repeat, WrapParam.Repeat, WrapParam.Repeat);
                tex.SetFilterParameters(FilterParam.LinearMipmapLinear, FilterParam.Linear);
                if (this.Data.Length > 85000)
                {
                    System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                }

                tex.SetImage(img, OpenTK.Graphics.OpenGL.PixelInternalFormat.Rgba);
                tex.GenerateMipMaps();
                img.Dispose();
                this.Data = null;
                this.GLTex = tex;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            return this.GLTex;
        }

        public void Delete() => this.GLTex?.Dispose();

        public struct FrameData
        { 
            public int X { get; set; }
            public int Y { get; set; }
            public int Height { get; set; }
            public int Width { get; set; }
            public int Duration { get; set; }
            public int TotalDurationToHere { get; set; }

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
