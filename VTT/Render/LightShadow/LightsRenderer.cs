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
    using VTT.Util;

    public class PointLightsRenderer
    {
        public const int MaxLightsNum = 16;
        public static int ShadowMapResolution { get; set; } = 256;

        static PointLightsRenderer()
        {
            constPositionPtrs = new string[MaxLightsNum];
            constColorPtrs = new string[MaxLightsNum];
            constCutoutPtrs = new string[MaxLightsNum];
            constIndexPtrs = new string[MaxLightsNum];
            for (int i = 0; i < MaxLightsNum; ++i)
            {
                constPositionPtrs[i] = "pl_position[" + i + "]";
                constColorPtrs[i] = "pl_color[" + i + "]";
                constCutoutPtrs[i] = "pl_cutout[" + i + "]";
                constIndexPtrs[i] = "pl_index[" + i + "]";
            }
        }

        public PointLight[] Lights { get; set; } = new PointLight[MaxLightsNum];
        public int NumLights { get; set; }

        public Texture DepthMap { get; set; }
        public uint FBO { get; set; }
        public FastAccessShader Shader { get; set; }

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

            ShadowMapResolution = r;
            this.DepthMap = new Texture(TextureTarget.Texture2DArray);
            this.DepthMap.Bind();
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, SizedInternalFormat.DepthComponent32Float, ShadowMapResolution, ShadowMapResolution, 6 * MaxLightsNum, PixelDataFormat.DepthComponent, PixelDataType.Float, IntPtr.Zero);
            this.DepthMap.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
            this.DepthMap.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
            //GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureCompareFunc, (int)Version10.Less);
            //GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureCompareMode, (int)Version30.CompareRefToTexture);

            this.FBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.All, this.FBO);
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
            this.Shader = new FastAccessShader(OpenGLUtil.LoadShader("pl", ShaderType.Vertex, ShaderType.Fragment, ShaderType.Geometry));
        }

        public void ResizeShadowMaps(int resolution)
        {
            this.DepthMap?.Dispose();
            this.DepthMap = new Texture(TextureTarget.Texture2DArray);
            this.DepthMap.Bind();
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

        private static readonly string[] constPositionPtrs;
        private static readonly string[] constColorPtrs;
        private static readonly string[] constCutoutPtrs;
        private static readonly string[] constIndexPtrs;

        private static readonly Vector3[,] LightLook = {
            { Vector3.UnitX, -Vector3.UnitY },
            { -Vector3.UnitX, -Vector3.UnitY },
            { Vector3.UnitY, Vector3.UnitZ },
            { -Vector3.UnitY, -Vector3.UnitZ },
            { Vector3.UnitZ, -Vector3.UnitY },
            { -Vector3.UnitZ, -Vector3.UnitY }
        };

        public void UniformLights(ShaderProgram shader)
        {
            for (int i = 0; i < this.NumLights; ++i)
            {
                shader[constPositionPtrs[i]].Set(this.Lights[i].Position);
                shader[constColorPtrs[i]].Set(this.Lights[i].Color);
                shader[constCutoutPtrs[i]].Set(new Vector2(this.Lights[i].LightPtr.Intensity, this.Lights[i].CastsShadows ? 1.0f : 0.0f));
                shader[constIndexPtrs[i]].Set(this.Lights[i].LightIndex);
            }

            shader["pl_num"].Set(this.NumLights);
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
        public void DrawLights(Map m, bool doDraw, double delta, Camera cam = null)
        {
            if (cam != null) // Frustrum cull, sort and push lights
            {
                for (int i = this._selectedLights.Count - 1; i >= 0; i--)
                {
                    PointLight pl = this._selectedLights[i];
                    if (!cam.IsSphereInFrustrum(pl.Position, pl.LightPtr.Intensity))
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

            GL.BindFramebuffer(FramebufferTarget.All, this.FBO);
            GL.Viewport(0, 0, ShadowMapResolution, ShadowMapResolution);
            GL.Clear(ClearBufferMask.Depth);
            GL.ColorMask(false, false, false, false);

            if (doDraw && m != null)
            {
                this.Shader.Program.Bind();
                SunShadowRenderer.ShadowPass = true;
                GL.Disable(Capability.CullFace);
                for (int i1 = 0; i1 < this.NumLights; i1++)
                {
                    PointLight pl = this.Lights[i1];
                    Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(90 * MathF.PI / 180, 1, 0.0001f, pl.LightPtr.Intensity);
                    Vector3 lightPos = pl.Position;
                    for (int i = 0; i < 6; ++i)
                    {
                        this._lightMatrices[i] = Matrix4x4.CreateLookAt(lightPos, lightPos + LightLook[i, 0], LightLook[i, 1]) * proj;
                    }

                    this.Shader["layer_offset"].Set(pl.LightIndex * 6);
                    this.Shader["light_pos"].Set(pl.Position);
                    this.Shader["far_plane"].Set(pl.LightPtr.Intensity);
                    for (int i = 0; i < 6; ++i)
                    {
                        this.Shader[$"projView[{i}]"].Set(this._lightMatrices[i]);
                    }

                    this._objsCache.Clear();
                    MapObject owner = pl.ObjectPtr;
                    if (owner == null || !owner.LightsCastShadows)
                    {
                        continue; // Can't draw data without owner
                    }

                    foreach (MapObject mo in m.Objects)
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
                            if (status == AssetStatus.Return && (a?.Model?.GLMdl?.GlReady ?? false))
                            {
                                if (mo.CameraCullerBox.Contains(pl.Position - mo.Position) || mo.CameraCullerBox.IntersectsSphere(pl.Position - mo.Position, pl.LightPtr.Intensity))
                                {
                                    Matrix4x4 modelMatrix = mo.ClientCachedModelMatrix;
                                    a.Model.GLMdl.Render(this.Shader, modelMatrix, proj, this._lightMatrices[0], 0, mo.AnimationContainer.CurrentAnimation, mo.AnimationContainer.GetTime(delta), mo.AnimationContainer);
                                }
                            }
                        }
                    }
                }

                SunShadowRenderer.ShadowPass = false;
                GL.Enable(Capability.CullFace);
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
