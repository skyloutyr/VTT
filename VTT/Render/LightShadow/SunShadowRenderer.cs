namespace VTT.Render.LightShadow
{
    using System.Numerics;
    using System;
    using System.Diagnostics;
    using VTT.Asset;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;
    using VTT.GL.Bindings;

    public class SunShadowRenderer
    {
        public const int ShadowMapResolution = 2048;

        private uint _sunFbo;
        private Texture _sunDepthTexture;
        private Texture _fakeDepthTexture;

        public FastAccessShader SunShader { get; set; }

        public Matrix4x4 SunView { get; set; } = Matrix4x4.Identity;
        public Matrix4x4 SunProjection { get; set; } = Matrix4x4.Identity;

        public Texture DepthTexture => this._sunDepthTexture;
        public Texture DepthFakeTexture => this._fakeDepthTexture;

        public Stopwatch CPUTimer { get; set; }

        public void Create()
        {
            this._fakeDepthTexture = new Texture(TextureTarget.Texture2D);
            this._fakeDepthTexture.Bind();
            OpenGLUtil.NameObject(GLObjectType.Texture, this._fakeDepthTexture, "Directional shadows empty depth texture 24d");
            this._fakeDepthTexture.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this._fakeDepthTexture.SetWrapParameters(WrapParam.ClampToBorder, WrapParam.ClampToBorder, WrapParam.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureProperty.BorderColor, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            float[] fakeDepthData = new float[8 * 8];
            Array.Fill(fakeDepthData, 1);
            unsafe
            {
                fixed (float* p = fakeDepthData)
                {
                    GL.TexImage2D(TextureTarget.Texture2D, 0, SizedInternalFormat.DepthComponent24, 8, 8, PixelDataFormat.DepthComponent, PixelDataType.Float, (nint)p);
                }
            }

            this._sunDepthTexture = new Texture(TextureTarget.Texture2D);
            this._sunDepthTexture.Bind();
            OpenGLUtil.NameObject(GLObjectType.Texture, this._sunDepthTexture, "Directional shadows depth texture 24d");
            this._sunDepthTexture.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this._sunDepthTexture.SetWrapParameters(WrapParam.ClampToBorder, WrapParam.ClampToBorder, WrapParam.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureProperty.BorderColor, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)Version30.CompareRefToTexture);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int)Version10.Less);
            GL.TexImage2D(TextureTarget.Texture2D, 0, SizedInternalFormat.DepthComponent24, ShadowMapResolution, ShadowMapResolution, PixelDataFormat.DepthComponent, PixelDataType.Float, IntPtr.Zero);

            this._sunFbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.All, this._sunFbo);
            OpenGLUtil.NameObject(GLObjectType.Framebuffer, this._sunFbo, "Directional shadows fbo");
            GL.FramebufferTexture2D(FramebufferTarget.All, FramebufferAttachment.Depth, TextureTarget.Texture2D, this._sunDepthTexture, 0);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(DrawBufferMode.None);
            FramebufferStatus fec = GL.CheckFramebufferStatus(FramebufferTarget.All);
            if (fec != FramebufferStatus.Complete)
            {
                throw new Exception("Sun framebuffer not complete!");
            }

            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GL.DrawBuffer(DrawBufferMode.Back);

            this.SunShader = new FastAccessShader(OpenGLUtil.LoadShader("object_shadow", ShaderType.Vertex, ShaderType.Fragment));
            this.SunView = Matrix4x4.CreateLookAt(new Vector3(0, -0.1f, 49.5f), Vector3.Zero, new Vector3(0, 1, 0));
            this.SunProjection = Matrix4x4.CreateOrthographic(48, 48, 0.1f, 100f);

            this.CPUTimer = new Stopwatch();
        }

        public static bool ShadowPass { get; set; }

        public void Render(Map m, double time)
        {
            this.CPUTimer.Restart();
            OpenGLUtil.StartSection("Directional shadows");

            if (m.EnableShadows && Client.Instance.Settings.EnableSunShadows && m.SunEnabled)
            {
                Vector3 sunCenter = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Position;
                Vector3 sunDirection = Client.Instance.Frontend.Renderer.SkyRenderer.GetCurrentSunDirection();

                VectorCamera vCam = new VectorCamera(sunCenter - (sunDirection.Normalized() * 49.5f), sunDirection.Normalized());
                vCam.Projection = Matrix4x4.CreateOrthographic(48, 48, 0.1f, 100f);
                vCam.RecalculateData(assumedUpVector: Vector3.UnitZ);

                this.SunView = vCam.View;
                this.SunProjection = vCam.Projection;

                GL.BindFramebuffer(FramebufferTarget.All, this._sunFbo);
                GL.Viewport(0, 0, ShadowMapResolution, ShadowMapResolution);
                GL.Clear(ClearBufferMask.Depth);

                this.SunShader.Program.Bind();
                this.SunShader.Essentials.View.Set(this.SunView);
                this.SunShader.Essentials.Projection.Set(this.SunProjection);

                ShadowPass = true;
                GL.Disable(Capability.CullFace);

                for (int i = -2; i <= 0; ++i)
                {
                    foreach (MapObject mo in m.IterateObjects(i))
                    {
                        if (!mo.CastsShadow || mo.DoNotRender)
                        {
                            continue;
                        }

                        AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(mo.AssetID, AssetType.Model, out Asset a);
                        if (status == AssetStatus.Return && a.ModelGlReady)
                        {
                            Matrix4x4 modelMatrix = mo.ClientCachedModelMatrix;
                            a.Model.GLMdl.Render(this.SunShader, modelMatrix, this.SunProjection, this.SunView, 0, mo.AnimationContainer.CurrentAnimation, mo.AnimationContainer.GetTime(time), mo.AnimationContainer);
                        }
                    }
                }

                ShadowPass = false;
                GL.Enable(Capability.CullFace);
                GL.BindFramebuffer(FramebufferTarget.All, 0);
                GL.DrawBuffer(DrawBufferMode.Back);
                GL.Viewport(0, 0, Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
            }

            OpenGLUtil.EndSection();
            this.CPUTimer.Stop();
        }
    }
}
