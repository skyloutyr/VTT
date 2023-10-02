namespace VTT.Render
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using System.Collections.Generic;
    using System.Threading;
    using VTT.Asset;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public class ParticleRenderer
    {
        public ShaderProgram ParticleShader { get; set; }
        public ParticleSystem CurrentlyEditedSystem { get; set; }
        public ParticleSystemInstance CurrentlyEditedSystemInstance { get; set; }

        private Texture _renderTex;
        private int _fbo;
        private int _rbo;
        private VectorCamera _cam;

        public Texture RenderTexture => this._renderTex;

        public Thread SecondaryWorker { get; private set; }

        public void Create()
        {
            this.ParticleShader = OpenGLUtil.LoadShader("particle", ShaderType.VertexShader, ShaderType.FragmentShader);
            this.ParticleShader.Bind();
            this.ParticleShader["m_texture_diffuse"].Set(0);
            this.ParticleShader["m_texture_normal"].Set(1);
            this.ParticleShader["m_texture_emissive"].Set(2);
            this.ParticleShader["m_texture_aomr"].Set(3);

            this._fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, this._fbo);
            this._rbo = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, this._rbo);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, 2048, 2048);

            this._renderTex = new Texture(TextureTarget.Texture2D);
            this._renderTex.Bind();
            this._renderTex.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this._renderTex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.SrgbAlpha, 2048, 2048, 0, PixelFormat.Rgba, PixelType.UnsignedByte, System.IntPtr.Zero);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, this._renderTex, 0);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, this._rbo);
            FramebufferErrorCode fec = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (fec != FramebufferErrorCode.FramebufferComplete)
            {
                throw new System.Exception("Framebuffer could not be completed - " + fec);
            }

            this._cam = new VectorCamera(new Vector3(5, 5, 5), new Vector3(-5, -5, -5).Normalized());
            this._cam.Projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(60), 1, 0.01f, 100f);
            this._cam.RecalculateData(assumedUpVector: Vector3.UnitZ);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DrawBuffer(DrawBufferMode.Back);

            this.SecondaryWorker = new Thread(this.UpdateOffscreen) { Priority = ThreadPriority.BelowNormal, IsBackground = true };
            this.SecondaryWorker.Start();
        }

        private readonly int[] viewport = new int[4];
        public void RenderFake()
        {
            if (this.CurrentlyEditedSystemInstance == null)
            {
                return;
            }

            int fboID = GL.GetInteger(GetPName.FramebufferBinding);
            GL.GetInteger(GetPName.Viewport, this.viewport);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, this._fbo);
            GL.Viewport(0, 0, 2048, 2048);
            GL.ClearColor(0.39f, 0.39f, 0.39f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            Map m = Client.Instance.CurrentMap;
            if (m != null)
            {
                Client.Instance.Frontend.Renderer.MapRenderer.GridRenderer.Render(0, this._cam, m, false);
            }

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            this.ParticleShader.Bind();
            this.ParticleShader["view"].Set(this._cam.View);
            this.ParticleShader["projection"].Set(this._cam.Projection);
            this.ParticleShader["model"].Set(Matrix4.Identity);
            this.ParticleShader["frame"].Set((uint)Client.Instance.Frontend.FramesExisted);
            this.ParticleShader["update"].Set((uint)Client.Instance.Frontend.UpdatesExisted);
            this.ParticleShader["gamma_factor"].Set(Client.Instance.Settings.Gamma);
            this.ParticleShader["do_fow"].Set(false);
            this.ParticleShader["sky_color"].Set(Client.Instance.Frontend.Renderer.ObjectRenderer.CachedSkyColor);
            this.ParticleShader["dataBuffer"].Set(14);
            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.UniformBlank(this.ParticleShader);
            this.CurrentlyEditedSystemInstance.Render(this.ParticleShader, this._cam.Position, this._cam);
            GL.Disable(EnableCap.Blend);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fboID);
            GL.Viewport(this.viewport[0], this.viewport[1], this.viewport[2], this.viewport[3]);
        }

        private static readonly EventWaitHandle particleMutex = new EventWaitHandle(false, EventResetMode.AutoReset);
        private static readonly EventWaitHandle particleSecondaryMutex = new EventWaitHandle(true, EventResetMode.AutoReset);
        private static readonly List<ParticleContainer> containers = new List<ParticleContainer>();

        private readonly List<Map> _disposeQueue = new List<Map>();
        public void Update()
        {
            particleSecondaryMutex.WaitOne();
            if (Client.Instance.Settings.ParticlesEnabled)
            {
                Map m = Client.Instance.CurrentMap;
                if (m != null)
                {
                    foreach (MapObject mo in m.IterateObjects(null))
                    {
                        lock (mo.Lock)
                        {
                            foreach (ParticleContainer pc in mo.ParticleContainers.Values)
                            {
                                pc.UpdateBufferState();
                                containers.Add(pc);
                            }
                        }
                    }
                }

                if (this._disposeQueue.Count > 0)
                {
                    foreach (Map map in this._disposeQueue)
                    {
                        if (map != null)
                        {
                            foreach (MapObject mo in map.IterateObjects(null))
                            {
                                lock (mo.Lock)
                                {
                                    foreach (ParticleContainer pc in mo.ParticleContainers.Values)
                                    {
                                        pc.IsActive = false;
                                        pc.DisposeInternal();
                                    }
                                }
                            }
                        }
                    }

                    this._disposeQueue.Clear();
                }

            }

            particleMutex.Set();
        }

        public void UpdateOffscreen()
        {
            while (true)
            {
                particleMutex.WaitOne();
                if (Client.Instance.Settings.ParticlesEnabled)
                {
                    foreach (ParticleContainer pc in containers)
                    {
                        pc.Update();
                    }

                    containers.Clear();
                }

                particleSecondaryMutex.Set();
            }
        }

        // Only call this method when changing maps, descyncs internal state for client objects!
        public void ClearParticles(Map m) => this._disposeQueue.Add(m);

        public void RenderAll()
        {
            Map m = Client.Instance.CurrentMap;
            if (m == null)
            {
                return;
            }

            if (!Client.Instance.Settings.ParticlesEnabled)
            {
                return;
            }

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
            {
                GL.Enable(EnableCap.Multisample);
                GL.Enable(EnableCap.SampleAlphaToCoverage);
            }

            this.ParticleShader.Bind();
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            this.ParticleShader["view"].Set(cam.View);
            this.ParticleShader["projection"].Set(cam.Projection);
            this.ParticleShader["model"].Set(Matrix4.Identity);
            this.ParticleShader["frame"].Set((uint)Client.Instance.Frontend.FramesExisted);
            this.ParticleShader["update"].Set((uint)Client.Instance.Frontend.UpdatesExisted);
            this.ParticleShader["gamma_factor"].Set(Client.Instance.Settings.Gamma);
            this.ParticleShader["sky_color"].Set(Client.Instance.Frontend.Renderer.ObjectRenderer.CachedSkyColor);
            this.ParticleShader["dataBuffer"].Set(14);
            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.Uniform(this.ParticleShader);
            GL.ActiveTexture(TextureUnit.Texture0);
            foreach (MapObject mo in m.IterateObjects(null))
            {
                if (mo.MapLayer <= 0 || Client.Instance.IsAdmin)
                {
                    lock (mo.Lock)
                    {
                        foreach (ParticleContainer pc in mo.ParticleContainers.Values)
                        {
                            if (pc.IsActive)
                            {
                                pc.Render(this.ParticleShader, cam);
                            }
                        }
                    }
                }
            }

            if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
            {
                GL.Disable(EnableCap.Multisample);
                GL.Disable(EnableCap.SampleAlphaToCoverage);
            }

            GL.Disable(EnableCap.Blend);
        }
    }
}
