namespace VTT.Render
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using System;
    using VTT.Asset;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Render.LightShadow;
    using VTT.Util;

    public class DeferredPipeline
    {
        public int? FBO { get; set; }

        public Texture PositionTex { get; set; }
        public Texture AlbedoTex { get; set; }
        public Texture EmissionTex { get; set; }
        public Texture NormalTex { get; set; }
        public Texture MRAOGTex { get; set; }
        public Texture DepthTex { get; set; }

        public ShaderProgram DeferredPass { get; set; }
        public ShaderProgram FinalPass { get; set; }

        private VertexArray _vao;
        private GPUBuffer _vbo;

        public void Create()
        {
            this.RecompileShaders(Client.Instance.Settings.EnableSunShadows, Client.Instance.Settings.EnableDirectionalShadows, Client.Instance.Settings.DisableShaderBranching);

            this._vao = new VertexArray();
            this._vbo = new GPUBuffer(BufferTarget.ArrayBuffer);
            this._vao.Bind();
            this._vbo.Bind();
            this._vbo.SetData(new float[] { 
                -1.0f, 1.0f, 0f, 1f,
                1.0f, -1.0f, 1f, 0f,
                1.0f, 1.0f, 1f, 1f,
                -1.0f, 1.0f, 0f, 1f,
                -1.0f, -1.0f, 0f, 0f,
                1.0f, -1.0f, 1f, 0f
            });

            this._vao.SetVertexSize<float>(4);
            this._vao.PushElement(ElementType.Vec2);
            this._vao.PushElement(ElementType.Vec2);
        }

        public void Dispose()
        {
            this.PositionTex.Dispose();
            this.AlbedoTex.Dispose();
            this.EmissionTex.Dispose();
            this.NormalTex.Dispose();
            this.MRAOGTex.Dispose();
            this.DepthTex.Dispose();

            if (this.FBO.HasValue)
            {
                GL.DeleteRenderbuffer(this.FBO.Value);
            }

            this._vbo.Dispose();
            this._vao.Dispose();
            this.PositionTex = this.AlbedoTex = this.NormalTex = this.MRAOGTex = this.DepthTex = default;
            this.FBO = null;
            this._vbo = default;
            this._vao = null;
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

            this.PositionTex = new Texture(TextureTarget.Texture2D);
            this.AlbedoTex = new Texture(TextureTarget.Texture2D);
            this.EmissionTex = new Texture(TextureTarget.Texture2D);
            this.NormalTex = new Texture(TextureTarget.Texture2D);
            this.MRAOGTex = new Texture(TextureTarget.Texture2D);
            this.DepthTex = new Texture(TextureTarget.Texture2D);
            if (!this.FBO.HasValue)
            {
                this.FBO = GL.GenFramebuffer();
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, this.FBO.Value);

            this.DepthTex.Bind();
            this.DepthTex.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this.DepthTex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Depth24Stencil8, w, h, 0, PixelFormat.DepthStencil, PixelType.Float32UnsignedInt248Rev, IntPtr.Zero);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, TextureTarget.Texture2D, this.DepthTex, 0);

            this.PositionTex.Bind();
            this.PositionTex.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this.PositionTex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, Client.Instance.Settings.UseHalfPrecision ? PixelInternalFormat.Rgba16f : PixelInternalFormat.Rgba32f, w, h, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, this.PositionTex, 0);

            this.NormalTex.Bind();
            this.NormalTex.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this.NormalTex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, Client.Instance.Settings.UseHalfPrecision ? PixelInternalFormat.Rgba16f : PixelInternalFormat.Rgba32f, w, h, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, this.NormalTex, 0);

            this.AlbedoTex.Bind();
            this.AlbedoTex.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this.AlbedoTex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2D, this.AlbedoTex, 0);

            this.MRAOGTex.Bind();
            this.MRAOGTex.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this.MRAOGTex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment3, TextureTarget.Texture2D, this.MRAOGTex, 0);

            this.EmissionTex.Bind();
            this.EmissionTex.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this.EmissionTex.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment4, TextureTarget.Texture2D, this.EmissionTex, 0);

            GL.DrawBuffers(5, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1, DrawBuffersEnum.ColorAttachment2, DrawBuffersEnum.ColorAttachment3, DrawBuffersEnum.ColorAttachment4 });
            FramebufferErrorCode fec = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);

            if (fec != FramebufferErrorCode.FramebufferComplete)
            {
                Client.Instance.Logger.Log(LogLevel.Fatal, "Could not complete deferred framebuffer!");
                Client.Instance.Logger.Log(LogLevel.Fatal, "  " + fec);
                throw new Exception("Framebuffer could not complete");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
        }

        public void RenderScene(Map m)
        {
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            ShaderProgram shader = this.DeferredPass;
            Vector3 sunDir = Client.Instance.Frontend.Renderer.SkyRenderer.GetCurrentSunDirection();
            Color sunColor = Client.Instance.Frontend.Renderer.SkyRenderer.GetSunColor();
            Vector3 ambientColor = Client.Instance.Frontend.Renderer.SkyRenderer.GetAmbientColor().Vec3();
            Color skyColor = Client.Instance.Frontend.Renderer.SkyRenderer.GetSkyColor();
            SunShadowRenderer dlRenderer = Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer;
            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;
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

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, this.FBO.Value);
            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.DepthMask(true);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
            {
                GL.Disable(EnableCap.Multisample);
            }

            for (int i = -2; i <= 0; ++i)
            {
                foreach (MapObject mo in m.IterateObjects(i))
                {
                    AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(mo.AssetID, AssetType.Model, out Asset a);
                    if (status == AssetStatus.Return && (a?.Model?.GLMdl?.glReady ?? false))
                    {
                        if (!mo.ClientAssignedModelBounds)
                        {
                            mo.ClientBoundingBox = a.Model.GLMdl.CombinedBounds;
                            mo.ClientAssignedModelBounds = true;
                        }

                        if (!Client.Instance.Frontend.Renderer.MapRenderer.IsAABoxInFrustrum(mo.CameraCullerBox, mo.Position))
                        {
                            mo.ClientRenderedThisFrame = false;
                            continue;
                        }

                        if (a.Model.GLMdl.HasTransparency || mo.TintColor.Alpha() < (1.0f - float.Epsilon) || !mo.ShaderID.IsEmpty())
                        {
                            mo.ClientDeferredRejectThisFrame = true;
                            continue;
                        }

                        mo.ClientRenderedThisFrame = true;
                        mo.ClientDeferredRejectThisFrame = false;
                        Matrix4 modelMatrix = mo.ClientCachedModelMatrix;
                        float ga = m.GridColor.Vec4().W;
                        shader["grid_alpha"].Set(i == -2 && m.GridEnabled ? ga : 0.0f);
                        shader["tint_color"].Set(mo.TintColor.Vec4());
                        GL.ActiveTexture(TextureUnit.Texture0);
                        a.Model.GLMdl.Render(shader, modelMatrix, cam.Projection, cam.View, double.NaN);

                    }
                }
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
            shader = this.FinalPass;
            shader.Bind();
            shader["gamma_factor"].Set(Client.Instance.Settings.Gamma);
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
                Client.Instance.Frontend.Renderer.White.Bind();
            }

            GL.ActiveTexture(TextureUnit.Texture13);
            plr.DepthMap.Bind();
            plr.UniformLights(shader);
            Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.Uniform(shader);

            this._vao.Bind();
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.DepthTest);
            GL.DepthMask(false);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, this.FBO.Value);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            GL.BlitFramebuffer(0, 0, Client.Instance.Frontend.Width, Client.Instance.Frontend.Height, 0, 0, Client.Instance.Frontend.Width, Client.Instance.Frontend.Height, ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit, BlitFramebufferFilter.Nearest);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        public void RecompileShaders(bool dirShadows, bool pointShadows, bool noBranches)
        {
            this.DeferredPass?.Dispose();
            this.FinalPass?.Dispose();

            this.DeferredPass = this.RecompileShader(dirShadows, pointShadows, noBranches, "deferred");
            this.DeferredPass.Bind();
            this.DeferredPass.BindUniformBlock("FrameData", 1);
            this.DeferredPass["m_texture_diffuse"].Set(0);
            this.DeferredPass["m_texture_normal"].Set(1);
            this.DeferredPass["m_texture_emissive"].Set(2);
            this.DeferredPass["m_texture_aomr"].Set(3);

            this.FinalPass = this.RecompileShader(dirShadows, pointShadows, noBranches, "deferred_final");
            this.FinalPass.Bind();
            this.FinalPass.BindUniformBlock("FrameData", 1);
            this.FinalPass["g_positions"].Set(12);
            this.FinalPass["g_normals"].Set(11);
            this.FinalPass["g_albedo"].Set(10);
            this.FinalPass["g_aomrg"].Set(9);
            this.FinalPass["g_emission"].Set(8);
            this.FinalPass["dl_shadow_map"].Set(14);
            this.FinalPass["pl_shadow_maps"].Set(13);

        }

        private ShaderProgram RecompileShader(bool dirShadows, bool pointShadows, bool noBranches, string sName)
        {
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

            if (noBranches)
            {
                RemoveDefine(ref lineVert, "BRANCHING");
                RemoveDefine(ref lineFrag, "BRANCHING");
            }

            lineFrag = lineFrag.Replace("#define PCF_ITERATIONS 2", $"#define PCF_ITERATIONS {Client.Instance.Settings.ShadowsPCF}");

            if (!ShaderProgram.TryCompile(out ShaderProgram sp, lineVert, null, lineFrag, out string err))
            {
                Logger l = Client.Instance.Logger;
                l.Log(LogLevel.Fatal, "Could not compile shader! Shader error was " + err);
                throw new Exception("Could not compile object shader! Shader error was " + err);
            }

            return sp;
        }

        private void RemoveDefine(ref string lines, string define)
        {
            string r = "#define " + define;
            int idx = lines.IndexOf(r);
            if (idx != -1)
            {
                lines = lines.Remove(idx, lines.IndexOf('\n', idx) - idx - 1);
            }
        }
    }
}
