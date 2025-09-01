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
    using System.Collections.Concurrent;
    using System.Linq;
    using VTT.Render.Shaders;
    using VTT.Render.Shaders.UBO;

    public class ParticleRenderer
    {
        public FastAccessShader<ParticleUniforms> ParticleShader { get; set; }
        public ParticleSystem CurrentlyEditedSystem { get; set; }
        public ParticleSystemInstance CurrentlyEditedSystemInstance { get; set; }

        private Texture _renderTex;
        private uint _fbo;
        private uint _rbo;
        private VectorCamera _cam;
        private UniformBufferFrameData _blankFrameData;

        public Texture RenderTexture => this._renderTex;

        public Thread SecondaryWorker { get; private set; }

        public bool OffscreenParticleUpdate { get; set; } = true;

        public Stopwatch CPUTimer { get; set; }

        private readonly List<ParticleContainer> _containers = new List<ParticleContainer>();
        private readonly ConcurrentQueue<ParticleAction> _containerActionQueue = new ConcurrentQueue<ParticleAction>();
        private volatile bool _freed;

        public void Create()
        {
#if USE_VTX_COMPRESSION
            this.ParticleShader = new FastAccessShader<ParticleUniforms>(OpenGLUtil.LoadShader("particle", stackalloc ShaderType[2] { ShaderType.Vertex, ShaderType.Fragment }, new DefineRule[] { new DefineRule(DefineRule.Mode.Define, "USE_VTX_COMPRESSION") }));
#else
            this.ParticleShader = new FastAccessShader<ParticleUniforms>(OpenGLUtil.LoadShader("particle", stackalloc ShaderType[2] { ShaderType.Vertex, ShaderType.Fragment }));
#endif
            this.ParticleShader.Bind();
            this.ParticleShader.Program.BindUniformBlock("FrameData", 1);
            this.ParticleShader.Program.BindUniformBlock("Material", 3);
            this.ParticleShader.Uniforms.Material.DiffuseSampler.Set(0);
            this.ParticleShader.Uniforms.Material.NormalSampler.Set(1);
            this.ParticleShader.Uniforms.Material.EmissiveSampler.Set(2);
            this.ParticleShader.Uniforms.Material.AOMRSampler.Set(3);
            this.ParticleShader.Uniforms.Sampler2DShadows.Set(4);
            this.ParticleShader.Uniforms.DataBufferSampler.Set(14);
            this.ParticleShader.Uniforms.FOW.Sampler.Set(15);

            this._fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.All, this._fbo);
            OpenGLUtil.NameObject(GLObjectType.Framebuffer, this._fbo, "Particle preview fbo");
            this._rbo = GL.GenRenderbuffer();
            GL.BindRenderbuffer(this._rbo);
            OpenGLUtil.NameObject(GLObjectType.Renderbuffer, this._rbo, "Particle preview rbo24d");
            GL.RenderbufferStorage(SizedInternalFormat.DepthComponent24, 2048, 2048);

            this._renderTex = new Texture(TextureTarget.Texture2D);
            this._renderTex.Bind();
            OpenGLUtil.NameObject(GLObjectType.Texture, this._renderTex, "Particle preview result texture");
            this._renderTex.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this._renderTex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, SizedInternalFormat.Rgba8, 2048, 2048, PixelDataFormat.Rgba, PixelDataType.Byte, IntPtr.Zero);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color0, TextureTarget.Texture2D, this._renderTex, 0);
            GL.FramebufferRenderbuffer(FramebufferTarget.All, FramebufferAttachment.Depth, this._rbo);
            FramebufferStatus fec = GL.CheckFramebufferStatus(FramebufferTarget.All);
            if (fec != FramebufferStatus.Complete)
            {
                throw new Exception("Framebuffer could not be completed - " + fec);
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

            this._blankFrameData = new UniformBufferFrameData();
            OpenGLUtil.NameObject(GLObjectType.Buffer, this._blankFrameData.GLID, "Particle dummy frame UBO");

            this.CPUTimer = new Stopwatch();
        }

        public void RenderFake()
        {
            if (this.CurrentlyEditedSystemInstance == null)
            {
                return;
            }

            OpenGLUtil.StartSection("Preview particle system");
            GL.BindFramebuffer(FramebufferTarget.All, this._fbo);
            GL.Viewport(0, 0, 2048, 2048);
            GL.ClearColor(0.125f, 0.125f, 0.125f, 1.0f);
            GLState.Clear(ClearBufferMask.Color | ClearBufferMask.Depth);
            Client.Instance.Frontend.Renderer.MapRenderer.GridRenderer.Render(0, this._cam, null, false);
            GLState.DepthMask.Set(false);
            GLState.Blend.Set(true);
            GLState.BlendFunc.Set((BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha));
            this._blankFrameData.SetBlank(this._cam, default, new Vector4(0.125f, 0.125f, 0.125f, 0f));
            this._blankFrameData.BindAsUniformBuffer();
            if (!this.HandleCustomShader(this.CurrentlyEditedSystemInstance.Template.CustomShaderID, null, 0, this._cam, false, true, out _))
            {
                FastAccessShader<ParticleUniforms> shader = this.ParticleShader;
                shader.Program.Bind();
                shader.Uniforms.Gamma.Factor.Set(Client.Instance.Settings.Gamma);
                shader.Uniforms.DoFOW.Set(false);
                Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.BindTexture(true);
                GLState.ActiveTexture.Set(4);
                Client.Instance.Frontend.Renderer.White.Bind();
                GLState.ActiveTexture.Set(3);
                Client.Instance.Frontend.Renderer.Black.Bind();
                GLState.ActiveTexture.Set(2);
                Client.Instance.Frontend.Renderer.Black.Bind();
                GLState.ActiveTexture.Set(6);
                Client.Instance.Frontend.Renderer.SkyRenderer.SkyboxRenderer.BlankTextureArray.Bind();
                GLState.ActiveTexture.Set(0);
            }

            GLState.ActiveTexture.Set(0);
            GlbMaterial.ResetState();
            this.CurrentlyEditedSystemInstance.Render(this.ParticleShader, this._cam.Position, this._cam);
            GLState.Blend.Set(false);
            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GLState.DepthMask.Set(true);
            GL.Viewport(0, 0, Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
            Client.Instance.Frontend.Renderer.ObjectRenderer.FrameUBO.BindAsUniformBuffer();
            OpenGLUtil.EndSection();
        }

        private static readonly EventWaitHandle particleMutex = new EventWaitHandle(false, EventResetMode.AutoReset);
        private static readonly EventWaitHandle particleSecondaryMutex = new EventWaitHandle(true, EventResetMode.AutoReset);

        public void AddEmitter(ParticleContainer pc) => this._containerActionQueue.Enqueue(new ParticleAction(ParticleAction.Kind.Addition, pc));
        public void RemoveEmitter(ParticleContainer pc) => this._containerActionQueue.Enqueue(new ParticleAction(ParticleAction.Kind.Deletion, pc));
        public void SafeUpdateEmitter(ParticleContainer pc, DataElement data) => this._containerActionQueue.Enqueue(new ParticleAction(ParticleAction.Kind.Update, pc, data));

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

            this.AddEmitter(pc);
        }

        private void FreeEmitter(ParticleContainer pc, int idx = -1)
        {
            pc.IsActive = false;
            pc.DisposeInternal();
            if (idx != -1)
            {
                this._containers.RemoveAt(idx);
            }
        }

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
                while (this._containerActionQueue.TryDequeue(out ParticleAction a))
                {
                    switch (a.type)
                    {
                        case ParticleAction.Kind.Addition:
                        {
                            this._containers.Add(a.container);
                            break;
                        }

                        case ParticleAction.Kind.Deletion:
                        {
                            this.FreeEmitter(a.container);
                            this._containers.Remove(a.container);
                            break;
                        }

                        case ParticleAction.Kind.Update:
                        {
                            a.container.Deserialize(a.readData);
                            break;
                        }

                        case ParticleAction.Kind.FullClear:
                        {
                            foreach (ParticleContainer pc in this._containers)
                            {
                                pc.IsActive = false;
                                pc.DisposeInternal();
                            }

                            this._containers.Clear();
                            break;
                        }

                        default:
                        {
                            break;
                        }
                    }
                }

                for (int i = this._containers.Count - 1; i >= 0; i--)
                {
                    ParticleContainer pc = this._containers[i];
                    pc.UpdateBufferState();
                    if (pc.IsFXEmitter && pc.ParticlesToEmit == -1)
                    {
                        this.FreeEmitter(pc, i);
                    }
                }
            }

            if (this.OffscreenParticleUpdate)
            {
                particleMutex.Set();
            }
        }

        public void UpdateOffscreen()
        {
            while (!this._freed)
            {
                particleMutex.WaitOne();
                this.UpdateForward();
                particleSecondaryMutex.Set();
            }
        }

        public void Terminate() => this._freed = true;

        public void UpdateForward()
        {
            if (Client.Instance.Settings.ParticlesEnabled)
            {
                foreach (ParticleContainer pc in this._containers)
                {
                    pc.Update();
                }
            }
        }

        // Only call this method when changing maps, descyncs internal state for client objects!
        public void ClearParticles(Map m) => this._containerActionQueue.Enqueue(new ParticleAction(ParticleAction.Kind.FullClear, null));

        public void RenderAll(double dt)
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
            OpenGLUtil.StartSection("Particle systems");

            GLState.Blend.Set(true);
            GLState.BlendFunc.Set((BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha));
            GLState.DepthTest.Set(true);
            GLState.DepthMask.Set(false);

            this._programsPopulated.Clear();
            FastAccessShader<ParticleUniforms> shader = this.ParticleShader;
            shader.Program.Bind();
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            Client.Instance.Frontend.Renderer.ObjectRenderer.UniformCommonShaderData(m, dt, shader.Uniforms.FrameData, shader.Uniforms.Gamma.EnableCorrection, shader.Uniforms.Gamma.Factor);
            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.BindTexture(false);
            Client.Instance.Frontend.Renderer.SkyRenderer.SkyboxRenderer.UniformShaderWithRespectToUBO(shader.Uniforms.FrameData.Skybox, m); // Particles also use UBOs now
            BindShadows2DTexture(m);
            GLState.ActiveTexture.Set(3);
            Client.Instance.Frontend.Renderer.Black.Bind();
            GLState.ActiveTexture.Set(2);
            Client.Instance.Frontend.Renderer.Black.Bind();
            GLState.ActiveTexture.Set(0);
            GlbMaterial.ResetState();
            foreach (ParticleContainer pc in this._containers.OrderByDescending(x => this.GetCameraDistanceTo(x, cam, m)))
            {
                bool shouldRender = pc.IsActive && (pc.Container == null || pc.Container.MapLayer <= 0 || Client.Instance.IsAdmin);
                if (shouldRender)
                {
                    this.HandleCustomShader(pc.CustomShaderID, m, dt, cam, true, false, out shader);
                    pc.Render(shader, cam);
                }
            }

            GLState.DepthMask.Set(true);
            GLState.DepthTest.Set(false);
            GLState.Blend.Set(false);

            this.CPUTimer.Stop();
            OpenGLUtil.EndSection();
        }

        private float GetCameraDistanceTo(ParticleContainer pc, Camera cam, Map m)
        {
            Vector3 sPos = (pc.Container?.Position ?? Vector3.Zero) + pc.ContainerPositionOffset;
            return m.Is2D ? m.Camera2DHeight - sPos.Z : Vector3.Distance(sPos, cam.Position);
        }

        private readonly List<FastAccessShader<ParticleUniforms>> _programsPopulated = new List<FastAccessShader<ParticleUniforms>>();
        private bool HandleCustomShader(Guid shaderID, Map m, double dt, Camera cam, bool enableMemory, bool blank, out FastAccessShader<ParticleUniforms> shader)
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
                AssetStatus aStat = Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(shaderID, AssetType.Shader, out Asset a);
                if (aStat == AssetStatus.Return)
                {
                    shader = null;
                    switch (a.Type)
                    {
                        case AssetType.Shader:
                        {
                            if (a.Shader != null && a.Shader.NodeGraph != null && a.Shader.NodeGraph.IsLoaded)
                            {
                                shader = a.Shader.NodeGraph.GetGLShader<ParticleUniforms>(true);
                            }

                            break;
                        }

                        case AssetType.GlslFragmentShader:
                        {
                            if (a.GlslFragment != null && !string.IsNullOrEmpty(a.GlslFragment.Data))
                            {
                                shader = a.GlslFragment.GetGLShader<ParticleUniforms>(true);
                            }

                            break;
                        }

                        default:
                        {
                            break;
                        }
                    }

                    if (shader != null)
                    {
                        if (!ShaderProgram.IsLastShaderSame(shader))
                        {
                            shader.Program.Bind();
                        }

                        if (!enableMemory || !this._programsPopulated.Contains(shader)) // Only need to populate shader uniforms once
                        {
                            Client.Instance.Frontend.Renderer.ObjectRenderer.UniformCommonShaderData(m, dt, shader.Uniforms.FrameData, shader.Uniforms.Gamma.EnableCorrection, shader.Uniforms.Gamma.Factor);
                            shader.Uniforms.DataBufferSampler.Set(14);
                            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.BindTexture(blank);
                            BindShadows2DTexture(m);
                            GLState.ActiveTexture.Set(3);
                            Client.Instance.Frontend.Renderer.Black.Bind();
                            GLState.ActiveTexture.Set(2);
                            Client.Instance.Frontend.Renderer.Black.Bind();

                            if (a.Type == AssetType.Shader) // Custom GLSL shaders can't define extra textures due to being raw GLSL data
                            {
                                // Load custom texture
                                GLState.ActiveTexture.Set(12);
                                if (a.Shader.NodeGraph.ExtraTextures.GetExtraTexture(out Texture t, out Vector2[] sz, out TextureAnimation[] anims) == AssetStatus.Return && t != null)
                                {
                                    t.Bind();
                                    shader.Uniforms.CustomShader.Sizes.Set(sz, 0);
                                    shader.Uniforms.CustomShader.Sampler.Set(12);
                                    for (int i = 0; i < sz.Length; ++i)
                                    {
                                        shader.Uniforms.CustomShader.AnimationFrames.Set(anims[i].FindFrameForIndex(double.NaN).LocationUniform, i);
                                    }
                                }
                                else
                                {
                                    Client.Instance.Frontend.Renderer.White.Bind();
                                }
                            }

                            Client.Instance.Frontend.Renderer.SkyRenderer.SkyboxRenderer.UniformShaderWithRespectToUBO(shader.Uniforms.FrameData.Skybox, m);

                            GLState.ActiveTexture.Set(0);
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
                        if (!ShaderProgram.IsLastShaderSame(shader.Program))
                        {
                            shader.Program.Bind();
                        }

                        return false;
                    }
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
            GLState.ActiveTexture.Set(4);
            Texture t = m != null && m.Has2DShadows && m.Is2D
                ? (Client.Instance.Frontend.Renderer.ObjectRenderer?.Shadow2DRenderer?.OutputTexture)
                : (Client.Instance.Frontend.Renderer.ObjectRenderer?.Shadow2DRenderer?.WhiteSquare);
            t ??= Client.Instance.Frontend.Renderer.White;
            t.Bind();
        }

        private class ParticleAction
        {
            public readonly Kind type;
            public readonly ParticleContainer container;
            public readonly DataElement readData;

            public ParticleAction(Kind type, ParticleContainer container, DataElement data = null)
            {
                this.type = type;
                this.container = container;
                this.readData = data;
            }

            public enum Kind
            {
                Addition,
                Deletion,
                FullClear,
                Update
            }
        }
    }
}
