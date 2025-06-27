namespace VTT.Render
{
    using System.Diagnostics;
    using System.Numerics;
    using VTT.Asset.Obj;
    using VTT.Control;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Render.Shaders;
    using VTT.Util;

    public class FastLightRenderer
    {
        public FastAccessShader<FastLightUniforms> Shader { get; set; }
        public WavefrontObject Sphere { get; set; }

        public uint? Ping { get; set; }
        public Texture TexColor { get; set; }
        public Stopwatch CPUTimer { get; } = new Stopwatch();

        public void Create()
        {
            this.Shader = new FastAccessShader<FastLightUniforms>(OpenGLUtil.LoadShader("fast_light", ShaderType.Vertex, ShaderType.Fragment));
            this.Shader.Bind();
            this.Shader.Uniforms.PositionsSampler.Set(0);
            this.Shader.Uniforms.NormalsSampler.Set(1);
            this.Shader.Uniforms.AlbedoSampler.Set(2);
            this.Shader.Uniforms.AOMRGSampler.Set(3);
            this.Shader.Uniforms.EmissionSampler.Set(4);
            this.Shader.Uniforms.DepthSampler.Set(5);
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
            this.Shader.Uniforms.View.Set(cam.View);
            this.Shader.Uniforms.Projection.Set(cam.Projection);
            this.Shader.Uniforms.ViewportSize.Set(new Vector2(Client.Instance.Frontend.Width, Client.Instance.Frontend.Height));
            this.Shader.Uniforms.CameraPosition.Set(cam.Position);

            GLState.ActiveTexture.Set(0);
            pipeline.PositionTex.Bind();
            GLState.ActiveTexture.Set(1);
            pipeline.NormalTex.Bind();
            GLState.ActiveTexture.Set(2);
            pipeline.AlbedoTex.Bind();
            GLState.ActiveTexture.Set(3);
            pipeline.MRAOGTex.Bind();
            GLState.ActiveTexture.Set(4);
            pipeline.EmissionTex.Bind();
            GLState.ActiveTexture.Set(5);
            pipeline.DepthTex.Bind();
            GLState.DepthTest.Set(false);
            GLState.CullFace.Set(false);
            GL.BindFramebuffer(FramebufferTarget.All, this.Ping.Value);
            GLState.Clear(ClearBufferMask.Color);
            GLState.Blend.Set(true);
            GLState.BlendFunc.Set((BlendingFactor.One, BlendingFactor.One));
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
                                    GLState.CullFaceMode.Set((baseOffset - cam.Position).Length() < fl.Radius ? PolygonFaceMode.Front : PolygonFaceMode.Back);
                                    this.Shader.Uniforms.Model.Set(new Vector4(baseOffset, fl.Color.W));
                                    this.Shader.Uniforms.Color.Set(fl.Color * new Vector4(fl.Intensity, fl.Intensity, fl.Intensity, 1.0f));
                                    this.Sphere.Render();
                                }
                            }
                        }
                    }
                }
            }

            GLState.BlendFunc.Set((BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha));
            GLState.Blend.Set(false);
            GLState.DepthTest.Set(false);
            GLState.CullFaceMode.Set(PolygonFaceMode.Back);
            GLState.CullFace.Set(true);
            GLState.DepthMask.Set(true);
            this.CPUTimer.Stop();

            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
        }
    }
}