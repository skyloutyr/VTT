namespace VTT.Render
{
    using OpenTK.Graphics.OpenGL;
    using System;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Render.Gui;
    using VTT.Render.LightShadow;
    using VTT.Util;

    public class WindowRenderer
    {
        public ClientWindow Container { get; }
        public GuiRenderer GuiRenderer { get; set; }
        public MapRenderer MapRenderer { get; set; }
        public MapObjectRenderer ObjectRenderer { get; set; }
        public SelectionManager SelectionManager { get; set; }
        public SkyRenderer SkyRenderer { get; set; }
        public PointLightsRenderer PointLightsRenderer { get; set; }
        public RulerRenderer RulerRenderer { get; set; }
        public PingRenderer PingRenderer { get; set; }
        public ParticleRenderer ParticleRenderer { get; set; }
        public UniversalPipeline Pipeline { get; set; }

        public Texture White { get; set; }
        public Texture Black { get; set; }

        public WindowRenderer(ClientWindow container) => this.Container = container;

        public void Create()
        {
            this.GuiRenderer = new GuiRenderer();
            this.GuiRenderer.Create();
            this.MapRenderer = new MapRenderer();
            this.MapRenderer.Create();
            this.ObjectRenderer = new MapObjectRenderer();
            this.ObjectRenderer.Create();
            this.SelectionManager = new SelectionManager();
            this.SkyRenderer = new SkyRenderer();
            this.SkyRenderer.Create();
            this.PointLightsRenderer = new PointLightsRenderer();
            this.PointLightsRenderer.Create();
            this.RulerRenderer = new RulerRenderer();
            this.RulerRenderer.Create();
            this.PingRenderer = new PingRenderer();
            this.PingRenderer.Create();
            this.ParticleRenderer = new ParticleRenderer();
            this.ParticleRenderer.Create();
            this.Pipeline = new UniversalPipeline();
            this.Pipeline.Create();
            int[] lMax = new int[1];
            OpenGLUtil.DetermineCompressedFormats();
            GL.GetInteger(GetPName.MaxTextureSize, lMax);
            Client.Instance.AssetManager.ClientAssetLibrary.GlMaxTextureSize = lMax[0];
            this.White = OpenGLUtil.LoadFromOnePixel(new SixLabors.ImageSharp.PixelFormats.Rgba32(1, 1, 1, 1f));
            this.Black = OpenGLUtil.LoadFromOnePixel(new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 0, 0, 1f));
        }

        private int _mapTrackerUpdateCounter;
        private bool _windowNeedsDrawing;
        public void Update(double time)
        {
            Map m = Client.Instance.CurrentMap;
            this.ObjectRenderer?.Update(m, time);
            this.SelectionManager?.Update();
            this.MapRenderer?.Update(m, time);
            this.RulerRenderer?.Update(time);
            this.GuiRenderer?.MainMenuRenderer?.Update(time);
            this.ParticleRenderer?.CurrentlyEditedSystemInstance?.Update(new OpenTK.Mathematics.Vector3(5, 5, 5));
            this.ParticleRenderer?.CurrentlyEditedSystemInstance?.UpdateBufferState();
            if (m != null)
            {
                if (++this._mapTrackerUpdateCounter >= 60)
                {
                    this._mapTrackerUpdateCounter = 0;
                    m.TurnTracker.Pulse();
                }

                lock (m.Lock)
                {
                    for (int i = m.Objects.Count - 1; i >= 0; i--)
                    {
                        MapObject mo = m.Objects[i];
                        if (!mo.IsRemoved)
                        {
                            mo.Update(time);
                        }
                        else
                        {
                            mo.Container = null;
                            mo.MapID = Guid.Empty;
                            m.Objects.Remove(mo);
                            m.ObjectsByID.Remove(mo.ID);
                            continue;
                        }
                    }
                }

                this.ParticleRenderer?.Update();
            }
        }

        public void ScrollWheel(float dx, float dy)
        {
            this.MapRenderer.ScrollCamera(dy);
            Client.Instance.Frontend.Renderer.GuiRenderer.HandleScrollWheelExtra(dx, dy);
        }

        public void Render(double time)
        {
            if (this._windowNeedsDrawing)
            {
                Map m = Client.Instance.CurrentMap;
                GL.ClearColor(this.SkyRenderer.GetSkyColor().ToGLColor());
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
                if (Client.Instance.Settings.MSAA == ClientSettings.MSAAMode.Disabled)
                {
                    GL.Disable(EnableCap.Multisample);
                }

                this.Container.GuiWrapper.BeforeFrame();
                this.Container.GuiWrapper.NewFrame(time);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                GL.DrawBuffer(DrawBufferMode.Back);
                GL.Viewport(0, 0, Client.Instance.Frontend.GameHandle.Size.X, Client.Instance.Frontend.GameHandle.Size.Y);

                this.MapRenderer.Render(m, time);
                this.ObjectRenderer.Render(m, time);
                this.RulerRenderer?.Render(time);
                this.ParticleRenderer.RenderFake();
                this.ParticleRenderer.RenderAll();
                this.MapRenderer.DrawingRenderer.Render(this.MapRenderer.ClientCamera);
                this.GuiRenderer.Render(time);
                this.SelectionManager.Render(time);
                this.SkyRenderer.Render(time);
                this.PingRenderer?.Render(time);
                this.ObjectRenderer.RenderLate(m, time);
                this.Container.GuiWrapper.Render(time);
                this.ObjectRenderer.RenderLatest(m, time);
                this.MapRenderer.FOWRenderer?.Render(time);

                Client.Instance.Frontend.GameHandle.SwapBuffers();
            }
        }

        public void Resize(int w, int h)
        {
            GL.Viewport(0, 0, w, h);
            this.Pipeline?.Resize(w, h);
            this.MapRenderer.Resize(w, h);
            this.ObjectRenderer?.Resize(w, h);
        }

        public void SetWindowState(bool state) => this._windowNeedsDrawing = state;
    }
}
