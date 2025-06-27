namespace VTT.Render
{
    using SixLabors.ImageSharp;
    using System;
    using System.Numerics;
    using VTT.Control;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Render.LightShadow;
    using VTT.Render.Shaders;
    using VTT.Util;
    using GL = GL.Bindings.GL;

    public class UniversalPipeline
    {
        public uint? FramebufferCompound { get; set; }
        public uint? FramebufferFinal { get; set; }

        public Texture PositionTex { get; set; }
        public Texture AlbedoTex { get; set; }
        public Texture EmissionTex { get; set; }
        public Texture NormalTex { get; set; }
        public Texture MRAOGTex { get; set; }
        public Texture DepthTex { get; set; }
        public Texture ColorOutputTex { get; set; }

        public FastAccessShader<DeferredUniforms> DeferredPrePass { get; set; }
        public FastAccessShader<DeferredFinalUniforms> DeferredFinal { get; set; }
        public FastAccessShader<ForwardUniforms> Forward { get; set; }
        public FastAccessShader<PipelineFinalUniforms> FinalPass { get; set; }


        public VertexArray FullScreenQuad { get; set; }
        private GPUBuffer _vbo;
        private GPUBuffer _ebo;

        public void Create()
        {
            this.CreateFullScreenQuad();
            this.RecompileShaders(Client.Instance.Settings.EnableDirectionalShadows, Client.Instance.Settings.EnableSunShadows);
        }

        public FastAccessShader<DeferredUniforms> BeginDeferred(Map m)
        {
            OpenGLUtil.StartSection("Setup deferred shader");
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            FastAccessShader<DeferredUniforms> shader = this.DeferredPrePass;
            bool useUBO = Client.Instance.Settings.UseUBO;
            shader.Bind();
            if (!useUBO)
            {
                shader.Uniforms.FrameData.View.Set(cam.View);
                shader.Uniforms.FrameData.Projection.Set(cam.Projection);
                shader.Uniforms.FrameData.CameraPosition.Set(cam.Position);
                shader.Uniforms.FrameData.Frame.Set((uint)Client.Instance.Frontend.FramesExisted);
                shader.Uniforms.FrameData.Update.Set((uint)Client.Instance.Frontend.UpdatesExisted);
                shader.Uniforms.FrameData.GridScale.Set(m.GridSize);
                shader.Uniforms.FrameData.CursorPosition.Set(Client.Instance.Frontend.Renderer.MapRenderer.TerrainHit ?? Vector3.Zero);
            }

            GL.BindFramebuffer(FramebufferTarget.All, this.FramebufferCompound.Value);
            GL.Viewport(0, 0, Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
            GL.ClearColor(0, 0, 0, 0);
            GLState.Clear(ClearBufferMask.Color | ClearBufferMask.Depth);
            GLState.DepthTest.Set(true);
            GLState.DepthFunc.Set(ComparisonMode.LessOrEqual);
            GLState.DepthMask.Set(true);
            GLState.CullFace.Set(true);
            GLState.CullFaceMode.Set(PolygonFaceMode.Back);
            if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
            {
                GLState.Multisample.Set(false);
            }

            OpenGLUtil.EndSection();
            return shader;
        }
        public FastAccessShader<ForwardUniforms> BeginForward(Map m, double delta)
        {
            OpenGLUtil.StartSection("Setup forward shader");
            GL.BindFramebuffer(FramebufferTarget.All, this.FramebufferCompound.Value);

            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;

            Vector3 cachedSunDir = Client.Instance.Frontend.Renderer.ObjectRenderer.CachedSunDirection;
            Vector3 cachedSunColor = Client.Instance.Frontend.Renderer.ObjectRenderer.CachedSunColor;
            Vector3 cachedAmbientColor = Client.Instance.Frontend.Renderer.ObjectRenderer.CachedAmbientColor;
            Vector3 cachedSkyColor = Client.Instance.Frontend.Renderer.ObjectRenderer.CachedSkyColor;

            FastAccessShader<ForwardUniforms> shader = this.Forward;
            shader.Bind();
            if (!Client.Instance.Settings.UseUBO)
            {
                UniformBlockFrameData uniforms = shader.Uniforms.FrameData;
                uniforms.View.Set(cam.View);
                uniforms.Projection.Set(cam.Projection);
                uniforms.Frame.Set((uint)Client.Instance.Frontend.FramesExisted);
                uniforms.Update.Set((uint)Client.Instance.Frontend.UpdatesExisted);
                uniforms.CameraPosition.Set(cam.Position);
                uniforms.CameraDirection.Set(cam.Direction);
                uniforms.SunDirection.Set(cachedSunDir);
                uniforms.SunColor.Set(cachedSunColor * m.SunIntensity);
                uniforms.AmbientColor.Set(cachedAmbientColor * m.AmbientIntensity);
                uniforms.SunView.Set(Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.SunView);
                uniforms.SunProjection.Set(Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.SunProjection);
                uniforms.SkyColor.Set(cachedSkyColor);
                uniforms.GridColor.Set(m.GridColor.Vec4());
                uniforms.GridScale.Set(m.GridSize);
                uniforms.CursorPosition.Set(Client.Instance.Frontend.Renderer.MapRenderer.TerrainHit ?? Vector3.Zero);
                uniforms.FrameDT.Set((float)delta);
                if (m.EnableDarkvision && m.DarkvisionData.TryGetValue(Client.Instance.ID, out (Guid, float) kv) && m.GetObject(kv.Item1, out MapObject mo))
                {
                    uniforms.DarkvisionData.Set(new Vector4(mo.Position, kv.Item2));
                }
                else
                {
                    uniforms.DarkvisionData.Set(Vector4.Zero);
                }
            }

            shader.Uniforms.Gamma.Factor.Set(Client.Instance.Settings.Gamma);
            shader.Uniforms.Gamma.EnableCorrection.Set(false);

            GLState.ActiveTexture.Set(14);
            if (m.EnableShadows && Client.Instance.Settings.EnableSunShadows)
            {
                Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.DepthTexture.Bind();
            }
            else
            {
                Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.DepthFakeTexture.Bind();
            }

            GLState.ActiveTexture.Set(13);
            plr.DepthMap.Bind();

            GLState.ActiveTexture.Set(0);
            plr.UniformLights(shader.Uniforms.PointLights);

            Client.Instance.Frontend.Renderer.SkyRenderer.SkyboxRenderer.UniformShader(shader.Uniforms.Skybox, m);
            OpenGLUtil.EndSection();

            return this.Forward;
        }
        public void EndDeferred(Map m)
        {
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            FastAccessShader<DeferredFinalUniforms> shader = this.DeferredFinal;
            Vector3 sunDir = Client.Instance.Frontend.Renderer.SkyRenderer.GetCurrentSunDirection();
            Color sunColor = Client.Instance.Frontend.Renderer.SkyRenderer.GetSunColor();
            Vector3 ambientColor = Client.Instance.Frontend.Renderer.SkyRenderer.GetAmbientColor().Vec3();
            Color skyColor = Client.Instance.Frontend.Renderer.SkyRenderer.GetSkyColor();
            SunShadowRenderer dlRenderer = Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer;
            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;
            bool useUBO = Client.Instance.Settings.UseUBO;

            GL.BindFramebuffer(FramebufferTarget.All, this.FramebufferFinal.Value);
            // Viewport already setup, just clear
            GL.ClearColor(0, 0, 0, 0);
            GLState.Clear(ClearBufferMask.Color | ClearBufferMask.Depth);

            shader.Bind();
            shader.Uniforms.Gamma.Factor.Set(Client.Instance.Settings.Gamma);
            shader.Uniforms.Gamma.EnableCorrection.Set(false);
            GLState.ActiveTexture.Set(12);
            this.PositionTex.Bind();
            GLState.ActiveTexture.Set(11);
            this.NormalTex.Bind();
            GLState.ActiveTexture.Set(10);
            this.AlbedoTex.Bind();
            GLState.ActiveTexture.Set(9);
            this.MRAOGTex.Bind();
            GLState.ActiveTexture.Set(8);
            this.EmissionTex.Bind();
            GLState.ActiveTexture.Set(7);
            this.DepthTex.Bind();
            if (!useUBO)
            {
                shader.Uniforms.EmissionSampler.Set(8);
                shader.Uniforms.FrameData.Frame.Set((uint)Client.Instance.Frontend.FramesExisted);
                shader.Uniforms.FrameData.Frame.Set((uint)Client.Instance.Frontend.UpdatesExisted);
                shader.Uniforms.FrameData.CameraPosition.Set(cam.Position);
                shader.Uniforms.FrameData.SunDirection.Set(sunDir);
                shader.Uniforms.FrameData.SunColor.Set(sunColor.Vec3() * m.SunIntensity);
                shader.Uniforms.FrameData.AmbientColor.Set(ambientColor * m.AmbientIntensity);
                shader.Uniforms.FrameData.SunView.Set(dlRenderer.SunView);
                shader.Uniforms.FrameData.SunProjection.Set(dlRenderer.SunProjection);
                shader.Uniforms.FrameData.SkyColor.Set(skyColor.Vec3());
                shader.Uniforms.FrameData.GridColor.Set(m.GridColor.Vec4());
                if (m.EnableDarkvision && m.DarkvisionData.TryGetValue(Client.Instance.ID, out (Guid, float) kv) && m.GetObject(kv.Item1, out MapObject mo))
                {
                    shader.Uniforms.FrameData.DarkvisionData.Set(new Vector4(mo.Position, kv.Item2));
                }
                else
                {
                    shader.Uniforms.FrameData.DarkvisionData.Set(Vector4.Zero);
                }
            }

            GLState.ActiveTexture.Set(14);
            if (m.EnableShadows && Client.Instance.Settings.EnableSunShadows)
            {
                dlRenderer.DepthTexture.Bind();
            }
            else
            {
                Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.DepthFakeTexture.Bind();
            }

            GLState.ActiveTexture.Set(13);
            plr.DepthMap.Bind();
            plr.UniformLights(shader.Uniforms.PointLights);
            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.Uniform(shader.Uniforms.FOW);
            Client.Instance.Frontend.Renderer.SkyRenderer.SkyboxRenderer.UniformShader(shader.Uniforms.Skybox, m);

            GLState.CullFace.Set(true);
            GLState.CullFaceMode.Set(PolygonFaceMode.Back);
            GLState.DepthTest.Set(true);
            GLState.DepthFunc.Set(ComparisonMode.Always);
            GLState.DepthMask.Set(true);

            this.DrawFullScreenQuad();

            GLState.DepthTest.Set(true);
            GLState.DepthFunc.Set(ComparisonMode.Less);
            GLState.CullFace.Set(false);

            // Bind default framebuffer and reset
            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GLState.ActiveTexture.Set(0);
        }

        public void FinishRender(Map m)
        {
            GL.BindFramebuffer(FramebufferTarget.All, 0);

            this.FinalPass.Bind();
            this.FinalPass.Uniforms.Gamma.Set(Client.Instance.Settings.Gamma);
            GLState.ActiveTexture.Set(0);
            this.ColorOutputTex.Bind();
            GLState.ActiveTexture.Set(1);
            this.DepthTex.Bind();
            GLState.ActiveTexture.Set(2);
            Client.Instance.Frontend.Renderer.ObjectRenderer.FastLightRenderer.TexColor.Bind();
            GLState.ActiveTexture.Set(3);
            if (m != null && m.Has2DShadows && m.Is2D)
            {
                Client.Instance.Frontend.Renderer.ObjectRenderer.Shadow2DRenderer.OutputTexture.Bind();
            }
            else
            {
                Client.Instance.Frontend.Renderer.ObjectRenderer.Shadow2DRenderer.WhiteSquare.Bind();
            }

            GLState.CullFace.Set(true);
            GLState.CullFaceMode.Set(PolygonFaceMode.Back);
            GLState.DepthTest.Set(true);
            GLState.DepthFunc.Set(ComparisonMode.Always);
            GLState.DepthMask.Set(true);

            this.DrawFullScreenQuad();

            GLState.DepthFunc.Set(ComparisonMode.LessOrEqual);
            GLState.CullFace.Set(false);
            GLState.ActiveTexture.Set(0);
        }

        public uint CreateDummyForwardFBO(Size s, out Texture pos, out Texture albedo, out Texture emission, out Texture normal, out Texture mraog, out Texture depth, out Texture colorOut)
        {
            pos = new Texture(TextureTarget.Texture2D);
            albedo = new Texture(TextureTarget.Texture2D);
            emission = new Texture(TextureTarget.Texture2D);
            normal = new Texture(TextureTarget.Texture2D);
            mraog = new Texture(TextureTarget.Texture2D);
            depth = new Texture(TextureTarget.Texture2D);
            colorOut = new Texture(TextureTarget.Texture2D);

            static void CheckFBO(string fbo)
            {
                FramebufferStatus fec = GL.CheckFramebufferStatus(FramebufferTarget.All);
                if (fec != FramebufferStatus.Complete)
                {
                    Client.Instance.Logger.Log(LogLevel.Fatal, $"Could not complete {fbo} framebuffer!");
                    Client.Instance.Logger.Log(LogLevel.Fatal, "  " + fec);
                    throw new Exception("Framebuffer could not complete");
                }
            }

            int w = s.Width;
            int h = s.Height;

            uint fbo = GL.GenFramebuffer();
            ResizeTex(pos, w, h, Client.Instance.Settings.UseHalfPrecision ? SizedInternalFormat.RgbaHalf : SizedInternalFormat.RgbaFloat, PixelDataFormat.Rgba, PixelDataType.Float);
            ResizeTex(albedo, w, h, SizedInternalFormat.Rgba8, PixelDataFormat.Rgba, PixelDataType.Byte);
            ResizeTex(emission, w, h, SizedInternalFormat.Rgba8, PixelDataFormat.Rgba, PixelDataType.Byte);
            ResizeTex(normal, w, h, Client.Instance.Settings.UseHalfPrecision ? SizedInternalFormat.RgbaHalf : SizedInternalFormat.RgbaFloat, PixelDataFormat.Rgba, PixelDataType.Float);
            ResizeTex(mraog, w, h, SizedInternalFormat.Rgba8, PixelDataFormat.Rgba, PixelDataType.Byte);
            ResizeTex(depth, w, h, SizedInternalFormat.DepthComponent32Float, PixelDataFormat.DepthComponent, PixelDataType.Float);
            ResizeTex(colorOut, w, h, SizedInternalFormat.Rgba8, PixelDataFormat.Rgba, PixelDataType.Byte);

            GL.BindFramebuffer(FramebufferTarget.All, fbo);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Depth, TextureTarget.Texture2D, depth, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color0, TextureTarget.Texture2D, colorOut, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color1, TextureTarget.Texture2D, pos, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color2, TextureTarget.Texture2D, normal, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color3, TextureTarget.Texture2D, albedo, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color4, TextureTarget.Texture2D, mraog, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color5, TextureTarget.Texture2D, emission, 0);
            GL.DrawBuffers(new DrawBufferMode[] { DrawBufferMode.Color0, DrawBufferMode.Color1, DrawBufferMode.Color2, DrawBufferMode.Color3, DrawBufferMode.Color4, DrawBufferMode.Color5 });
            CheckFBO("compound dummy");

            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GL.DrawBuffer(DrawBufferMode.Back);

            return fbo;
        }

        public static uint UseFBO(uint? fbo, string name)
        {
            if (!fbo.HasValue)
            {
                fbo = GL.GenFramebuffer();
                GL.BindFramebuffer(FramebufferTarget.All, fbo.Value);
                OpenGLUtil.NameObject(GLObjectType.Framebuffer, fbo.Value, name);
            }
            else
            {
                GL.BindFramebuffer(FramebufferTarget.All, fbo.Value);
            }

            return fbo.Value;
        }

        public static void CheckFBO(string fbo)
        {
            FramebufferStatus fec = GL.CheckFramebufferStatus(FramebufferTarget.All);
            if (fec != FramebufferStatus.Complete)
            {
                Client.Instance.Logger.Log(LogLevel.Fatal, $"Could not complete {fbo} framebuffer!");
                Client.Instance.Logger.Log(LogLevel.Fatal, "  " + fec);
                throw new Exception("Framebuffer could not complete");
            }
        }

        public void CreateFullScreenQuad()
        {
            this.FullScreenQuad = new VertexArray();
            this._vbo = new GPUBuffer(BufferTarget.Array);
            this._ebo = new GPUBuffer(BufferTarget.ElementArray);
            this.FullScreenQuad.Bind();
            this._vbo.Bind();
            this._vbo.SetData(new float[] {
                -1.0f, 1.0f, 0f, 1f,
                1.0f, -1.0f, 1f, 0f,
                1.0f, 1.0f, 1f, 1f,
                //-1.0f, 1.0f, 0f, 1f,
                -1.0f, -1.0f, 0f, 0f,
                //1.0f, -1.0f, 1f, 0f
            });

            this._ebo.Bind();
            this._ebo.SetData(new byte[] {
                0, 1, 2, 0, 3, 1
            });

            this.FullScreenQuad.SetVertexSize<float>(4);
            this.FullScreenQuad.PushElement(ElementType.Vec2);
            this.FullScreenQuad.PushElement(ElementType.Vec2);

            OpenGLUtil.NameObject(GLObjectType.VertexArray, this.FullScreenQuad, "Fullscreen quad vao");
            OpenGLUtil.NameObject(GLObjectType.Buffer, this._vbo, "Fullscreen quad vbo");
            OpenGLUtil.NameObject(GLObjectType.Buffer, this._ebo, "Fullscreen quad ebo");
        }
        public void DrawFullScreenQuad()
        {
            this.FullScreenQuad.Bind();
            GLState.DrawElements(PrimitiveType.Triangles, 6, ElementsType.Byte, IntPtr.Zero);
        }
        public void RecompileShaders(bool havePointShadows, bool haveSunShadows)
        {
            this.DeferredPrePass?.Program.Dispose();
            this.DeferredFinal?.Program.Dispose();
            this.Forward?.Program.Dispose();
            this.FinalPass?.Program.Dispose();

            this.DeferredPrePass = new FastAccessShader<DeferredUniforms>(this.CompileShader("deferred", haveSunShadows, havePointShadows));
            this.DeferredPrePass.Bind();
            this.DeferredPrePass.Program.BindUniformBlock("FrameData", 1);
            this.DeferredPrePass.Program.BindUniformBlock("BoneData", 2);
            this.DeferredPrePass.Uniforms.Material.DiffuseSampler.Set(0);
            this.DeferredPrePass.Uniforms.Material.NormalSampler.Set(1);
            this.DeferredPrePass.Uniforms.Material.EmissiveSampler.Set(2);
            this.DeferredPrePass.Uniforms.Material.AOMRSampler.Set(3);

            this.DeferredFinal = new FastAccessShader<DeferredFinalUniforms>(this.CompileShader("deferred_final", haveSunShadows, havePointShadows));
            this.DeferredFinal.Bind();
            this.DeferredFinal.Program.BindUniformBlock("FrameData", 1);
            this.DeferredFinal.Uniforms.PositionsSampler.Set(12);
            this.DeferredFinal.Uniforms.NormalsSampler.Set(11);
            this.DeferredFinal.Uniforms.AlbedoSampler.Set(10);
            this.DeferredFinal.Uniforms.AOMRGSampler.Set(9);
            this.DeferredFinal.Uniforms.EmissionSampler.Set(8);
            this.DeferredFinal.Uniforms.DepthSampler.Set(7);
            this.DeferredFinal.Uniforms.Skybox.Sampler.Set(6);
            this.DeferredFinal.Uniforms.DirectionalLight.DepthSampler.Set(14);
            this.DeferredFinal.Uniforms.PointLights.ShadowMapsSampler.Set(13);

            this.Forward = new FastAccessShader<ForwardUniforms>(this.CompileShader("object", haveSunShadows, havePointShadows));
            this.Forward.Bind();
            this.Forward.Program.BindUniformBlock("FrameData", 1);
            this.Forward.Program.BindUniformBlock("BoneData", 2);
            this.Forward.Uniforms.Material.DiffuseSampler.Set(0);
            this.Forward.Uniforms.Material.NormalSampler.Set(1);
            this.Forward.Uniforms.Material.EmissiveSampler.Set(2);
            this.Forward.Uniforms.Material.AOMRSampler.Set(3);
            this.Forward.Uniforms.PointLights.ShadowMapsSampler.Set(13);
            this.Forward.Uniforms.DirectionalLight.DepthSampler.Set(14);
            this.Forward.Uniforms.Skybox.Sampler.Set(6);

            this.FinalPass = new FastAccessShader<PipelineFinalUniforms>(this.CompileShader("universal_final", haveSunShadows, havePointShadows));
            this.FinalPass.Bind();
            this.FinalPass.Uniforms.ColorSampler.Set(0);
            this.FinalPass.Uniforms.DepthSampler.Set(1);
            this.FinalPass.Uniforms.FastLightSampler.Set(2);
            this.FinalPass.Uniforms.Shadows2DSampler.Set(3);
        }
        public ShaderProgram CompileShader(string sName, bool dirShadows, bool pointShadows)
        {
            static void RemoveDefine(ref string lines, string define)
            {
                string r = "#define " + define;
                int idx = lines.IndexOf(r);
                if (idx != -1)
                {
                    lines = lines.Remove(idx, lines.IndexOf('\n', idx) - idx - 1);
                }
            }

            string lineVert = IOVTT.ResourceToString($"VTT.Embed.{sName}.vert");
            string lineFrag = IOVTT.ResourceToString($"VTT.Embed.{sName}.frag");
            if (!dirShadows)
            {
                RemoveDefine(ref lineVert, "HAS_DIRECTIONAL_SHADOWS");
                RemoveDefine(ref lineFrag, "HAS_DIRECTIONAL_SHADOWS");
            }

            if (!pointShadows)
            {
                RemoveDefine(ref lineVert, "HAS_POINT_SHADOWS");
                RemoveDefine(ref lineFrag, "HAS_POINT_SHADOWS");
            }

            lineFrag = lineFrag.Replace("#define PCF_ITERATIONS 2", $"#define PCF_ITERATIONS {Client.Instance.Settings.ShadowsPCF}");

            if (!ShaderProgram.TryCompile(out ShaderProgram sp, lineVert, null, lineFrag, out string err))
            {
                Logger l = Client.Instance.Logger;
                l.Log(LogLevel.Fatal, $"Could not compile shader {sName}! Shader error was " + err);
                throw new Exception($"Could not compile object shader {sName}! Shader error was " + err);
            }

            OpenGLUtil.NameObject(GLObjectType.Program, sp, sName.Capitalize() + " program");
            return sp;
        }

        public static void ResizeTex(Texture t, int w, int h, SizedInternalFormat pif, PixelDataFormat pf, PixelDataType pt)
        {
            t.Bind();
            t.Size = new Size(w, h);
            t.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            t.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, pif, w, h, pf, pt, IntPtr.Zero);
        }

        public void Resize(int w, int h)
        {
            if (w == 0 || h == 0)
            {
                return;
            }

            this.PositionTex?.Dispose();
            this.AlbedoTex?.Dispose();
            this.EmissionTex?.Dispose();
            this.NormalTex?.Dispose();
            this.MRAOGTex?.Dispose();
            this.DepthTex?.Dispose();
            this.ColorOutputTex?.Dispose();

            this.PositionTex = new Texture(TextureTarget.Texture2D);
            this.AlbedoTex = new Texture(TextureTarget.Texture2D);
            this.EmissionTex = new Texture(TextureTarget.Texture2D);
            this.NormalTex = new Texture(TextureTarget.Texture2D);
            this.MRAOGTex = new Texture(TextureTarget.Texture2D);
            this.DepthTex = new Texture(TextureTarget.Texture2D);
            this.ColorOutputTex = new Texture(TextureTarget.Texture2D);

            ResizeTex(this.PositionTex, w, h, Client.Instance.Settings.UseHalfPrecision ? SizedInternalFormat.RgbaHalf : SizedInternalFormat.RgbaFloat, PixelDataFormat.Rgba, PixelDataType.Float);
            ResizeTex(this.AlbedoTex, w, h, SizedInternalFormat.Rgba8, PixelDataFormat.Rgba, PixelDataType.Byte);
            ResizeTex(this.EmissionTex, w, h, SizedInternalFormat.Rgba8, PixelDataFormat.Rgba, PixelDataType.Byte);
            ResizeTex(this.NormalTex, w, h, Client.Instance.Settings.UseHalfPrecision ? SizedInternalFormat.RgbaHalf : SizedInternalFormat.RgbaFloat, PixelDataFormat.Rgba, PixelDataType.Float);
            ResizeTex(this.MRAOGTex, w, h, SizedInternalFormat.Rgba8, PixelDataFormat.Rgba, PixelDataType.Byte);
            ResizeTex(this.DepthTex, w, h, SizedInternalFormat.DepthComponent32Float, PixelDataFormat.DepthComponent, PixelDataType.Float);
            ResizeTex(this.ColorOutputTex, w, h, Client.Instance.Settings.UseHalfPrecision ? SizedInternalFormat.RgbaHalf : SizedInternalFormat.RgbaFloat, PixelDataFormat.Rgba, PixelDataType.Float);

            OpenGLUtil.NameObject(GLObjectType.Texture, this.PositionTex, "Pipeline positions texture 32f");
            OpenGLUtil.NameObject(GLObjectType.Texture, this.AlbedoTex, "Pipeline albedo texture 8b");
            OpenGLUtil.NameObject(GLObjectType.Texture, this.EmissionTex, "Pipeline emission texture 8b");
            OpenGLUtil.NameObject(GLObjectType.Texture, this.NormalTex, "Pipeline normal texture 32f");
            OpenGLUtil.NameObject(GLObjectType.Texture, this.MRAOGTex, "Pipeline metal/rough/ao/grid texture 8b");
            OpenGLUtil.NameObject(GLObjectType.Texture, this.DepthTex, "Pipeline depth texture 32d");
            OpenGLUtil.NameObject(GLObjectType.Texture, this.ColorOutputTex, "Pipeline color composite texture 32f");

            this.FramebufferCompound = UseFBO(this.FramebufferCompound, "Pipeline composite fbo");
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Depth, TextureTarget.Texture2D, this.DepthTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color0, TextureTarget.Texture2D, this.ColorOutputTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color1, TextureTarget.Texture2D, this.PositionTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color2, TextureTarget.Texture2D, this.NormalTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color3, TextureTarget.Texture2D, this.AlbedoTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color4, TextureTarget.Texture2D, this.MRAOGTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color5, TextureTarget.Texture2D, this.EmissionTex, 0);
            GL.DrawBuffers(new DrawBufferMode[] { DrawBufferMode.Color0, DrawBufferMode.Color1, DrawBufferMode.Color2, DrawBufferMode.Color3, DrawBufferMode.Color4, DrawBufferMode.Color5 });
            CheckFBO("Pipeline composite fbo");

            this.FramebufferFinal = UseFBO(this.FramebufferFinal, "Pipeline deferred final pass fbo");
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color0, TextureTarget.Texture2D, this.ColorOutputTex, 0);
            GL.DrawBuffers(new DrawBufferMode[] { DrawBufferMode.Color0 });
            CheckFBO("Pipeline deferred final pass fbo");

            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
        }
    }
}
