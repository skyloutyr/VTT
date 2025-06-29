﻿namespace VTT.Render
{
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Numerics;
    using VTT.Control;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Render.Gui;
    using VTT.Render.LightShadow;
    using VTT.Util;
    using GL = GL.Bindings.GL;

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
        public ClientAvatarLibrary AvatarLibrary { get; set; } = new ClientAvatarLibrary();

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
            this.ParticleRenderer = new ParticleRenderer() { OffscreenParticleUpdate = Client.Instance.Settings.OffscreenParticleUpdates };
            this.ParticleRenderer.Create();
            this.Pipeline = new UniversalPipeline();
            this.Pipeline.Create();
            OpenGLUtil.DetermineCompressedFormats();
            int lMax = GL.GetInteger(GLPropertyName.MaxTextureSize)[0];
            Client.Instance.AssetManager.ClientAssetLibrary.GlMaxTextureSize = lMax;
            this.White = OpenGLUtil.LoadFromOnePixel(new Rgba32(1, 1, 1, 1f));
            this.Black = OpenGLUtil.LoadFromOnePixel(new Rgba32(0, 0, 0, 1f));
            OpenGLUtil.NameObject(GLObjectType.Texture, this.White, "White pixel");
            OpenGLUtil.NameObject(GLObjectType.Texture, this.Black, "Black pixel");
        }

        private bool _windowNeedsDrawing;
        public void Update()
        {
            Map m = Client.Instance.CurrentMap;
            this.MapRenderer?.Update(m);
            this.ObjectRenderer?.Update(m);
            this.ObjectRenderer?.Shadow2DRenderer?.Update(m);
            this.SelectionManager?.Update();
            this.RulerRenderer?.Update();
            this.GuiRenderer?.MainMenuRenderer?.Update();
            this.ParticleRenderer?.CurrentlyEditedSystemInstance?.Update(new Vector3(5, 5, 5));
            this.ParticleRenderer?.CurrentlyEditedSystemInstance?.UpdateBufferState();
            if (m != null)
            {
                m.Update();
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
            OpenGLUtil.StartSection("Frame Start");
            if (this._windowNeedsDrawing)
            {
                Map m = Client.Instance.CurrentMap;
                GLState.Clear(ClearBufferMask.Depth | ClearBufferMask.Stencil);
                if (Client.Instance.Settings.MSAA == ClientSettings.MSAAMode.Disabled)
                {
                    GLState.Multisample.Set(false);
                }

                this.Container.GuiWrapper.BeforeFrame();
                this.Container.GuiWrapper.NewFrame(time);
                GL.BindFramebuffer(FramebufferTarget.All, 0);
                GL.DrawBuffer(DrawBufferMode.Back);
                GL.Viewport(0, 0, Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Width, Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Height);

                this.AvatarLibrary.Render();
                this.MapRenderer.Render(m, time);
                this.ObjectRenderer.Render(m, time);
                this.RulerRenderer?.Render(time);
                this.SelectionManager?.Render(m, time);
                this.MapRenderer.DrawingRenderer.Render(this.MapRenderer.ClientCamera);
                this.ParticleRenderer.RenderFake();
                this.GuiRenderer.Render(time);
                this.SelectionManager.RenderGui(time);
                this.PingRenderer?.Render(time);
                this.SkyRenderer.Render(m, time);
                this.ObjectRenderer.RenderLate(m, time);
                this.MapRenderer.RenderLate(m, time);
                this.ParticleRenderer.RenderAll();
                this.Container.GuiWrapper.Render(time);
                this.ObjectRenderer.RenderLatest(m, time);
                this.MapRenderer.FOWRenderer?.Render(time);
            }

            OpenGLUtil.EndSection();
            // Have to swap buffers here to still adhere to vsync even when minimized
            // With vsync disabled (swapInterval == 0) we are burning cpu, but w/e as swap of 0 does that anyway
            Client.Instance.Frontend.GameHandle.SwapBuffers();
        }

        public void Resize(int w, int h)
        {
            GL.Viewport(0, 0, w, h);
            this.Pipeline?.Resize(w, h);
            this.ObjectRenderer?.Shadow2DRenderer?.Resize(w, h);
            this.MapRenderer.Resize(w, h);
            this.ObjectRenderer?.Resize(w, h);
        }

        public void SetWindowState(bool state) => this._windowNeedsDrawing = state;
    }

    public class ClientAvatarLibrary
    {
        public Dictionary<Guid, (Texture, bool)> ClientImages { get; } = new Dictionary<Guid, (Texture, bool)>();
        public ConcurrentQueue<(Guid, Image<Rgba32>)> ClientImagesQueue { get; } = new ConcurrentQueue<(Guid, Image<Rgba32>)>();
        public void UploadClientTexture(Guid clientID, Image<Rgba32> img)
        {
            bool b = img != null;
            if (!b)
            {
                img = new Image<Rgba32>(32, 32, new Rgba32(0, 0, 0, 255));
            }

            if (!this.ClientImages.TryGetValue(clientID, out (Texture, bool) value))
            {
                Texture tex = new Texture(TextureTarget.Texture2D);
                tex.Bind();
                tex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
                tex.SetFilterParameters(FilterParam.LinearMipmapLinear, FilterParam.Linear);
                this.ClientImages[clientID] = value = (tex, b);
            }

            value.Item1.Bind();
            value.Item1.SetImage(img, SizedInternalFormat.Rgba8);
            value.Item1.GenerateMipMaps();
            this.ClientImages[clientID] = (value.Item1, b);
        }

        public void Render()
        {
            while (!this.ClientImagesQueue.IsEmpty)
            {
                if (!this.ClientImagesQueue.TryDequeue(out (Guid, Image<Rgba32>) val))
                {
                    break;
                }

                this.UploadClientTexture(val.Item1, val.Item2);
            }
        }
    }
}
