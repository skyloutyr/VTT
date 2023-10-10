namespace VTT.Render
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using System.Diagnostics;
    using VTT.Asset.Obj;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public class FastLightRenderer
    {
        public ShaderProgram Shader { get; set; }
        public WavefrontObject Sphere { get; set; }

        public int? Ping { get; set; }
        public Texture TexColor { get; set; }
        public Stopwatch CPUTimer { get; } = new Stopwatch();

        public void Create()
        {
            this.Shader = OpenGLUtil.LoadShader("fast_light", ShaderType.VertexShader, ShaderType.FragmentShader);
            this.Shader.Bind();
            this.Shader["g_positions"].Set(0);
            this.Shader["g_normals"].Set(1);
            this.Shader["g_albedo"].Set(2);
            this.Shader["g_aomrg"].Set(3);
            this.Shader["g_emission"].Set(4);
            this.Shader["g_depth"].Set(5);
            this.Shader["g_color_last"].Set(6);
            this.Sphere = OpenGLUtil.LoadModel("sphere_mediumres", VertexFormat.Pos);
        }

        public void Resize(int w, int h)
        {
            UniversalPipeline pipeline = Client.Instance.Frontend.Renderer.Pipeline;
            this.TexColor?.Dispose();

            this.TexColor = new Texture(TextureTarget.Texture2D);
            UniversalPipeline.ResizeTex(this.TexColor, w, h, PixelInternalFormat.Rgb32f, PixelFormat.Rgb, PixelType.Float);

            this.Ping = UniversalPipeline.UseFBO(this.Ping);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, pipeline.DepthTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, this.TexColor, 0);
            GL.DrawBuffers(1, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0 });
            UniversalPipeline.CheckFBO("fast light");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
        }

        public void Render(Map m)
        {
            this.CPUTimer.Restart();
            UniversalPipeline pipeline = Client.Instance.Frontend.Renderer.Pipeline;
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;

            this.Shader.Bind();
            this.Shader["view"].Set(cam.View);
            this.Shader["projection"].Set(cam.Projection);
            this.Shader["viewport_size"].Set(new Vector2(Client.Instance.Frontend.Width, Client.Instance.Frontend.Height));
            this.Shader["camera_position"].Set(cam.Position);

            GL.ActiveTexture(TextureUnit.Texture0);
            pipeline.PositionTex.Bind();
            GL.ActiveTexture(TextureUnit.Texture1);
            pipeline.NormalTex.Bind();
            GL.ActiveTexture(TextureUnit.Texture2);
            pipeline.AlbedoTex.Bind();
            GL.ActiveTexture(TextureUnit.Texture3);
            pipeline.MRAOGTex.Bind();
            GL.ActiveTexture(TextureUnit.Texture4);
            pipeline.EmissionTex.Bind();
            GL.ActiveTexture(TextureUnit.Texture5);
            pipeline.DepthTex.Bind();
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, this.Ping.Value);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            foreach (MapObject mo in m.IterateObjects(null))
            {
                if (mo.MapLayer <= 0 || Client.Instance.IsAdmin)
                {
                    lock (mo.FastLightsLock)
                    {
                        for (int i = mo.FastLights.Count - 1; i >= 0; i--)
                        {
                            FastLight fl = mo.FastLights[i];
                            if (fl.Enabled)
                            {
                                Vector3 baseOffset;
                                Vector4 bo4 = new Vector4(fl.Offset.Xyz, 1.0f);
                                Quaternion q = fl.UseObjectTransform ? mo.Rotation : Quaternion.Identity;
                                bo4 = q * bo4;
                                baseOffset = bo4.Xyz / bo4.W;
                                baseOffset *= mo.Scale;
                                baseOffset += mo.Position;
                                if (cam.IsSphereInFrustrum(baseOffset, fl.Color.W))
                                {
                                    GL.CullFace((baseOffset - cam.Position).Length < fl.Radius ? CullFaceMode.Front : CullFaceMode.Back);
                                    this.Shader["model"].Set(new Vector4(baseOffset, fl.Color.W));
                                    this.Shader["light_color"].Set(fl.Color * new Vector4(fl.Intensity, fl.Intensity, fl.Intensity, 1.0f));
                                    this.Sphere.Render();
                                }
                            }
                        }
                    }
                }
            }

            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.CullFace);
            GL.DepthMask(true);
            this.CPUTimer.Stop();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
        }
    }
}