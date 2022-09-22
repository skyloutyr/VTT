namespace VTT.Render.LightShadow
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using VTT.Asset;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public class SunShadowRenderer
    {
        public const int ShadowMapResolution = 2048;

        private int _sunFbo;
        private Texture _sunDepthTexture;

        public ShaderProgram SunShader { get; set; }

        public Matrix4 SunView { get; set; } = Matrix4.Identity;
        public Matrix4 SunProjection { get; set; } = Matrix4.Identity;

        public Texture DepthTexture => this._sunDepthTexture;

        public void Create()
        {
            this._sunDepthTexture = new Texture(TextureTarget.Texture2D);
            this._sunDepthTexture.Bind();
            this._sunDepthTexture.SetFilterParameters(FilterParam.Linear, FilterParam.Linear);
            this._sunDepthTexture.SetWrapParameters(WrapParam.ClampToBorder, WrapParam.ClampToBorder, WrapParam.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)Version30.CompareRefToTexture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int)Version10.Less);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, ShadowMapResolution, ShadowMapResolution, 0, PixelFormat.DepthComponent, PixelType.Float, System.IntPtr.Zero);

            this._sunFbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, this._sunFbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, this._sunDepthTexture, 0);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            this.SunShader = OpenGLUtil.LoadShader("object_shadow", ShaderType.VertexShader, ShaderType.FragmentShader);
        }

        public static bool ShadowPass { get; set; }

        public void Render(Map m, double time)
        {
            if (m.EnableShadows && Client.Instance.Settings.EnableSunShadows && m.SunEnabled)
            {
                Vector3 sunCenter = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Position;
                Vector3 sunDirection = Client.Instance.Frontend.Renderer.SkyRenderer.GetCurrentSunDirection();

                VectorCamera vCam = new VectorCamera(sunCenter - (sunDirection.Normalized() * 49.5f), sunDirection.Normalized());
                vCam.Projection = Matrix4.CreateOrthographic(48, 48, 0.1f, 100f);
                vCam.RecalculateData(assumedUpVector: Vector3.UnitZ);

                this.SunView = vCam.View;
                this.SunProjection = vCam.Projection;

                GL.Viewport(0, 0, ShadowMapResolution, ShadowMapResolution);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, this._sunFbo);
                GL.Clear(ClearBufferMask.DepthBufferBit);

                this.SunShader.Bind();
                this.SunShader["view"].Set(this.SunView);
                this.SunShader["projection"].Set(this.SunProjection);

                ShadowPass = true;

                for (int i = -2; i <= 0; ++i)
                {
                    foreach (MapObject mo in m.IterateObjects(i))
                    {
                        AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(mo.AssetID, AssetType.Model, out Asset a);
                        if (status == AssetStatus.Return && a != null && a.Model != null && a.Model.GLMdl != null)
                        {
                            Matrix4 modelMatrix = mo.ClientCachedModelMatrix;
                            a.Model.GLMdl.Render(this.SunShader, this.SunProjection, this.SunView, modelMatrix, 0);
                        }
                    }
                }

                ShadowPass = false;
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                GL.Viewport(0, 0, Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
            }
        }
    }
}
