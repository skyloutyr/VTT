namespace VTT.Render.LightShadow
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using VTT.Asset;
    using VTT.Asset.Glb;
    using VTT.Control;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Render.Shaders;
    using VTT.Util;

    public class PointLightsRenderer
    {
        public const int MaxLightsNum = 16;
        public static int ShadowMapResolution { get; set; } = 256;

        public PointLight[] Lights { get; set; } = new PointLight[MaxLightsNum];
        public int NumLights { get; set; }

        public Texture DepthMap { get; set; }
        public uint FBO { get; set; }
        public FastAccessShader<PointLightShadowUniforms> Shader { get; set; }
        public bool VertexShaderLayerAvailable { get; set; }

        public void Clear()
        {
            this.NumLights = 0;
            this._selectedLights.Clear();
        }

        public void Create()
        {
            int r;
            switch (Client.Instance.Settings.PointShadowsQuality)
            {
                case ClientSettings.GraphicsSetting.Low:
                {
                    r = 128;
                    break;
                }

                case ClientSettings.GraphicsSetting.Medium:
                {
                    r = 256;
                    break;
                }

                case ClientSettings.GraphicsSetting.High:
                {
                    r = 512;
                    break;
                }

                case ClientSettings.GraphicsSetting.Ultra:
                default:
                {
                    r = 1024;
                    break;
                }

            }

            this.VertexShaderLayerAvailable = OpenGLUtil.IsExtensionAvailable("GL_AMD_vertex_shader_layer");
            ShadowMapResolution = r;
            this.DepthMap = new Texture(TextureTarget.Texture2DArray);
            this.DepthMap.Bind();
            OpenGLUtil.NameObject(GLObjectType.Texture, this.DepthMap, "Point light shadow texture array 32d");
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, SizedInternalFormat.DepthComponent32Float, ShadowMapResolution, ShadowMapResolution, 6 * MaxLightsNum, PixelDataFormat.DepthComponent, PixelDataType.Float, IntPtr.Zero);
            this.DepthMap.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this.DepthMap.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            //GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureCompareFunc, (int)Version10.Less);
            //GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureCompareMode, (int)Version30.CompareRefToTexture);

            this.FBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.All, this.FBO);
            OpenGLUtil.NameObject(GLObjectType.Framebuffer, this.FBO, "Point light shadow fbo");
            GL.FramebufferTexture(FramebufferTarget.All, FramebufferAttachment.Depth, this.DepthMap, 0);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(DrawBufferMode.None);
            FramebufferStatus fec = GL.CheckFramebufferStatus(FramebufferTarget.All);
            if (fec != FramebufferStatus.Complete)
            {
                throw new Exception();
            }

            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
            this.Shader = this.VertexShaderLayerAvailable
                ? new FastAccessShader<PointLightShadowUniforms>(OpenGLUtil.LoadShader("pl_ext", stackalloc ShaderType[2] { ShaderType.Vertex, ShaderType.Fragment }))
                : new FastAccessShader<PointLightShadowUniforms>(OpenGLUtil.LoadShader("pl", stackalloc ShaderType[3] { ShaderType.Vertex, ShaderType.Fragment, ShaderType.Geometry }));
        }

        public void ResizeShadowMaps(int resolution)
        {
            this.DepthMap?.Dispose();
            this.DepthMap = new Texture(TextureTarget.Texture2DArray);
            this.DepthMap.Bind();
            OpenGLUtil.NameObject(GLObjectType.Texture, this.DepthMap, "Point light shadow texture array 32d");
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, SizedInternalFormat.DepthComponent32Float, resolution, resolution, 6 * MaxLightsNum, PixelDataFormat.DepthComponent, PixelDataType.Float, IntPtr.Zero);
            this.DepthMap.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this.DepthMap.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            //GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureCompareFunc, (int)Version10.Less);
            //GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureCompareMode, (int)Version30.CompareRefToTexture);
            if (this.FBO != 0)
            {
                GL.DeleteFramebuffer(this.FBO);
            }

            GL.BindFramebuffer(FramebufferTarget.All, this.FBO);
            OpenGLUtil.NameObject(GLObjectType.Framebuffer, this.FBO, "Point light shadow fbo");
            GL.FramebufferTexture(FramebufferTarget.All, FramebufferAttachment.Depth, this.DepthMap, 0);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(DrawBufferMode.None);
            FramebufferStatus fec = GL.CheckFramebufferStatus(FramebufferTarget.All);
            if (fec != FramebufferStatus.Complete)
            {
                throw new Exception();
            }

            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
            ShadowMapResolution = resolution;
        }

        public void PushLight(in PointLight l) => this.Lights[this.NumLights++] = new PointLight(l, this.NumLights - 1);
        public PointLight PopLight() => this.Lights[this.NumLights--];
        public PointLight PeekLight() => this.Lights[this.NumLights - 1];

        private static readonly Vector3[,] LightLook = {
            { Vector3.UnitX, -Vector3.UnitY },
            { -Vector3.UnitX, -Vector3.UnitY },
            { Vector3.UnitY, Vector3.UnitZ },
            { -Vector3.UnitY, -Vector3.UnitZ },
            { Vector3.UnitZ, -Vector3.UnitY },
            { -Vector3.UnitZ, -Vector3.UnitY }
        };

        /*
        public void UniformLights(UniformBlockPointLights uniforms)
        {
            for (int i = 0; i < this.NumLights; ++i)
            {
                uniforms.Positions.Set(this.Lights[i].Position, i);
                uniforms.Colors.Set(this.Lights[i].Color, i);
                uniforms.Thresholds.Set(new Vector2(this.Lights[i].LightPtr.Intensity, this.Lights[i].CastsShadows ? 1.0f : 0.0f), i);
                uniforms.Indices.Set(this.Lights[i].LightIndex, i);
            }

            uniforms.Amount.Set(this.NumLights);
        }
        */

        private readonly UnsafeArray<Vector4> _plPosClrDataCache = new UnsafeArray<Vector4>(16);
        private readonly UnsafeArray<float> _plCutoutDataCache = new UnsafeArray<float>(16);
        public void PackLightData(out int plNum, out Span<Vector4> posColorData, out Span<float> cutoutData)
        {
            for (int i = 0; i < this.NumLights; ++i)
            {
                PointLight pl = this.Lights[i];
                this._plPosClrDataCache[i] = new Vector4(
                    pl.Position,
                    VTTMath.UInt32BitsToSingle(pl.Color.Rgba())
                );

                this._plCutoutDataCache[i] = pl.LightPtr.Intensity * (pl.CastsShadows ? 1.0f : -1.0f);
            }

            posColorData = this._plPosClrDataCache.AsSpan()[..this.NumLights];
            cutoutData = this._plCutoutDataCache.AsSpan()[..this.NumLights];
            plNum = this.NumLights;
        }

        private readonly MatrixStack _ms = new MatrixStack() { Reversed = true };
        private readonly List<PointLight> _selectedLights = new List<PointLight>();
        public void ProcessScene(Matrix4x4 baseModelMatrix, GlbScene s, MapObject owner = null)
        {
            this._ms.Reversed = true;
            if (s.Lights.Count > 0)
            {
                this._ms.Push(baseModelMatrix);
                foreach (GlbObject glbO in s.RootObjects)
                {
                    this.ProcessGlbObjectRecursively(glbO, owner);
                }

                this._ms.Pop();
            }
        }

        private void ProcessGlbObjectRecursively(GlbObject o, MapObject owner)
        {
            this._ms.Push(o.LocalCachedTransform);
            if (o.Type == GlbObjectType.Light && o.Light.LightType == KhrLight.LightTypeEnum.Point)
            {
                PointLight pl = new PointLight(this._ms, o.Light, this.NumLights, owner);
                this._selectedLights.Add(pl);
            }

            foreach (GlbObject child in o.Children)
            {
                this.ProcessGlbObjectRecursively(child, owner);
            }

            this._ms.Pop();
        }

        public void AddLightCandidate(PointLight pl) => this._selectedLights.Add(pl);

        private readonly List<MapObject> _objsCache = new List<MapObject>();
        private readonly Matrix4x4[] _lightMatrices = new Matrix4x4[6];
        private bool _hadLightsLastFrame;

        public void DrawLights(Map m, bool doDraw, double delta, Camera cam = null)
        {
            if (cam != null) // Frustum cull, sort and push lights
            {
                for (int i = this._selectedLights.Count - 1; i >= 0; i--)
                {
                    PointLight pl = this._selectedLights[i];
                    if (!cam.IsSphereInFrustum(pl.Position, pl.LightPtr.Intensity))
                    {
                        this._selectedLights.RemoveAt(i);
                        continue;
                    }
                }

                this._selectedLights.Sort((l, r) => (l.Position - cam.Position).Length().CompareTo((r.Position - cam.Position).Length()));
            }

            for (int i = 0; i < this._selectedLights.Count && i < MaxLightsNum; ++i) // Push all eligible light sources
            {
                this.PushLight(this._selectedLights[i]);
            }

            if (this.NumLights == 0 && !this._hadLightsLastFrame)
            {
                return;
            }

            this._hadLightsLastFrame = this.NumLights > 0;
            GL.BindFramebuffer(FramebufferTarget.All, this.FBO);
            GL.Viewport(0, 0, ShadowMapResolution, ShadowMapResolution);
            GLState.Clear(ClearBufferMask.Depth);
            GL.ColorMask(false, false, false, false);

            if (doDraw && m != null)
            {
                this.Shader.Program.Bind();
                SunShadowRenderer.ShadowPass = true;
                GLState.CullFace.Set(false);
                Span<Frustum> frustums = stackalloc Frustum[6];
                for (int i1 = 0; i1 < this.NumLights; i1++)
                {
                    PointLight pl = this.Lights[i1];
                    Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(90 * MathF.PI / 180, 1, 0.01f, pl.LightPtr.Intensity);
                    Vector3 lightPos = pl.Position;
                    for (int i = 0; i < 6; ++i)
                    {
                        this._lightMatrices[i] = Matrix4x4.CreateLookAt(lightPos, lightPos + LightLook[i, 0], LightLook[i, 1]) * proj;
                        if (this.VertexShaderLayerAvailable)
                        {
                            frustums[i] = new Frustum(this._lightMatrices[i]);
                        }
                    }

                    if (!this.VertexShaderLayerAvailable)
                    {
                        this.Shader.Uniforms.LayerOffset.Set(pl.LightIndex * 6);
                        for (int i = 0; i < 6; ++i)
                        {
                            this.Shader.Uniforms.ProjView.Set(this._lightMatrices[i], i);
                        }
                    }

                    this.Shader.Uniforms.LightPosition.Set(pl.Position);
                    this.Shader.Uniforms.LightFarPlane.Set(pl.LightPtr.Intensity);

                    this._objsCache.Clear();
                    MapObject owner = pl.ObjectPtr;
                    if (owner == null || !owner.LightsCastShadows)
                    {
                        continue; // Can't draw data without owner
                    }

                    foreach (MapObject mo in m.IterateObjects(null))
                    {
                        if (!mo.CastsShadow)
                        {
                            continue;
                        }

                        if (mo.DoNotRender)
                        {
                            continue;
                        }

                        if (mo.MapLayer <= 0 && (mo != pl.ObjectPtr || pl.ObjectPtr.LightsSelfCastsShadow))
                        {
                            AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(mo.AssetID, AssetType.Model, out Asset a);
                            if (status == AssetStatus.Return && a.ModelGlReady)
                            {
                                if (mo.CameraCullerBox.Contains(pl.Position - mo.Position) || mo.CameraCullerBox.IntersectsSphere(pl.Position - mo.Position, pl.LightPtr.Intensity))
                                {
                                    Matrix4x4 modelMatrix = mo.ClientCachedModelMatrix;
                                    if (this.VertexShaderLayerAvailable)
                                    {
                                        int nInstances = 0;
                                        for (int l = 0; l < 6; ++l)
                                        {
                                            if (frustums[l].IsSphereInFrustumCached(ref mo.LightShadowCullingSpheres[i1 + 1]))
                                            {
                                                this.Shader.Uniforms.LayerOffset.Set((pl.LightIndex * 6) + l, nInstances);
                                                this.Shader.Uniforms.ProjView.Set(this._lightMatrices[l], nInstances++);
                                                //a.Model.GLMdl.Render(in this.Shader.Uniforms.glbEssentials, modelMatrix, proj, this._lightMatrices[l], 0, mo.AnimationContainer.CurrentAnimation, mo.AnimationContainer.GetTime(delta), mo.AnimationContainer);
                                            }
                                        }

                                        if (nInstances > 0)
                                        {
                                            a.Model.GLMdl.Render(in this.Shader.Uniforms.glbEssentials, modelMatrix, proj, this._lightMatrices[0], 0, mo.AnimationContainer.CurrentAnimation, mo.AnimationContainer.GetTime(delta), mo.AnimationContainer, x => GLState.DrawElementsInstanced(PrimitiveType.Triangles, x.AmountToRender, ElementsType.UnsignedInt, 0, nInstances));
                                        }
                                    }
                                    else
                                    {
                                        a.Model.GLMdl.Render(in this.Shader.Uniforms.glbEssentials, modelMatrix, proj, this._lightMatrices[0], 0, mo.AnimationContainer.CurrentAnimation, mo.AnimationContainer.GetTime(delta), mo.AnimationContainer);
                                    }
                                }
                            }
                        }
                    }
                }

                SunShadowRenderer.ShadowPass = false;
                GLState.CullFace.Set(true);
            }

            GL.Viewport(0, 0, Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
            GL.ColorMask(true, true, true, true);
            GL.BindFramebuffer(FramebufferTarget.All, 0);
            GL.DrawBuffer(DrawBufferMode.Back);
        }
    }

    public readonly struct PointLight
    {
        public Vector3 Position { get; }
        public Vector3 Color { get; }
        public int LightIndex { get; }
        public MapObject ObjectPtr { get; }

        public bool CastsShadows { get; }
        public bool CastsOwnShadow { get; }

        public GlbLight LightPtr { get; }

        public PointLight(Vector3 position, Vector3 color, int lightIndex, MapObject objectPtr, bool castsShadows, bool castsOwnShadow, GlbLight lightPtr)
        {
            this.Position = position;
            this.Color = color;
            this.LightIndex = lightIndex;
            this.ObjectPtr = objectPtr;
            this.CastsShadows = castsShadows;
            this.CastsOwnShadow = castsOwnShadow;
            this.LightPtr = lightPtr;
        }

        public PointLight(MatrixStack ms, GlbLight light, int lightIndex, MapObject objectPtr)
        {
            this.Position = Vector4.Transform(new Vector4(0, 0, 0, 1), ms.Current).Xyz();
            this.Color = light.Color.Xyz();
            this.LightPtr = light;
            this.LightIndex = lightIndex;
            this.ObjectPtr = objectPtr;
            this.CastsOwnShadow = objectPtr?.LightsSelfCastsShadow ?? false;
            this.CastsShadows = objectPtr?.LightsCastShadows ?? false;
        }

        public PointLight(PointLight copy, int lightIndex)
        {
            this.Position = copy.Position;
            this.Color = copy.Color;
            this.LightPtr = copy.LightPtr;
            this.LightIndex = lightIndex;
            this.ObjectPtr = copy.ObjectPtr;
            this.CastsShadows = copy.CastsShadows;
            this.CastsOwnShadow = copy.CastsOwnShadow;
        }
    }
}
