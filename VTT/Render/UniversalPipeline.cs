namespace VTT.Render
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Render.LightShadow;
    using VTT.Util;

    public class UniversalPipeline
    {
        public int? FramebufferCompound { get; set; }
        public int? FramebufferFinal { get; set; }

        public Texture PositionTex { get; set; }
        public Texture AlbedoTex { get; set; }
        public Texture EmissionTex { get; set; }
        public Texture NormalTex { get; set; }
        public Texture MRAOGTex { get; set; }
        public Texture DepthTex { get; set; }
        public Texture ColorOutputTex { get; set; }

        public ShaderProgram DeferredPrePass { get; set; }
        public ShaderProgram DeferredFinal { get; set; }
        public ShaderProgram Forward { get; set; }
        public ShaderProgram FinalPass { get; set; }


        public VertexArray FullScreenQuad { get; set; }
        private GPUBuffer _vbo;
        private GPUBuffer _ebo;

        public void Create()
        {
            this.CreateFullScreenQuad();
            this.RecompileShaders(Client.Instance.Settings.EnableDirectionalShadows, Client.Instance.Settings.EnableSunShadows);
        }

        public ShaderProgram BeginDeferred(Map m)
        {
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            ShaderProgram shader = this.DeferredPrePass;
            bool useUBO = Client.Instance.Settings.UseUBO;

            shader.Bind();
            if (!useUBO)
            {
                shader["view"].Set(cam.View);
                shader["projection"].Set(cam.Projection);
                shader["camera_position"].Set(cam.Position);
                shader["frame"].Set((uint)Client.Instance.Frontend.FramesExisted);
                shader["update"].Set((uint)Client.Instance.Frontend.UpdatesExisted);
                shader["grid_size"].Set(m.GridSize);
                shader["cursor_position"].Set(Client.Instance.Frontend.Renderer.RulerRenderer.TerrainHit ?? Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld ?? Vector3.Zero);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, this.FramebufferCompound.Value);
            GL.Viewport(0, 0, Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.DepthMask(true);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
            {
                GL.Disable(EnableCap.Multisample);
            }

            return shader;
        }
        public ShaderProgram BeginForward(Map m, double delta)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, this.FramebufferCompound.Value);

            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;

            Vector3 cachedSunDir = Client.Instance.Frontend.Renderer.ObjectRenderer.CachedSunDirection;
            Vector3 cachedSunColor = Client.Instance.Frontend.Renderer.ObjectRenderer.CachedSunColor;
            Vector3 cachedAmbientColor = Client.Instance.Frontend.Renderer.ObjectRenderer.CachedAmbientColor;
            Vector3 cachedSkyColor = Client.Instance.Frontend.Renderer.ObjectRenderer.CachedSkyColor;

            ShaderProgram shader = this.Forward;
            shader.Bind();
            if (!Client.Instance.Settings.UseUBO)
            {
                shader["view"].Set(cam.View);
                shader["projection"].Set(cam.Projection);
                shader["frame"].Set((uint)Client.Instance.Frontend.FramesExisted);
                shader["update"].Set((uint)Client.Instance.Frontend.UpdatesExisted);
                shader["camera_position"].Set(cam.Position);
                shader["camera_direction"].Set(cam.Direction);
                shader["dl_direction"].Set(cachedSunDir);
                shader["dl_color"].Set(cachedSunColor * m.SunIntensity);
                shader["al_color"].Set(cachedAmbientColor * m.AmbietIntensity);
                shader["sun_view"].Set(Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.SunView);
                shader["sun_projection"].Set(Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.SunProjection);
                shader["sky_color"].Set(cachedSkyColor);
                shader["grid_color"].Set(m.GridColor.Vec4());
                shader["grid_size"].Set(m.GridSize);
                shader["cursor_position"].Set(Client.Instance.Frontend.Renderer.RulerRenderer.TerrainHit ?? Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld ?? Vector3.Zero);
                shader["dv_data"].Set(Vector4.Zero);
                shader["frame_delta"].Set((float)delta);
                if (m.EnableDarkvision)
                {
                    if (m.DarkvisionData.TryGetValue(Client.Instance.ID, out (Guid, float) kv))
                    {
                        if (m.GetObject(kv.Item1, out MapObject mo))
                        {
                            shader["dv_data"].Set(new Vector4(mo.Position, kv.Item2));
                        }
                    }
                }
            }

            shader["gamma_factor"].Set(Client.Instance.Settings.Gamma);
            shader["gamma_correct"].Set(false);

            GL.ActiveTexture(TextureUnit.Texture14);
            if (m.EnableShadows && Client.Instance.Settings.EnableSunShadows)
            {
                Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.DepthTexture.Bind();
            }
            else
            {
                Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.DepthFakeTexture.Bind();
            }

            GL.ActiveTexture(TextureUnit.Texture13);
            plr.DepthMap.Bind();

            GL.ActiveTexture(TextureUnit.Texture0);
            plr.UniformLights(shader);

            return this.Forward;
        }
        public void EndDeferred(Map m)
        {
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            ShaderProgram shader = this.DeferredFinal;
            Vector3 sunDir = Client.Instance.Frontend.Renderer.SkyRenderer.GetCurrentSunDirection();
            Color sunColor = Client.Instance.Frontend.Renderer.SkyRenderer.GetSunColor();
            Vector3 ambientColor = Client.Instance.Frontend.Renderer.SkyRenderer.GetAmbientColor().Vec3();
            Color skyColor = Client.Instance.Frontend.Renderer.SkyRenderer.GetSkyColor();
            SunShadowRenderer dlRenderer = Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer;
            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;
            bool useUBO = Client.Instance.Settings.UseUBO;

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, this.FramebufferFinal.Value);
            // Viewport already setup, just clear
            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            shader.Bind();
            shader["gamma_factor"].Set(Client.Instance.Settings.Gamma);
            shader["gamma_correct"].Set(false);
            GL.ActiveTexture(TextureUnit.Texture12);
            this.PositionTex.Bind();
            GL.ActiveTexture(TextureUnit.Texture11);
            this.NormalTex.Bind();
            GL.ActiveTexture(TextureUnit.Texture10);
            this.AlbedoTex.Bind();
            GL.ActiveTexture(TextureUnit.Texture9);
            this.MRAOGTex.Bind();
            GL.ActiveTexture(TextureUnit.Texture8);
            this.EmissionTex.Bind();
            GL.ActiveTexture(TextureUnit.Texture7);
            this.DepthTex.Bind();
            if (!useUBO)
            {
                shader["g_emission"].Set(8);
                shader["frame"].Set((uint)Client.Instance.Frontend.FramesExisted);
                shader["update"].Set((uint)Client.Instance.Frontend.UpdatesExisted);
                shader["camera_position"].Set(cam.Position);
                shader["dl_direction"].Set(sunDir);
                shader["dl_color"].Set(sunColor.Vec3() * m.SunIntensity);
                shader["al_color"].Set(ambientColor * m.AmbietIntensity);
                shader["sun_view"].Set(dlRenderer.SunView);
                shader["sun_projection"].Set(dlRenderer.SunProjection);
                shader["sky_color"].Set(skyColor.Vec3());
                shader["grid_color"].Set(m.GridColor.Vec4());
                shader["dv_data"].Set(Vector4.Zero);
                if (m.EnableDarkvision)
                {
                    if (m.DarkvisionData.TryGetValue(Client.Instance.ID, out (Guid, float) kv))
                    {
                        if (m.GetObject(kv.Item1, out MapObject mo))
                        {
                            shader["dv_data"].Set(new Vector4(mo.Position, kv.Item2 / m.GridUnit));
                        }
                    }
                }
            }

            GL.ActiveTexture(TextureUnit.Texture14);
            if (m.EnableShadows && Client.Instance.Settings.EnableSunShadows)
            {
                dlRenderer.DepthTexture.Bind();
            }
            else
            {
                Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.DepthFakeTexture.Bind();
            }

            GL.ActiveTexture(TextureUnit.Texture13);
            plr.DepthMap.Bind();
            plr.UniformLights(shader);
            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.Uniform(shader);

            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Always);
            GL.DepthMask(true);

            this.DrawFullScreenQuad();

            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.Disable(EnableCap.CullFace);

            // Bind default framebuffer and reset
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        public void FinishRender()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DrawBuffer(DrawBufferMode.Back);

            this.FinalPass.Bind();
            this.FinalPass["gamma"].Set(Client.Instance.Settings.Gamma);
            GL.ActiveTexture(TextureUnit.Texture0);
            this.ColorOutputTex.Bind();
            GL.ActiveTexture(TextureUnit.Texture1);
            this.DepthTex.Bind();
            GL.ActiveTexture(TextureUnit.Texture2);
            Client.Instance.Frontend.Renderer.ObjectRenderer.FastLightRenderer.TexColor.Bind();

            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Always);
            GL.DepthMask(true);

            this.DrawFullScreenQuad();

            GL.DepthFunc(DepthFunction.Lequal);
            GL.Disable(EnableCap.CullFace);
        }

        public int CreateDummyForwardFBO(Size s, out Texture pos, out Texture albedo, out Texture emission, out Texture normal, out Texture mraog, out Texture depth, out Texture colorOut)
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
                FramebufferErrorCode fec = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                if (fec != FramebufferErrorCode.FramebufferComplete)
                {
                    Client.Instance.Logger.Log(LogLevel.Fatal, $"Could not complete {fbo} framebuffer!");
                    Client.Instance.Logger.Log(LogLevel.Fatal, "  " + fec);
                    throw new Exception("Framebuffer could not complete");
                }
            }

            int w = s.Width;
            int h = s.Height;

            int fbo = GL.GenFramebuffer();
            ResizeTex(pos, w, h, Client.Instance.Settings.UseHalfPrecision ? PixelInternalFormat.Rgba16f : PixelInternalFormat.Rgba32f, PixelFormat.Rgba, PixelType.Float);
            ResizeTex(albedo, w, h, PixelInternalFormat.Rgba, PixelFormat.Rgba, PixelType.UnsignedByte);
            ResizeTex(emission, w, h, PixelInternalFormat.Rgba, PixelFormat.Rgba, PixelType.UnsignedByte);
            ResizeTex(normal, w, h, Client.Instance.Settings.UseHalfPrecision ? PixelInternalFormat.Rgba16f : PixelInternalFormat.Rgba32f, PixelFormat.Rgba, PixelType.Float);
            ResizeTex(mraog, w, h, PixelInternalFormat.Rgba, PixelFormat.Rgba, PixelType.UnsignedByte);
            ResizeTex(depth, w, h, PixelInternalFormat.DepthComponent32f, PixelFormat.DepthComponent, PixelType.Float);
            ResizeTex(colorOut, w, h, PixelInternalFormat.Rgba, PixelFormat.Rgba, PixelType.UnsignedByte);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, depth, 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, colorOut, 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, pos, 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2D, normal, 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment3, TextureTarget.Texture2D, albedo, 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment4, TextureTarget.Texture2D, mraog, 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment5, TextureTarget.Texture2D, emission, 0);
            GL.DrawBuffers(6, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1, DrawBuffersEnum.ColorAttachment2, DrawBuffersEnum.ColorAttachment3, DrawBuffersEnum.ColorAttachment4, DrawBuffersEnum.ColorAttachment5 });
            CheckFBO("compound dummy");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DrawBuffer(DrawBufferMode.Back);

            return fbo;
        }

        public static int UseFBO(int? fbo)
        {
            if (!fbo.HasValue)
            {
                fbo = GL.GenFramebuffer();
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo.Value);
            return fbo.Value;
        }

        public static void CheckFBO(string fbo)
        {
            FramebufferErrorCode fec = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (fec != FramebufferErrorCode.FramebufferComplete)
            {
                Client.Instance.Logger.Log(LogLevel.Fatal, $"Could not complete {fbo} framebuffer!");
                Client.Instance.Logger.Log(LogLevel.Fatal, "  " + fec);
                throw new Exception("Framebuffer could not complete");
            }
        }

        public void CreateFullScreenQuad()
        {
            this.FullScreenQuad = new VertexArray();
            this._vbo = new GPUBuffer(BufferTarget.ArrayBuffer);
            this._ebo = new GPUBuffer(BufferTarget.ElementArrayBuffer);
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
        }
        public void DrawFullScreenQuad()
        {
            this.FullScreenQuad.Bind();
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedByte, IntPtr.Zero);
        }
        public void RecompileShaders(bool havePointShadows, bool haveSunShadows)
        {
            this.DeferredPrePass?.Dispose();
            this.DeferredFinal?.Dispose();
            this.Forward?.Dispose();
            this.FinalPass?.Dispose();

            this.DeferredPrePass = this.CompileShader("deferred", haveSunShadows, havePointShadows);
            this.DeferredPrePass.Bind();
            this.DeferredPrePass.BindUniformBlock("FrameData", 1);
            this.DeferredPrePass["m_texture_diffuse"].Set(0);
            this.DeferredPrePass["m_texture_normal"].Set(1);
            this.DeferredPrePass["m_texture_emissive"].Set(2);
            this.DeferredPrePass["m_texture_aomr"].Set(3);

            this.DeferredFinal = this.CompileShader("deferred_final", haveSunShadows, havePointShadows);
            this.DeferredFinal.Bind();
            this.DeferredFinal.BindUniformBlock("FrameData", 1);
            this.DeferredFinal["g_positions"].Set(12);
            this.DeferredFinal["g_normals"].Set(11);
            this.DeferredFinal["g_albedo"].Set(10);
            this.DeferredFinal["g_aomrg"].Set(9);
            this.DeferredFinal["g_emission"].Set(8);
            this.DeferredFinal["g_depth"].Set(7);
            this.DeferredFinal["dl_shadow_map"].Set(14);
            this.DeferredFinal["pl_shadow_maps"].Set(13);

            this.Forward = this.CompileShader("object", haveSunShadows, havePointShadows);
            this.Forward.Bind();
            this.Forward.BindUniformBlock("FrameData", 1);
            this.Forward["m_texture_diffuse"].Set(0);
            this.Forward["m_texture_normal"].Set(1);
            this.Forward["m_texture_emissive"].Set(2);
            this.Forward["m_texture_aomr"].Set(3);
            this.Forward["pl_shadow_maps"].Set(13);
            this.Forward["dl_shadow_map"].Set(14);

            this.FinalPass = this.CompileShader("universal_final", haveSunShadows, havePointShadows);
            this.FinalPass.Bind();
            this.FinalPass["g_color"].Set(0);
            this.FinalPass["g_depth"].Set(1);
            this.FinalPass["g_fast_light"].Set(2);
        }
        public ShaderProgram CompileShader(string sName, bool dirShadows, bool pointShadows)
        {
            void RemoveDefine(ref string lines, string define)
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

            return sp;
        }

        public static void ResizeTex(Texture t, int w, int h, PixelInternalFormat pif, PixelFormat pf, PixelType pt)
        {
            t.Bind();
            t.Size = new Size(w, h);
            t.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            t.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, pif, w, h, 0, pf, pt, IntPtr.Zero);
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

            ResizeTex(this.PositionTex, w, h, Client.Instance.Settings.UseHalfPrecision ? PixelInternalFormat.Rgba16f : PixelInternalFormat.Rgba32f, PixelFormat.Rgba, PixelType.Float);
            ResizeTex(this.AlbedoTex, w, h, PixelInternalFormat.Rgba, PixelFormat.Rgba, PixelType.UnsignedByte);
            ResizeTex(this.EmissionTex, w, h, PixelInternalFormat.Rgba, PixelFormat.Rgba, PixelType.UnsignedByte);
            ResizeTex(this.NormalTex, w, h, Client.Instance.Settings.UseHalfPrecision ? PixelInternalFormat.Rgba16f : PixelInternalFormat.Rgba32f, PixelFormat.Rgba, PixelType.Float);
            ResizeTex(this.MRAOGTex, w, h, PixelInternalFormat.Rgba, PixelFormat.Rgba, PixelType.UnsignedByte);
            ResizeTex(this.DepthTex, w, h, PixelInternalFormat.DepthComponent32f, PixelFormat.DepthComponent, PixelType.Float);
            ResizeTex(this.ColorOutputTex, w, h, PixelInternalFormat.Rgba, PixelFormat.Rgba, PixelType.UnsignedByte);

            this.FramebufferCompound = UseFBO(this.FramebufferCompound);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, this.DepthTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, this.ColorOutputTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, this.PositionTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2D, this.NormalTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment3, TextureTarget.Texture2D, this.AlbedoTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment4, TextureTarget.Texture2D, this.MRAOGTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment5, TextureTarget.Texture2D, this.EmissionTex, 0);
            GL.DrawBuffers(6, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1, DrawBuffersEnum.ColorAttachment2, DrawBuffersEnum.ColorAttachment3, DrawBuffersEnum.ColorAttachment4, DrawBuffersEnum.ColorAttachment5 });
            CheckFBO("universal compound");

            this.FramebufferFinal = UseFBO(this.FramebufferFinal);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, this.ColorOutputTex, 0);
            GL.DrawBuffers(1, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0 });
            CheckFBO("deferred final");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
        }
    }
}
