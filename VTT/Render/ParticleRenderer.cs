namespace VTT.Render
{
    using System.Numerics;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using VTT.Asset;
    using VTT.Asset.Glb;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;
    using VTT.GL.Bindings;

    public class ParticleRenderer
    {
        public FastAccessShader ParticleShader { get; set; }
        public ParticleSystem CurrentlyEditedSystem { get; set; }
        public ParticleSystemInstance CurrentlyEditedSystemInstance { get; set; }

        private Texture _renderTex;
        private uint _fbo;
        private uint _rbo;
        private VectorCamera _cam;

        public Texture RenderTexture => this._renderTex;

        public Thread SecondaryWorker { get; private set; }

        public bool OffscreenParticleUpdate { get; set; } = true;

        public Stopwatch CPUTimer { get; set; }

        public void Create()
        {
            this.ParticleShader = new FastAccessShader(OpenGLUtil.LoadShader("particle", ShaderType.Vertex, ShaderType.Fragment));
            this.ParticleShader.Program.Bind();
            this.ParticleShader["m_texture_diffuse"].Set(0);
            this.ParticleShader["m_texture_normal"].Set(1);
            this.ParticleShader["m_texture_emissive"].Set(2);
            this.ParticleShader["m_texture_aomr"].Set(3);
            this.ParticleShader["texture_shadows2d"].Set(4);

            this._fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.All, this._fbo);
            this._rbo = GL.GenRenderbuffer();
            GL.BindRenderbuffer(this._rbo);
            GL.RenderbufferStorage(SizedInternalFormat.DepthComponent24, 2048, 2048);

            this._renderTex = new Texture(TextureTarget.Texture2D);
            this._renderTex.Bind();
            this._renderTex.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this._renderTex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, SizedInternalFormat.Srgb8Alpha8, 2048, 2048, PixelDataFormat.Rgba, PixelDataType.Byte, IntPtr.Zero);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color0, TextureTarget.Texture2D, this._renderTex, 0);
            GL.FramebufferRenderbuffer(FramebufferTarget.All, FramebufferAttachment.Depth, this._rbo);
            FramebufferStatus fec = GL.CheckFramebufferStatus(FramebufferTarget.All);
            if (fec != FramebufferStatus.Complete)
            {
                throw new System.Exception("Framebuffer could not be completed - " + fec);
            }

            this._cam = new VectorCamera(new Vector3(5, 5, 5), new Vector3(-5, -5, -5).Normalized());
            this._cam.Projection = Matrix4x4.CreatePerspectiveFieldOfView(60 * MathF.PI / 180, 1, 0.01f, 100f);
            this._cam.RecalculateData(assumedUpVector: Vector3.UnitZ);
            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GL.DrawBuffer(DrawBufferMode.Back);

            if (this.OffscreenParticleUpdate)
            {
                this.SecondaryWorker = new Thread(this.UpdateOffscreen) { Priority = ThreadPriority.BelowNormal, IsBackground = true };
                this.SecondaryWorker.Start();
            }

            this.CPUTimer = new Stopwatch();
        }

        private readonly int[] viewport = new int[4];
        public void RenderFake()
        {
            if (this.CurrentlyEditedSystemInstance == null)
            {
                return;
            }

            uint fboID = (uint)GL.GetInteger(GLPropertyName.FramebufferBinding)[0];
            GL.GetInteger(GLPropertyName.Viewport).CopyTo(this.viewport);
            GL.BindFramebuffer(FramebufferTarget.All, this._fbo);
            GL.Viewport(0, 0, 2048, 2048);
            GL.ClearColor(0.39f, 0.39f, 0.39f, 1.0f);
            GL.Clear(ClearBufferMask.Color | ClearBufferMask.Depth);
            Client.Instance.Frontend.Renderer.MapRenderer.GridRenderer.Render(0, this._cam, null, false);
            GL.Enable(Capability.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            if (!this.HandleCustomShader(this.CurrentlyEditedSystemInstance.Template.CustomShaderID, null, this._cam, false, true, out _))
            {
                FastAccessShader shader = this.ParticleShader;
                shader.Program.Bind();
                shader.Essentials.View.Set(this._cam.View);
                shader.Essentials.Projection.Set(this._cam.Projection);
                shader.Essentials.Transform.Set(Matrix4x4.Identity);
                shader["frame"].Set((uint)Client.Instance.Frontend.FramesExisted);
                shader["update"].Set((uint)Client.Instance.Frontend.UpdatesExisted);
                shader["gamma_factor"].Set(Client.Instance.Settings.Gamma);
                shader.Particle.DoFOW.Set(false);
                shader["sky_color"].Set(Client.Instance.Frontend.Renderer.ObjectRenderer.CachedSkyColor);
                shader["cursor_position"].Set(new Vector3(0, 0, 0));
                shader["viewport_size"].Set(new Vector2(Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Width, Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Height));
                shader["dataBuffer"].Set(14);
                Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.UniformBlank(shader);
                GL.ActiveTexture(4);
                Client.Instance.Frontend.Renderer.White.Bind();
                GL.ActiveTexture(3);
                Client.Instance.Frontend.Renderer.Black.Bind();
                GL.ActiveTexture(2);
                Client.Instance.Frontend.Renderer.Black.Bind();
            }

            GL.ActiveTexture(0);
            this.CurrentlyEditedSystemInstance.Render(this.ParticleShader, this._cam.Position, this._cam);
            GL.Disable(Capability.Blend);
            GL.BindFramebuffer(FramebufferTarget.All, fboID);
            GL.Viewport(this.viewport[0], this.viewport[1], this.viewport[2], this.viewport[3]);
        }

        private static readonly EventWaitHandle particleMutex = new EventWaitHandle(false, EventResetMode.AutoReset);
        private static readonly EventWaitHandle particleSecondaryMutex = new EventWaitHandle(true, EventResetMode.AutoReset);
        private static readonly List<ParticleContainer> containers = new List<ParticleContainer>();
        private readonly object fxLock = new object();
        private readonly List<ParticleContainer> fxContainers = new List<ParticleContainer>();
        private bool clearFx;

        public void AddFXEmitter(Guid systemID, Vector3 position, int particlesToEmit)
        {
            ParticleContainer pc = new ParticleContainer(null) { 
                ID = Guid.NewGuid(),
                AttachmentPoint = string.Empty,
                ContainerPositionOffset = position,
                IsActive = true,
                IsFXEmitter = true,
                ParticlesToEmit = particlesToEmit,
                RotateVelocityByOrientation = false,
                SystemID = systemID,
                UseContainerOrientation = false
            };

            lock (this.fxLock)
            {
                this.fxContainers.Add(pc);
            }
        }

        private readonly List<Map> _disposeQueue = new List<Map>();
        public void Update()
        {
            if (this.OffscreenParticleUpdate)
            {
                particleSecondaryMutex.WaitOne();
            }
            else
            {
                this.UpdateForward();
            }

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

                lock (this.fxLock)
                {
                    for (int i = this.fxContainers.Count - 1; i >= 0; i--)
                    {
                        ParticleContainer pc = this.fxContainers[i];
                        if (this.clearFx)
                        {
                            pc.IsActive = false;
                            pc.DisposeInternal();
                            this.fxContainers.RemoveAt(i);
                            continue;
                        }

                        pc.UpdateBufferState();
                        if (pc.ParticlesToEmit == -1)
                        {
                            pc.IsActive = false;
                            pc.DisposeInternal();
                            this.fxContainers.RemoveAt(i);
                        }
                    }

                    this.clearFx = false;
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

            if (this.OffscreenParticleUpdate)
            {
                particleMutex.Set();
            }
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

                    lock (this.fxLock)
                    {
                        for (int i = this.fxContainers.Count - 1; i >= 0; i--)
                        {
                            ParticleContainer pc = this.fxContainers[i];
                            pc.Update();
                        }
                    }
                }


                particleSecondaryMutex.Set();
            }
        }

        public void UpdateForward()
        {
            if (Client.Instance.Settings.ParticlesEnabled)
            {
                foreach (ParticleContainer pc in containers)
                {
                    pc.Update();
                }

                containers.Clear();

                lock (this.fxLock)
                {
                    for (int i = this.fxContainers.Count - 1; i >= 0; i--)
                    {
                        ParticleContainer pc = this.fxContainers[i];
                        pc.Update();
                    }
                }
            }
        }

        // Only call this method when changing maps, descyncs internal state for client objects!
        public void ClearParticles(Map m)
        {
            this._disposeQueue.Add(m);
            lock (this.fxLock)
            {
                this.clearFx = true;
            }
        }

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

            this.CPUTimer.Restart();

            GL.Enable(Capability.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
            {
                GL.Enable(Capability.Multisample);
                GL.Enable(Capability.SampleAlphaToCoverage);
            }

            this._programsPopulated.Clear();
            FastAccessShader shader = this.ParticleShader;
            shader.Program.Bind();
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            shader.Essentials.View.Set(cam.View);
            shader.Essentials.Projection.Set(cam.Projection);
            shader.Essentials.Transform.Set(Matrix4x4.Identity);
            shader["frame"].Set((uint)Client.Instance.Frontend.FramesExisted);
            shader["update"].Set((uint)Client.Instance.Frontend.UpdatesExisted);
            shader["gamma_factor"].Set(Client.Instance.Settings.Gamma);
            shader["sky_color"].Set(Client.Instance.Frontend.Renderer.ObjectRenderer.CachedSkyColor);
            shader["cursor_position"].Set(Client.Instance.Frontend.Renderer.MapRenderer.TerrainHit ?? Vector3.Zero);
            shader["viewport_size"].Set(new Vector2(Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Width, Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Height));
            shader["dataBuffer"].Set(14);
            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.Uniform(shader);
            BindShadows2DTexture(m);
            GL.ActiveTexture(3);
            Client.Instance.Frontend.Renderer.Black.Bind();
            GL.ActiveTexture(2);
            Client.Instance.Frontend.Renderer.Black.Bind();
            GL.ActiveTexture(0);
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
                                this.HandleCustomShader(pc.CustomShaderID, m, cam, true, false, out shader);
                                pc.Render(shader, cam);
                            }
                        }
                    }
                }
            }

            lock (this.fxLock)
            {
                foreach (ParticleContainer pc in this.fxContainers)
                {
                    if (pc.IsActive)
                    {
                        this.HandleCustomShader(pc.CustomShaderID, m, cam, true, false, out shader);
                        pc.Render(shader, cam);
                    }
                }
            }

            if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
            {
                GL.Disable(Capability.Multisample);
                GL.Disable(Capability.SampleAlphaToCoverage);
            }

            GL.Disable(Capability.Blend);

            this.CPUTimer.Stop();
        }

        private List<ShaderProgram> _programsPopulated = new List<ShaderProgram>();
        private bool HandleCustomShader(Guid shaderID, Map m, Camera cam, bool enableMemory, bool blank, out FastAccessShader shader)
        {
            if (Guid.Empty.Equals(shaderID) || !Client.Instance.Settings.EnableCustomShaders)
            {
                shader = this.ParticleShader;
                if (!ShaderProgram.IsLastShaderSame(shader.Program))
                {
                    shader.Program.Bind();
                }

                return false;
            }
            else
            {
                AssetStatus aStat = Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(shaderID, AssetType.Shader, out Asset a);
                if (aStat == AssetStatus.Return && a.Shader != null && a.Shader.NodeGraph != null && a.Shader.NodeGraph.IsLoaded)
                {
                    shader = a.Shader.NodeGraph.GetGLShader(true);
                    if (!ShaderProgram.IsLastShaderSame(shader))
                    {
                        shader.Program.Bind();
                    }

                    if (!enableMemory || !this._programsPopulated.Contains(shader)) // Only need to populate shader uniforms once
                    {
                        shader.Essentials.View.Set(cam.View);
                        shader.Essentials.Projection.Set(cam.Projection);
                        shader.Essentials.Transform.Set(Matrix4x4.Identity);
                        shader["frame"].Set((uint)Client.Instance.Frontend.FramesExisted);
                        shader["update"].Set((uint)Client.Instance.Frontend.UpdatesExisted);
                        shader["gamma_factor"].Set(Client.Instance.Settings.Gamma);
                        shader["sky_color"].Set(Client.Instance.Frontend.Renderer.ObjectRenderer.CachedSkyColor);
                        shader["cursor_position"].Set(blank ? Vector3.Zero : Client.Instance.Frontend.Renderer.MapRenderer.TerrainHit ?? Vector3.Zero);
                        shader["viewport_size"].Set(new Vector2(Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Width, Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Height));
                        shader["dataBuffer"].Set(14);
                        if (blank)
                        {
                            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.UniformBlank(shader);
                        }
                        else
                        {
                            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.Uniform(shader);
                        }

                        BindShadows2DTexture(m);
                        GL.ActiveTexture(3);
                        Client.Instance.Frontend.Renderer.Black.Bind();
                        GL.ActiveTexture(2);
                        Client.Instance.Frontend.Renderer.Black.Bind();

                        // Load custom texture
                        GL.ActiveTexture(12);
                        if (a.Shader.NodeGraph.ExtraTextures.GetExtraTexture(out Texture t, out Vector2[] sz, out TextureAnimation[] anims) == AssetStatus.Return && t != null)
                        {
                            t.Bind();
                            for (int i = 0; i < sz.Length; ++i)
                            {
                                shader[$"unifiedTextureData[{i}]"].Set(sz[i]);
                                shader[$"unifiedTextureFrames[{i}]"].Set(anims[i].FindFrameForIndex(double.NaN).LocationUniform);
                            }
                        }
                        else
                        {
                            Client.Instance.Frontend.Renderer.White.Bind();
                        }

                        GL.ActiveTexture(0);
                        if (enableMemory)
                        {
                            this._programsPopulated.Add(shader);
                        }
                    }

                    return true;
                }
                else
                {
                    shader = this.ParticleShader;
                    if (!ShaderProgram.IsLastShaderSame(shader))
                    {
                        shader.Program.Bind();
                    }

                    return false;
                }
            }
        }

        private static void BindShadows2DTexture(Map m)
        {
            GL.ActiveTexture(4);
            Texture t = m != null && m.Has2DShadows && m.Is2D
                ? (Client.Instance.Frontend.Renderer.ObjectRenderer?.Shadow2DRenderer?.OutputTexture)
                : (Client.Instance.Frontend.Renderer.ObjectRenderer?.Shadow2DRenderer?.WhiteSquare);
            t ??= Client.Instance.Frontend.Renderer.White;
            t.Bind();
        }
    }
}
