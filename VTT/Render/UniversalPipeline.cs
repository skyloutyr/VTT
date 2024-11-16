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

        public FastAccessShader DeferredPrePass { get; set; }
        public ShaderProgram DeferredFinal { get; set; }
        public FastAccessShader Forward { get; set; }
        public ShaderProgram FinalPass { get; set; }


        public VertexArray FullScreenQuad { get; set; }
        private GPUBuffer _vbo;
        private GPUBuffer _ebo;

        public void Create()
        {
            this.CreateFullScreenQuad();
            this.RecompileShaders(Client.Instance.Settings.EnableDirectionalShadows, Client.Instance.Settings.EnableSunShadows);
        }

        public FastAccessShader BeginDeferred(Map m)
        {
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            FastAccessShader shader = this.DeferredPrePass;
            bool useUBO = Client.Instance.Settings.UseUBO;

            shader.Program.Bind();
            if (!useUBO)
            {
                shader.Essentials.View.Set(cam.View);
                shader.Essentials.Projection.Set(cam.Projection);
                shader["camera_position"].Set(cam.Position);
                shader["frame"].Set((uint)Client.Instance.Frontend.FramesExisted);
                shader["update"].Set((uint)Client.Instance.Frontend.UpdatesExisted);
                shader["grid_size"].Set(m.GridSize);
                shader["cursor_position"].Set(Client.Instance.Frontend.Renderer.MapRenderer.TerrainHit ?? Vector3.Zero);
            }

            GL.BindFramebuffer(FramebufferTarget.All, this.FramebufferCompound.Value);
            GL.Viewport(0, 0, Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.Color | ClearBufferMask.Depth);
            GL.Enable(Capability.DepthTest);
            GL.DepthFunction(ComparisonMode.LessOrEqual);
            GL.DepthMask(true);
            GL.Enable(Capability.CullFace);
            GL.CullFace(PolygonFaceMode.Back);
            if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
            {
                GL.Disable(Capability.Multisample);
            }

            return shader;
        }
        public FastAccessShader BeginForward(Map m, double delta)
        {
            GL.BindFramebuffer(FramebufferTarget.All, this.FramebufferCompound.Value);

            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;

            Vector3 cachedSunDir = Client.Instance.Frontend.Renderer.ObjectRenderer.CachedSunDirection;
            Vector3 cachedSunColor = Client.Instance.Frontend.Renderer.ObjectRenderer.CachedSunColor;
            Vector3 cachedAmbientColor = Client.Instance.Frontend.Renderer.ObjectRenderer.CachedAmbientColor;
            Vector3 cachedSkyColor = Client.Instance.Frontend.Renderer.ObjectRenderer.CachedSkyColor;

            FastAccessShader shader = this.Forward;
            shader.Program.Bind();
            if (!Client.Instance.Settings.UseUBO)
            {
                shader.Essentials.View.Set(cam.View);
                shader.Essentials.Projection.Set(cam.Projection);
                shader["frame"].Set((uint)Client.Instance.Frontend.FramesExisted);
                shader["update"].Set((uint)Client.Instance.Frontend.UpdatesExisted);
                shader["camera_position"].Set(cam.Position);
                shader["camera_direction"].Set(cam.Direction);
                shader["dl_direction"].Set(cachedSunDir);
                shader["dl_color"].Set(cachedSunColor * m.SunIntensity);
                shader["al_color"].Set(cachedAmbientColor * m.AmbientIntensity);
                shader["sun_view"].Set(Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.SunView);
                shader["sun_projection"].Set(Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.SunProjection);
                shader["sky_color"].Set(cachedSkyColor);
                shader["grid_color"].Set(m.GridColor.Vec4());
                shader["grid_size"].Set(m.GridSize);
                shader["cursor_position"].Set(Client.Instance.Frontend.Renderer.MapRenderer.TerrainHit ?? Vector3.Zero);
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

            GL.ActiveTexture(14);
            if (m.EnableShadows && Client.Instance.Settings.EnableSunShadows)
            {
                Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.DepthTexture.Bind();
            }
            else
            {
                Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.DepthFakeTexture.Bind();
            }

            GL.ActiveTexture(13);
            plr.DepthMap.Bind();

            GL.ActiveTexture(0);
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

            GL.BindFramebuffer(FramebufferTarget.All, this.FramebufferFinal.Value);
            // Viewport already setup, just clear
            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.Color | ClearBufferMask.Depth);

            shader.Bind();
            shader["gamma_factor"].Set(Client.Instance.Settings.Gamma);
            shader["gamma_correct"].Set(false);
            GL.ActiveTexture(12);
            this.PositionTex.Bind();
            GL.ActiveTexture(11);
            this.NormalTex.Bind();
            GL.ActiveTexture(10);
            this.AlbedoTex.Bind();
            GL.ActiveTexture(9);
            this.MRAOGTex.Bind();
            GL.ActiveTexture(8);
            this.EmissionTex.Bind();
            GL.ActiveTexture(7);
            this.DepthTex.Bind();
            if (!useUBO)
            {
                shader["g_emission"].Set(8);
                shader["frame"].Set((uint)Client.Instance.Frontend.FramesExisted);
                shader["update"].Set((uint)Client.Instance.Frontend.UpdatesExisted);
                shader["camera_position"].Set(cam.Position);
                shader["dl_direction"].Set(sunDir);
                shader["dl_color"].Set(sunColor.Vec3() * m.SunIntensity);
                shader["al_color"].Set(ambientColor * m.AmbientIntensity);
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

            GL.ActiveTexture(14);
            if (m.EnableShadows && Client.Instance.Settings.EnableSunShadows)
            {
                dlRenderer.DepthTexture.Bind();
            }
            else
            {
                Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.DepthFakeTexture.Bind();
            }

            GL.ActiveTexture(13);
            plr.DepthMap.Bind();
            plr.UniformLights(shader);
            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.Uniform(shader);

            GL.Enable(Capability.CullFace);
            GL.CullFace(PolygonFaceMode.Back);
            GL.Enable(Capability.DepthTest);
            GL.DepthFunction(ComparisonMode.Always);
            GL.DepthMask(true);

            this.DrawFullScreenQuad();

            GL.Enable(Capability.DepthTest);
            GL.DepthFunction(ComparisonMode.Less);
            GL.Disable(Capability.CullFace);

            // Bind default framebuffer and reset
            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GL.ActiveTexture(0);
        }

        public void FinishRender(Map m)
        {
            GL.BindFramebuffer(FramebufferTarget.All, 0);

            this.FinalPass.Bind();
            this.FinalPass["gamma"].Set(Client.Instance.Settings.Gamma);
            GL.ActiveTexture(0);
            this.ColorOutputTex.Bind();
            GL.ActiveTexture(1);
            this.DepthTex.Bind();
            GL.ActiveTexture(2);
            Client.Instance.Frontend.Renderer.ObjectRenderer.FastLightRenderer.TexColor.Bind();
            GL.ActiveTexture(3);
            if (m != null && m.Has2DShadows && m.Is2D)
            {
                Client.Instance.Frontend.Renderer.ObjectRenderer.Shadow2DRenderer.OutputTexture.Bind();
            }
            else
            {
                Client.Instance.Frontend.Renderer.ObjectRenderer.Shadow2DRenderer.WhiteSquare.Bind();
            }

            GL.Enable(Capability.CullFace);
            GL.CullFace(PolygonFaceMode.Back);
            GL.Enable(Capability.DepthTest);
            GL.DepthFunction(ComparisonMode.Always);
            GL.DepthMask(true);

            this.DrawFullScreenQuad();

            GL.DepthFunction(ComparisonMode.LessOrEqual);
            GL.Disable(Capability.CullFace);
            GL.ActiveTexture(0);
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

        public static uint UseFBO(uint? fbo)
        {
            if (!fbo.HasValue)
            {
                fbo = GL.GenFramebuffer();
            }

            GL.BindFramebuffer(FramebufferTarget.All, fbo.Value);
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
        }
        public void DrawFullScreenQuad()
        {
            this.FullScreenQuad.Bind();
            GL.DrawElements(PrimitiveType.Triangles, 6, ElementsType.Byte, IntPtr.Zero);
        }
        public void RecompileShaders(bool havePointShadows, bool haveSunShadows)
        {
            this.DeferredPrePass?.Program.Dispose();
            this.DeferredFinal?.Dispose();
            this.Forward?.Program.Dispose();
            this.FinalPass?.Dispose();

            this.DeferredPrePass = new FastAccessShader(this.CompileShader("deferred", haveSunShadows, havePointShadows));
            this.DeferredPrePass.Program.Bind();
            this.DeferredPrePass.Program.BindUniformBlock("FrameData", 1);
            this.DeferredPrePass.Program.BindUniformBlock("BoneData", 2);
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

            this.Forward = new FastAccessShader(this.CompileShader("object", haveSunShadows, havePointShadows));
            this.Forward.Program.Bind();
            this.Forward.Program.BindUniformBlock("FrameData", 1);
            this.Forward.Program.BindUniformBlock("BoneData", 2);
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
            this.FinalPass["g_shadows2d"].Set(3);
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

            this.FramebufferCompound = UseFBO(this.FramebufferCompound);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Depth, TextureTarget.Texture2D, this.DepthTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color0, TextureTarget.Texture2D, this.ColorOutputTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color1, TextureTarget.Texture2D, this.PositionTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color2, TextureTarget.Texture2D, this.NormalTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color3, TextureTarget.Texture2D, this.AlbedoTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color4, TextureTarget.Texture2D, this.MRAOGTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color5, TextureTarget.Texture2D, this.EmissionTex, 0);
            GL.DrawBuffers(new DrawBufferMode[] { DrawBufferMode.Color0, DrawBufferMode.Color1, DrawBufferMode.Color2, DrawBufferMode.Color3, DrawBufferMode.Color4, DrawBufferMode.Color5 });
            CheckFBO("universal compound");

            this.FramebufferFinal = UseFBO(this.FramebufferFinal);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color0, TextureTarget.Texture2D, this.ColorOutputTex, 0);
            GL.DrawBuffers(new DrawBufferMode[] { DrawBufferMode.Color0 });
            CheckFBO("deferred final");

            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
        }
    }
}
