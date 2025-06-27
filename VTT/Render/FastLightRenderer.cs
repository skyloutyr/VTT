namespace VTT.Render
{
    using System.Diagnostics;
    using System.Numerics;
    using VTT.Asset.Obj;
    using VTT.Control;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Util;

    public class FastLightRenderer
    {
        public ShaderProgram Shader { get; set; }
        public WavefrontObject Sphere { get; set; }

        public uint? Ping { get; set; }
        public Texture TexColor { get; set; }
        public Stopwatch CPUTimer { get; } = new Stopwatch();

        public void Create()
        {
            this.Shader = OpenGLUtil.LoadShader("fast_light", ShaderType.Vertex, ShaderType.Fragment);
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
            UniversalPipeline.ResizeTex(this.TexColor, w, h, SizedInternalFormat.RgbFloat, PixelDataFormat.Rgb, PixelDataType.Float);
            OpenGLUtil.NameObject(GLObjectType.Texture, this.TexColor, "Fast light color");

            this.Ping = UniversalPipeline.UseFBO(this.Ping, "Fast light fbo");
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Depth, TextureTarget.Texture2D, pipeline.DepthTex, 0);
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Color0, TextureTarget.Texture2D, this.TexColor, 0);
            GL.DrawBuffers(new DrawBufferMode[] { DrawBufferMode.Color0 });
            UniversalPipeline.CheckFBO("Fast light");

            GL.BindFramebuffer(FramebufferTarget.All, 0);
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

            GL.ActiveTexture(0);
            pipeline.PositionTex.Bind();
            GL.ActiveTexture(1);
            pipeline.NormalTex.Bind();
            GL.ActiveTexture(2);
            pipeline.AlbedoTex.Bind();
            GL.ActiveTexture(3);
            pipeline.MRAOGTex.Bind();
            GL.ActiveTexture(4);
            pipeline.EmissionTex.Bind();
            GL.ActiveTexture(5);
            pipeline.DepthTex.Bind();
            GL.Disable(Capability.DepthTest);
            GL.Disable(Capability.CullFace);
            GL.BindFramebuffer(FramebufferTarget.All, this.Ping.Value);
            GL.Clear(ClearBufferMask.Color);
            GL.Enable(Capability.Blend);
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
                                Vector4 bo4 = new Vector4(fl.Offset.Xyz(), 1.0f);
                                Quaternion q = fl.UseObjectTransform ? mo.Rotation : Quaternion.Identity;
                                bo4 = Vector4.Transform(bo4, q);
                                baseOffset = bo4.Xyz() / bo4.W;
                                baseOffset *= mo.Scale;
                                baseOffset += mo.Position;
                                if (cam.IsSphereInFrustrum(baseOffset, fl.Color.W))
                                {
                                    GL.CullFace((baseOffset - cam.Position).Length() < fl.Radius ? PolygonFaceMode.Front : PolygonFaceMode.Back);
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
            GL.Disable(Capability.Blend);
            GL.Enable(Capability.DepthTest);
            GL.CullFace(PolygonFaceMode.Back);
            GL.Enable(Capability.CullFace);
            GL.DepthMask(true);
            this.CPUTimer.Stop();

            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
        }
    }
}