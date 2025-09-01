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
    using VTT.Render.Shaders;
    using System.Runtime.Intrinsics;

    public class SunShadowRenderer
    {
        public const int NumShadowCascades = 4;

        private uint? _sunFbo;
        private Texture _sunDepthTexture;
        private Texture _fakeDepthTexture;
        private Texture _fakeDepthFullyShadedTexture;

        public FastAccessShader<SunShadowUniforms> SunShader { get; set; }
        public SunCascades Cascades { get; set; }

        public Stopwatch CPUTimer { get; set; }
        public int ShadowMapResolution { get; set; }

        public void Create()
        {
            this._fakeDepthTexture = new Texture(TextureTarget.Texture2DArray);
            this._fakeDepthTexture.Bind();
            OpenGLUtil.NameObject(GLObjectType.Texture, this._fakeDepthTexture, $"Directional shadows empty depth texture array {NumShadowCascades + 1}x32d");
            this._fakeDepthTexture.SetFilterParameters(FilterParam.Linear, FilterParam.Linear);
            this._fakeDepthTexture.SetWrapParameters(WrapParam.ClampToBorder, WrapParam.ClampToBorder, WrapParam.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureProperty.CompareMode, TextureCompareMode.CompareRefToTexture);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureProperty.CompareFunc, ComparisonMode.LessOrEqual);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureProperty.BorderColor, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            float[] fakeDepthData = new float[8 * 8 * (NumShadowCascades + 1)];
            Array.Fill(fakeDepthData, 1);
            unsafe
            {
                fixed (float* p = fakeDepthData)
                {
                    GL.TexImage3D(TextureTarget.Texture2DArray, 0, SizedInternalFormat.DepthComponent32Float, 8, 8, NumShadowCascades + 1, PixelDataFormat.DepthComponent, PixelDataType.Float, (nint)p);
                }
            }

            this._fakeDepthFullyShadedTexture = new Texture(TextureTarget.Texture2DArray);
            this._fakeDepthFullyShadedTexture.Bind();
            OpenGLUtil.NameObject(GLObjectType.Texture, this._fakeDepthFullyShadedTexture, $"Directional shadows full depth texture array {NumShadowCascades + 1}x32d");
            this._fakeDepthFullyShadedTexture.SetFilterParameters(FilterParam.Linear, FilterParam.Linear);
            this._fakeDepthFullyShadedTexture.SetWrapParameters(WrapParam.ClampToBorder, WrapParam.ClampToBorder, WrapParam.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureProperty.CompareMode, TextureCompareMode.CompareRefToTexture);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureProperty.CompareFunc, ComparisonMode.LessOrEqual);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureProperty.BorderColor, new float[] { 0.0f, 0.0f, 0.0f, 0.0f });
            Array.Fill(fakeDepthData, 0);
            unsafe
            {
                fixed (float* p = fakeDepthData)
                {
                    GL.TexImage3D(TextureTarget.Texture2DArray, 0, SizedInternalFormat.DepthComponent32Float, 8, 8, NumShadowCascades + 1, PixelDataFormat.DepthComponent, PixelDataType.Float, (nint)p);
                }
            }

            int smRes = Client.Instance.Settings.DirectionalShadowsQuality switch
            {
                ClientSettings.GraphicsSetting.Low => 576,
                ClientSettings.GraphicsSetting.Medium => 1152,
                ClientSettings.GraphicsSetting.High => 2304,
                ClientSettings.GraphicsSetting.Ultra => 4608,
                _ => 1152
            };

            this.SetCascadeResolution(smRes);
            bool layeredVSh = OpenGLUtil.IsExtensionAvailable("GL_AMD_vertex_shader_layer");
            //bool layeredVSh = false;

            this.SunShader = new FastAccessShader<SunShadowUniforms>(OpenGLUtil.LoadShader(
                layeredVSh ? "object_shadow_ext" : "object_shadow", 
                layeredVSh 
                    ? stackalloc ShaderType[2] { ShaderType.Vertex, ShaderType.Fragment } 
                    : stackalloc ShaderType[3] { ShaderType.Vertex, ShaderType.Geometry, ShaderType.Fragment }, 
                new DefineRule[] { new DefineRule(DefineRule.Mode.ReplaceOrDefine, "NUM_CASCADES 5", $"NUM_CASCADES {NumShadowCascades + 1}") }));

            this.SunShader.Bind();
            this.SunShader.Program.BindUniformBlock("BoneData", 2);

            this.Cascades = new SunCascades();

            this.CPUTimer = new Stopwatch();
        }

        public void SetCascadeResolution(int res)
        {
            this.ShadowMapResolution = res;
            this._sunDepthTexture?.Dispose();
            this._sunDepthTexture = new Texture(TextureTarget.Texture2DArray);
            this._sunDepthTexture.Bind();
            OpenGLUtil.NameObject(GLObjectType.Texture, this._sunDepthTexture, $"Directional shadows depth texture array {NumShadowCascades + 1}x32d");
            this._sunDepthTexture.SetFilterParameters(FilterParam.Linear, FilterParam.Linear);
            this._sunDepthTexture.SetWrapParameters(WrapParam.ClampToBorder, WrapParam.ClampToBorder, WrapParam.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureProperty.BorderColor, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            GL.TexParameter(TextureTarget.Texture2DArray, TextureProperty.CompareMode, TextureCompareMode.CompareRefToTexture);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureProperty.CompareFunc, ComparisonMode.LessOrEqual);
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, SizedInternalFormat.DepthComponent32Float, res, res, NumShadowCascades + 1, PixelDataFormat.DepthComponent, PixelDataType.Float, IntPtr.Zero);
            Client.Instance.Logger.Log(LogLevel.Info, $"Allocated {((nint)(res * res * 4 * (NumShadowCascades + 1))).AsHumanReadableByteLength()} for shadow cascades.");
            if (this._sunFbo.HasValue)
            {
                GL.DeleteFramebuffer(this._sunFbo.Value);
            }

            this._sunFbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.All, this._sunFbo.Value);
            OpenGLUtil.NameObject(GLObjectType.Framebuffer, this._sunFbo.Value, "Directional shadows fbo");
            GL.FramebufferTexture(FramebufferTarget.All, FramebufferAttachment.Depth, this._sunDepthTexture, 0);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(DrawBufferMode.None);
            FramebufferStatus fec = GL.CheckFramebufferStatus(FramebufferTarget.All);
            if (fec != FramebufferStatus.Complete)
            {
                throw new Exception("Sun framebuffer not complete!");
            }

            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
        }

        public static bool ShadowPass { get; set; }

        public void BindDepthTexture(bool isFake = false, bool fullyShaded = false)
        {
            if (!Client.Instance.Settings.EnableDirectionalShadows)
            {
                this._fakeDepthTexture.Bind();
            }
            else
            {
                if (isFake)
                {
                    if (fullyShaded)
                    {
                        this._fakeDepthFullyShadedTexture.Bind();
                    }
                    else
                    {
                        this._fakeDepthTexture.Bind();
                    }
                }
                else
                {
                    this._sunDepthTexture.Bind();
                }
            }
        }

        public void Render(Map m, double time)
        {
            this.CPUTimer.Restart();
            OpenGLUtil.StartSection("Directional shadows");

            if (m.EnableShadows && Client.Instance.Settings.EnableSunShadows && m.SunEnabled)
            {
                this.Cascades.RecalculateCascades(Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera, Client.Instance.Frontend.Renderer.SkyRenderer.GetCurrentSunDirection(), this.ShadowMapResolution);

                GL.BindFramebuffer(FramebufferTarget.All, this._sunFbo.Value);
                GL.Viewport(0, 0, ShadowMapResolution, ShadowMapResolution);
                GLState.Clear(ClearBufferMask.Depth);

                GLState.DepthTest.Set(true);
                GLState.DepthFunc.Set(ComparisonMode.Less);
                GLState.CullFace.Set(true);
                GLState.CullFaceMode.Set(PolygonFaceMode.Back); // Going against conventions here, front face shadows eliminate most issues with spectral leakage at the cost of peter panning (which is acceptable with good bias tuning)
                GL.ColorMask(false, false, false, false);

                this.SunShader.Bind();
                this.SunShader.Uniforms.LightMatrices.Set(this.Cascades.CascadeArray.AsSpan(), 0);

                ShadowPass = true;

                Span<int> localLightLayers = stackalloc int[NumShadowCascades + 1];

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
                            int numInstances = 0;
                            for (int j = 0; j < NumShadowCascades + 1; ++j)
                            {
                                if (this.Cascades.CascadeFrustums[j].IsSphereInFrustumCached(ref mo.SunShadowCullingSpheres[j]))
                                {
                                    localLightLayers[numInstances++] = j;
                                }
                            }

                            if (numInstances > 0)
                            {
                                Matrix4x4 modelMatrix = mo.ClientCachedModelMatrix;
                                this.SunShader.Uniforms.LayerIndices.Set(localLightLayers, 0);
                                a.Model.GLMdl.Render(in this.SunShader.Uniforms.glbEssentials, modelMatrix, Matrix4x4.Identity, Matrix4x4.Identity, 0, mo.AnimationContainer.CurrentAnimation, mo.AnimationContainer.GetTime(time), mo.AnimationContainer, x => GLState.DrawElementsInstanced(PrimitiveType.Triangles, x.AmountToRender, ElementsType.UnsignedInt, 0, numInstances));
                            }
                        }
                    }
                }

                ShadowPass = false;
                GL.ColorMask(true, true, true, true);
                GL.BindFramebuffer(FramebufferTarget.All, 0);
                GL.DrawBuffer(DrawBufferMode.Back);
                GL.Viewport(0, 0, Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
            }

            OpenGLUtil.EndSection();
            this.CPUTimer.Stop();
        }

        public class SunCascades
        {
            public UnsafeArray<Matrix4x4> CascadeArray { get; private set; } = new UnsafeArray<Matrix4x4>(NumShadowCascades + 1);
            public UnsafeArray<Frustum> CascadeFrustums { get; private set; } = new UnsafeArray<Frustum>(NumShadowCascades + 1);

            public UnsafeArray<float> ShadowCascadeLevels { get; private set; }
            public Vector4 ShadowCascadeLevelsAsVec4 { get; private set; }

            public SunCascades()
            {
                this.ShadowCascadeLevels = new UnsafeArray<float>(NumShadowCascades);
                float cDiv = 2.0f;
                int ndIter = 0;
                for (int i = NumShadowCascades - 1; i >= 0; --i)
                {
                    this.ShadowCascadeLevels[i] = 1.0f / cDiv;
                    cDiv = ndIter == 0 ? 10f : ndIter == 1 ? 25f : cDiv * 2.0f;
                    ndIter += 1;
                }

                this.ShadowCascadeLevelsAsVec4 = new Vector4(
                    this.ShadowCascadeLevels[0],
                    this.ShadowCascadeLevels.Length == 1 ? 0 : this.ShadowCascadeLevels[1],
                    this.ShadowCascadeLevels.Length <= 2 ? 0 : this.ShadowCascadeLevels[2],
                    this.ShadowCascadeLevels.Length <= 3 ? 0 : this.ShadowCascadeLevels[3]
                );
            }

            public void RecalculateCascades(Camera cam, Vector3 sunDirection, int smRes)
            {
                if (this.CascadeArray.Length != NumShadowCascades + 1)
                {
                    this.ReallocateCascadeNum();
                }

                float cNear = 0.01f;
                float cFar = 100f;
                for (int i = 0; i < NumShadowCascades + 1; ++i)
                {
                    this.CascadeArray[i] = i == 0
                        ? CalculateLightSpaceMatrix(cam, sunDirection, cNear, cFar * ShadowCascadeLevels[i], smRes)
                        : i < NumShadowCascades
                            ? CalculateLightSpaceMatrix(cam, sunDirection, cFar * ShadowCascadeLevels[i - 1], cFar * ShadowCascadeLevels[i], smRes)
                            : CalculateLightSpaceMatrix(cam, sunDirection, cFar * ShadowCascadeLevels[i - 1], cFar, smRes);

                    this.CascadeFrustums[i] = new Frustum(this.CascadeArray[i]);
                }
            }

            private void ReallocateCascadeNum()
            {
                this.CascadeArray.Free();
                this.CascadeArray = new UnsafeArray<Matrix4x4>(NumShadowCascades + 1);
                this.CascadeFrustums.Free();
                this.CascadeFrustums = new UnsafeArray<Frustum>(NumShadowCascades + 1);
                this.ShadowCascadeLevels.Free();
                this.ShadowCascadeLevels = new UnsafeArray<float>(NumShadowCascades);
                float cDiv = 2.0f;
                int ndIter = 0;
                for (int i = NumShadowCascades - 1; i >= 0; --i)
                {
                    this.ShadowCascadeLevels[i] = 1.0f / cDiv;
                    cDiv = ndIter == 0 ? 10f : ndIter == 1 ? 25f : cDiv * 2.0f;
                    ndIter += 1;
                }

                this.ShadowCascadeLevelsAsVec4 = new Vector4(
                    this.ShadowCascadeLevels[0],
                    this.ShadowCascadeLevels.Length == 1 ? 0 : this.ShadowCascadeLevels[1],
                    this.ShadowCascadeLevels.Length <= 2 ? 0 : this.ShadowCascadeLevels[2],
                    this.ShadowCascadeLevels.Length <= 3 ? 0 : this.ShadowCascadeLevels[3]
                );
            }

            private Matrix4x4 CalculateLightSpaceMatrix(Camera cam, Vector3 sunDirection, float near, float far, int smRes)
            {
                Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(Client.Instance.Settings.FOV * MathF.PI / 180.0f, (float)Client.Instance.Frontend.Width / Client.Instance.Frontend.Height, near, far);
                Matrix4x4 view = cam.View;
                Span<Vector4> pts = stackalloc Vector4[8];
                this.CalculateWorldspaceFrustumCorners(proj, view, ref pts, out Vector4 center);
                Matrix4x4 lightView = VTTMath.CompareFloat(MathF.Abs(sunDirection.Z), 1)
                    ? Matrix4x4.CreateLookAt(center.Xyz() - sunDirection, center.Xyz(), -Vector3.UnitY)
                    : Matrix4x4.CreateLookAt(center.Xyz() - sunDirection, center.Xyz(), Vector3.UnitZ);
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (Vector4 v in pts)
                {
                    Vector3 lsv = Vector4.Transform(v, lightView).AsVector128().AsVector3();
                    min = Vector3.Min(lsv, min);
                    max = Vector3.Max(lsv, max);
                }

                Vector3 unitsPerTexel = new Vector3(max.X - min.X, max.Y - min.Y, max.Z - min.Z) / new Vector3(smRes, smRes, smRes);
                min.X = MathF.Floor(min.X / unitsPerTexel.X) * unitsPerTexel.X;
                min.Y = MathF.Floor(min.Y / unitsPerTexel.Y) * unitsPerTexel.Y;
                min.Z = MathF.Floor(min.Z / unitsPerTexel.Z) * unitsPerTexel.Z;
                max.X = MathF.Floor(max.X / unitsPerTexel.X) * unitsPerTexel.X;
                max.Y = MathF.Floor(max.Y / unitsPerTexel.Y) * unitsPerTexel.Y;
                max.Z = MathF.Floor(max.Z / unitsPerTexel.Z) * unitsPerTexel.Z;

                float zMult = 10.0f;
                if (min.Z < 0)
                {
                    min.Z *= zMult;
                }
                else
                {
                    min.Z /= zMult;
                }

                if (max.Z < 0)
                {
                    max.Z /= zMult;
                }
                else
                {
                    max.Z *= zMult;
                }

                float temp = -min.Z;
                min.Z = -max.Z;
                max.Z = temp;
                Matrix4x4 lightProjection = Matrix4x4.CreateOrthographicOffCenter(min.X, max.X, min.Y, max.Y, min.Z, max.Z);

                return lightView * lightProjection;
            }

            private void CalculateWorldspaceFrustumCorners(Matrix4x4 proj, Matrix4x4 view, ref Span<Vector4> points, out Vector4 center)
            {
                if (!Matrix4x4.Invert(view * proj, out Matrix4x4 inv))
                {
                    inv = Matrix4x4.Identity;
                }

                Vector4 v;
                center = Vector4.Zero;
                v = Vector4.Transform(new Vector4(-1, -1, -1, 1), inv);
                points[0] = v / v.W;
                v = Vector4.Transform(new Vector4(-1, -1, 1, 1), inv);
                points[1] = v / v.W;
                v = Vector4.Transform(new Vector4(-1, 1, -1, 1), inv);
                points[2] = v / v.W;
                v = Vector4.Transform(new Vector4(-1, 1, 1, 1), inv);
                points[3] = v / v.W;
                v = Vector4.Transform(new Vector4(1, -1, -1, 1), inv);
                points[4] = v / v.W;
                v = Vector4.Transform(new Vector4(1, -1, 1, 1), inv);
                points[5] = v / v.W;
                v = Vector4.Transform(new Vector4(1, 1, -1, 1), inv);
                points[6] = v / v.W;
                v = Vector4.Transform(new Vector4(1, 1, 1, 1), inv);
                points[7] = v / v.W;

                center += points[0] * 0.125f;
                center += points[1] * 0.125f;
                center += points[2] * 0.125f;
                center += points[3] * 0.125f;
                center += points[4] * 0.125f;
                center += points[5] * 0.125f;
                center += points[6] * 0.125f;
                center += points[7] * 0.125f;
            }
        }
    }
}
