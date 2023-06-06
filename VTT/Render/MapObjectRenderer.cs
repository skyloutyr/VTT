namespace VTT.Render
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using VTT.Asset;
    using VTT.Asset.Obj;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Render.LightShadow;
    using VTT.Util;

    public class MapObjectRenderer
    {
        private VertexArray _boxVao;
        private GPUBuffer _boxVbo;

        private VertexArray _noAssetVao;
        private GPUBuffer _noAssetVbo;
        private GPUBuffer _noAssetEbo;

        public ShaderProgram RenderShader { get; set; }
        public ShaderProgram HighlightShader { get; set; }
        public ShaderProgram OverlayShader { get; set; }
        public MapObject ObjectMouseOver { get; set; }
        public Vector3 MouseHitWorld { get; set; }

        public EditMode EditMode { get; set; } = EditMode.Select;
        public bool MoveModeArrows { get; set; }

        public WavefrontObject MoveArrow { get; set; }
        public WavefrontObject MoveSide { get; set; }
        public WavefrontObject MoveCenter { get; set; }

        public WavefrontObject ScaleArrow { get; set; }
        public WavefrontObject ScaleSide { get; set; }
        public WavefrontObject ScaleCenter { get; set; }
        public WavefrontObject ArrowMove { get; set; }
        public WavefrontObject ArrowMoveOutline { get; set; }

        public WavefrontObject RotateCircle { get; set; }
        public WavefrontObject Cross { get; set; }

        public SunShadowRenderer DirectionalLightRenderer { get; set; }
        public DeferredPipeline DeferredPipeline { get; set; }

        private Vector3 _cachedSunDir;
        private Color   _cachedSunColor;
        private Vector3 _cachedAmbientColor;
        private Color   _cachedSkyColor;
        private ShaderContainerLocalPassthroughData _passthroughData = new ShaderContainerLocalPassthroughData();

        public void Create()
        {
            this._noAssetVao = new VertexArray();
            this._noAssetVbo = new GPUBuffer(BufferTarget.ArrayBuffer);
            this._noAssetEbo = new GPUBuffer(BufferTarget.ElementArrayBuffer);
            this._noAssetVao.Bind();
            this._noAssetVbo.Bind();
            this._noAssetVbo.SetData(new float[] {
                0.5f, 0.5f, -0.5f, 0, 0,
                0.5f,-0.5f, -0.5f, 0, 0,
                0.5f, 0.5f,  0.5f, 0, 0,
                0.5f,-0.5f,  0.5f, 0, 0,
               -0.5f, 0.5f, -0.5f, 0, 0,
               -0.5f,-0.5f, -0.5f, 0, 0,
               -0.5f, 0.5f,  0.5f, 0, 0,
               -0.5f,-0.5f,  0.5f, 0, 0,
            });

            this._noAssetEbo.Bind();
            this._noAssetEbo.SetData(new uint[] {
                5 - 1, 3 - 1, 1 - 1,
                3 - 1, 8 - 1, 4 - 1,
                7 - 1, 6 - 1, 8 - 1,
                2 - 1, 8 - 1, 6 - 1,
                1 - 1, 4 - 1, 2 - 1,
                5 - 1, 2 - 1, 6 - 1,
                5 - 1, 7 - 1, 3 - 1,
                3 - 1, 7 - 1, 8 - 1,
                7 - 1, 5 - 1, 6 - 1,
                2 - 1, 4 - 1, 8 - 1,
                1 - 1, 3 - 1, 4 - 1,
                5 - 1, 1 - 1, 2 - 1,
            });
            this._noAssetVao.Reset();
            this._noAssetVao.SetVertexSize<float>(5);
            this._noAssetVao.PushElement(ElementType.Vec3);
            this._noAssetVao.PushElement(ElementType.Vec2);

            this.OverlayShader = OpenGLUtil.LoadShader("moverlay", ShaderType.VertexShader, ShaderType.FragmentShader);
            this.ReloadObjectShader(Client.Instance.Settings.EnableSunShadows, Client.Instance.Settings.EnableDirectionalShadows, Client.Instance.Settings.DisableShaderBranching);
            this.HighlightShader = OpenGLUtil.LoadShader("highlight", ShaderType.VertexShader, ShaderType.FragmentShader);
            this.PrecomputeSelectionBox();

            this.MoveArrow = OpenGLUtil.LoadModel("arrow_arrow", VertexFormat.Pos);
            this.MoveSide = OpenGLUtil.LoadModel("arrow_square", VertexFormat.Pos);
            this.MoveCenter = OpenGLUtil.LoadModel("arrow_center_arrow", VertexFormat.Pos);

            this.ArrowMove = OpenGLUtil.LoadModel("arrow_move", VertexFormat.Pos);
            this.ArrowMoveOutline = OpenGLUtil.LoadModel("arrow_move_outline", VertexFormat.Pos);

            this.ScaleArrow = OpenGLUtil.LoadModel("arrow_cube", VertexFormat.Pos);
            this.ScaleSide = OpenGLUtil.LoadModel("arrow_square", VertexFormat.Pos);
            this.ScaleCenter = OpenGLUtil.LoadModel("arrow_center_square", VertexFormat.Pos);

            this.RotateCircle = OpenGLUtil.LoadModel("arrow_ring", VertexFormat.Pos);
            this.Cross = OpenGLUtil.LoadModel("cross", VertexFormat.Pos);

            this.DirectionalLightRenderer = new SunShadowRenderer();
            this.DirectionalLightRenderer.Create();
            this.DeferredPipeline = new DeferredPipeline();
            if (Client.Instance.Settings.Pipeline == ClientSettings.PipelineType.Deferred)
            {
                this.DeferredPipeline.Create();
            }

            this.FrameUBOManager = new FrameUBOManager();
        }

        #region Hightlight Box

        private static readonly double[] vertexData = {
            -1.000000, -1.000000, -1.000000,
            -1.000000, -1.000000, 1.000000 ,
            -1.000000, 1.000000 ,-1.000000 ,
            -1.000000, 1.000000 ,1.000000  ,
            1.000000 ,-1.000000 ,-1.000000 ,
            1.000000 ,-1.000000 ,1.000000  ,
            1.000000 ,1.000000 ,-1.000000  ,
            1.000000 ,1.000000 ,1.000000   ,
            5.000000 ,1.000000 ,1.000000   ,
            5.000000 ,1.000000 ,-1.000000  ,
            5.000000 ,-1.000000, -1.000000 ,
            5.000000 ,-1.000000, 1.000000  ,
            -1.000000, 5.000000, 1.000000  ,
            -1.000000, 5.000000, -1.000000 ,
            1.000000 ,5.000000 ,-1.000000  ,
            1.000000 ,5.000000 ,1.000000   ,
            -1.000000, -1.000000, 5.000000 ,
            -1.000000, 1.000000 ,5.000000  ,
            1.000000 ,1.000000 ,5.000000   ,
            1.000000 ,-1.000000, 5.000000
        };

        private static readonly int[] indexData = {
            2, 3,  1,
            8, 13, 4,
            7, 11, 5,
            6, 1,  5,
            7, 1,  3,
            4, 19, 8,
            8, 10, 7,
            5, 12, 6,
            4, 14, 3,
            7, 16, 8,
            3, 15, 7,
            8, 20, 6,
            2, 18, 4,
            6, 17, 2,
            6, 12, 9,
            6, 9,  8,
            2, 4,  3,
            6, 2,  1,
            7, 5,  1,
            9, 11, 10,
            13,15, 14,
            18,20, 19,
            8, 16, 13,
            7, 10, 11,
            4, 18, 19,
            9, 12, 11,
            5, 11, 12,
            13,16, 15,
            4, 13, 14,
            7, 15, 16,
            3, 14, 15,
            18,17, 20,
            8, 19, 20,
            2, 17, 18,
            6, 20, 17,
            8, 9,  10,
        };

        private static readonly Quaternion[] boxRotationalQuaternions = new Quaternion[] {
            Quaternion.Identity,                                                       // -X, -Y, -Z
            Quaternion.FromAxisAngle(Vector3.UnitZ, MathHelper.DegreesToRadians(90)),  // +X, -Y, -Z
            Quaternion.FromAxisAngle(Vector3.UnitZ, MathHelper.DegreesToRadians(-90)), // -X, +Y, -Z
            Quaternion.FromAxisAngle(Vector3.UnitZ, MathHelper.DegreesToRadians(180)), // +X, +Y, -Z
            Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(180)), // -X, +Y, +Z
            Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(180)), // +X, -Y, +Z
            Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(90)),  // -X, -Y, +Z
            new Quaternion(0.707f, -0.707f, 0f, -0f)                                   // +X, +Y, +Z and we don't talk about the -0f                                  
        };

        private static readonly Vector3[] boxTranslations = new Vector3[] {
            new Vector3(-1f, -1f, -1f),
            new Vector3(1f, -1f, -1f),
            new Vector3(-1f, 1f, -1f),
            new Vector3(1f, 1f, -1f),
            new Vector3(-1f, 1f, 1f),
            new Vector3(1f, -1f, 1f),
            new Vector3(-1f, -1f, 1f),
            new Vector3(1f, 1f, 1f),
        };

        public FrameUBOManager FrameUBOManager { get; private set; }

        public void ReloadObjectShader(bool dirShadows, bool pointShadows, bool noBranches)
        {
            string lineVert = IOVTT.ResourceToString("VTT.Embed.object.vert");
            string lineFrag = IOVTT.ResourceToString("VTT.Embed.object.frag");
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

            this.RenderShader?.Dispose();

            if (!ShaderProgram.TryCompile(out ShaderProgram sp, lineVert, null, lineFrag, out string err))
            {
                Logger l = Client.Instance.Logger;
                l.Log(LogLevel.Fatal, "Could not compile shader! Shader error was " + err);
                throw new Exception("Could not compile object shader! Shader error was " + err);
            }

            this.RenderShader = sp;
            this.RenderShader.Bind();
            this.RenderShader.BindUniformBlock("FrameData", 1);
            this.RenderShader["m_texture_diffuse"].Set(0);
            this.RenderShader["m_texture_normal"].Set(1);
            this.RenderShader["m_texture_emissive"].Set(2);
            this.RenderShader["m_texture_aomr"].Set(3);
            this.RenderShader["pl_shadow_maps"].Set(13);
            this.RenderShader["dl_shadow_map"].Set(14);
            if (Client.Instance.Settings.Pipeline == ClientSettings.PipelineType.Deferred)
            {
                this.DeferredPipeline?.RecompileShaders(dirShadows, pointShadows, noBranches);
            }
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

        public void PrecomputeSelectionBox()
        {
            float[] boxDataArray = new float[5184];
            int aIdx = 0;
            for (int i = 0; i < 8; ++i)
            {
                for (int j = 0; j < indexData.Length; ++j)
                {
                    int idx = indexData[j] - 1;
                    Vector3 v = new Vector3((float)vertexData[(idx * 3) + 0], (float)vertexData[(idx * 3) + 1], (float)vertexData[(idx * 3) + 2]);
                    v = v / 50.0f;
                    v = (boxRotationalQuaternions[i] * new Vector4(v, 1.0f)).Xyz;
                    v += boxTranslations[i] / 2f;
                    boxDataArray[aIdx++] = v.X;
                    boxDataArray[aIdx++] = v.Y;
                    boxDataArray[aIdx++] = v.Z;

                    boxDataArray[aIdx++] = -boxTranslations[i].X;
                    boxDataArray[aIdx++] = -boxTranslations[i].Y;
                    boxDataArray[aIdx++] = -boxTranslations[i].Z;
                }
            }

            this._boxVao = new VertexArray();
            this._boxVbo = new GPUBuffer(BufferTarget.ArrayBuffer);
            this._boxVao.Bind();
            this._boxVbo.Bind();
            this._boxVbo.SetData(boxDataArray);
            this._boxVao.Reset();
            this._boxVao.SetVertexSize<float>(6);
            this._boxVao.PushElement(ElementType.Vec3);
            this._boxVao.PushElement(ElementType.Vec3);
        }

        #endregion


        private readonly List<(Vector3, MapObject)> _mouseOverList = new List<(Vector3, MapObject)>();
        public void Update(Map m, double delta)
        {
            this.ObjectMouseOver = null;
            if (m != null && this.EditMode != EditMode.FOW)
            {
                Ray r = Client.Instance.Frontend.Renderer.MapRenderer.RayFromCursor();
                this._mouseOverList.Clear();

                RaycastResut rr = RaycastResut.Raycast(r, m, o => o.MapLayer == Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer);
                if (rr.Result)
                {
                    this.ObjectMouseOver = rr.ObjectHit;
                    this.MouseHitWorld = rr.Hit;
                }
            }
        }

        public void RenderEarly(Map m, double delta)
        { 
        }
        
        public void RenderLate(Map m, double delta)
        {
            if (m != null)
            {
                this.RenderAuras(m);
            }
        }

        public void RenderLatest(Map m, double delta)
        {
            if (m != null)
            {
                this.RenderEditMode(m);
            }
        }

        public void Resize(int w, int h)
        {
            if (Client.Instance.Settings.Pipeline == ClientSettings.PipelineType.Deferred)
            {
                this.DeferredPipeline.Resize(w, h);
            }
        }

        public void Render(Map m, double delta)
        {
            if (m != null)
            {
                this._cachedSunDir = Client.Instance.Frontend.Renderer.SkyRenderer.GetCurrentSunDirection();
                this._cachedSunColor = Client.Instance.Frontend.Renderer.SkyRenderer.GetSunColor();
                this._cachedAmbientColor = Client.Instance.Frontend.Renderer.SkyRenderer.GetAmbientColor().Vec3();
                this._cachedSkyColor = Client.Instance.Frontend.Renderer.SkyRenderer.GetSkyColor();

                this.DirectionalLightRenderer.Render(m, delta);
                this.UpdateUBO(m, delta);
                if (Client.Instance.Settings.Pipeline == ClientSettings.PipelineType.Forward)
                {
                    this.RenderForward(m, delta);
                }
                else
                {
                    this.RenderDeferred(m, delta);
                }

                this.RenderObjectMouseOver(m);
                this.RenderDebug(m);
            }
        }

        public void RenderDebug(Map m)
        {
        }

        private void RenderEditMode(Map m)
        {
            SelectionManager sm = Client.Instance.Frontend.Renderer.SelectionManager;
            if (sm.SelectedObjects.Count > 0)
            {
                Vector3 min = sm.SelectedObjects[0].Position;
                Vector3 max = sm.SelectedObjects[0].Position;
                for (int i = 1; i < sm.SelectedObjects.Count; i++)
                {
                    MapObject mo = sm.SelectedObjects[i];
                    min = Vector3.ComponentMin(min, mo.Position);
                    max = Vector3.ComponentMax(max, mo.Position);
                }

                Vector3 half;
                if (sm.HalfRenderVector.HasValue)
                {
                    half = sm.HalfRenderVector.Value;
                }
                else
                {
                    half = (max - min) / 2;
                    half = min + half;
                }

                Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;

                if (this.EditMode == EditMode.Select)
                {
                    return;
                }

                bool is2d = Client.Instance.Frontend.Renderer.MapRenderer.IsOrtho;
                float orthozoom = Client.Instance.Frontend.Renderer.MapRenderer.ZoomOrtho;
                GL.Enable(EnableCap.CullFace);
                GL.CullFace(CullFaceMode.Back);
                GL.Disable(EnableCap.DepthTest);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.DepthMask(false);

                switch (this.EditMode)
                {
                    case EditMode.Translate:
                    {
                        if (this.MoveModeArrows)
                        {
                            float dotZ = MathF.Abs(Vector3.Dot(Vector3.UnitZ, cam.Direction));
                            float dotX = MathF.Abs(Vector3.Dot(Vector3.UnitX, cam.Direction));
                            float dotY = MathF.Abs(Vector3.Dot(Vector3.UnitY, cam.Direction));
                            Vector3 majorAxis =
                                dotZ > dotX && dotZ > dotY ? Vector3.UnitZ :
                                dotY > dotX ? Vector3.UnitY : Vector3.UnitX;

                            Vector3 offsetAxis =
                                dotZ > dotX && dotZ > dotY ? new Vector3(0, -1.5f, 0) :
                                dotY > dotX ? new Vector3(0, 0, -1.5f) : new Vector3(0, -1.5f, 0);

                            Matrix4 baseRotation =
                                dotZ > dotX && dotZ > dotY ? Matrix4.Identity :
                                dotY > dotX ? Matrix4.CreateRotationY(MathF.PI * 0.5f) * Matrix4.CreateRotationX(MathF.PI * 0.5f) * Matrix4.CreateRotationZ(MathF.PI * 0.5f): 
                                Matrix4.CreateRotationY(MathF.PI * 0.5f);

                            Vector4 renderClr = (
                                dotZ <= dotX || dotZ <= dotY ? Color.SkyBlue.Vec4() :
                                Color.GreenYellow.Vec4()) * new Vector4(1, 1, 1, 0.75f);
                            Vector4 blue = dotZ <= dotX || dotZ <= dotY ? Color.Blue.Vec4() : Color.Gold.Vec4();

                            for (int i = 0; i < 8; ++i)
                            {
                                Quaternion q = Quaternion.FromAxisAngle(majorAxis, MathF.PI * (i * 0.25f));
                                Matrix4 modelMatrix = baseRotation * Matrix4.CreateTranslation(offsetAxis) * Matrix4.CreateScale(0.5f) * Matrix4.CreateFromQuaternion(q) * Matrix4.CreateTranslation(half);
                                this.OverlayShader.Bind();
                                this.OverlayShader["view"].Set(cam.View);
                                this.OverlayShader["projection"].Set(cam.Projection);
                                this.OverlayShader["model"].Set(modelMatrix);
                                this.OverlayShader["u_color"].Set(renderClr);
                                this.ArrowMove.Render();
                                this.OverlayShader["u_color"].Set(blue);
                                this.ArrowMoveOutline.Render();
                            }

                            break;
                        }
                        else
                        {
                            if (is2d)
                            {
                                Matrix4 modelMatrix;
                                this.OverlayShader.Bind();
                                this.OverlayShader["view"].Set(cam.View);
                                this.OverlayShader["projection"].Set(cam.Projection);

                                modelMatrix = Matrix4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4.CreateScale(150f * orthozoom) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(90)) * Matrix4.CreateTranslation(half);
                                this.OverlayShader["model"].Set(modelMatrix);
                                this.OverlayShader["u_color"].Set(Color.Red.Vec4());
                                this.MoveArrow.Render();

                                modelMatrix = Matrix4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4.CreateScale(150f * orthozoom) * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-90)) * Matrix4.CreateTranslation(half);
                                this.OverlayShader["model"].Set(modelMatrix);
                                this.OverlayShader["u_color"].Set(Color.Green.Vec4());
                                this.MoveArrow.Render();

                                modelMatrix = Matrix4.CreateScale(150f * orthozoom) * Matrix4.CreateTranslation(half);
                                this.OverlayShader["model"].Set(modelMatrix);
                                this.OverlayShader["u_color"].Set(Color.Blue.Vec4() * new Vector4(1, 1, 1, 0.75f));
                                this.MoveSide.Render();
                            }
                            else
                            {
                                Matrix4 viewProj = cam.ViewProj;
                                Vector4 posScreen = new Vector4(half, 1.0f) * viewProj;
                                Matrix4 modelMatrix = Matrix4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4.CreateScale(0.2f * posScreen.W) * Matrix4.CreateTranslation(half);

                                this.OverlayShader.Bind();
                                this.OverlayShader["view"].Set(cam.View);
                                this.OverlayShader["projection"].Set(cam.Projection);
                                this.OverlayShader["model"].Set(modelMatrix);
                                this.OverlayShader["u_color"].Set(Color.Blue.Vec4());
                                this.MoveArrow.Render();

                                modelMatrix = Matrix4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4.CreateScale(0.2f * posScreen.W) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(90)) * Matrix4.CreateTranslation(half);
                                this.OverlayShader["model"].Set(modelMatrix);
                                this.OverlayShader["u_color"].Set(Color.Red.Vec4());
                                this.MoveArrow.Render();

                                modelMatrix = Matrix4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4.CreateScale(0.2f * posScreen.W) * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-90)) * Matrix4.CreateTranslation(half);
                                this.OverlayShader["model"].Set(modelMatrix);
                                this.OverlayShader["u_color"].Set(Color.Green.Vec4());
                                this.MoveArrow.Render();

                                modelMatrix = Matrix4.CreateTranslation(new Vector3(0.5f, 0.5f, 0)) * Matrix4.CreateScale(0.2f * posScreen.W) * Matrix4.CreateTranslation(half);
                                this.OverlayShader["model"].Set(modelMatrix);
                                this.OverlayShader["u_color"].Set(Color.Blue.Vec4() * new Vector4(1, 1, 1, 0.75f));
                                this.MoveSide.Render();

                                modelMatrix = Matrix4.CreateTranslation(new Vector3(0.5f, 0.5f, 0f)) * Matrix4.CreateScale(0.2f * posScreen.W) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(-90)) * Matrix4.CreateTranslation(half);
                                this.OverlayShader["model"].Set(modelMatrix);
                                this.OverlayShader["u_color"].Set(Color.Red.Vec4() * new Vector4(1, 1, 1, 0.75f));
                                this.MoveSide.Render();

                                modelMatrix = Matrix4.CreateTranslation(new Vector3(0.5f, 0.5f, 0f)) * Matrix4.CreateScale(0.2f * posScreen.W) * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(90)) * Matrix4.CreateTranslation(half);
                                this.OverlayShader["model"].Set(modelMatrix);
                                this.OverlayShader["u_color"].Set(Color.Green.Vec4() * new Vector4(1, 1, 1, 0.75f));
                                this.MoveSide.Render();

                                modelMatrix = Matrix4.CreateScale(0.1f * posScreen.W) * Matrix4.CreateTranslation(half);
                                this.OverlayShader["model"].Set(modelMatrix);
                                this.OverlayShader["u_color"].Set(Color.White.Vec4());
                                this.MoveCenter.Render();
                            }

                            break;
                        }
                    }

                    case EditMode.Scale:
                    {
                        if (is2d)
                        {
                            Matrix4 modelMatrix;
                            this.OverlayShader.Bind();
                            this.OverlayShader["view"].Set(cam.View);
                            this.OverlayShader["projection"].Set(cam.Projection);

                            modelMatrix = Matrix4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4.CreateScale(150f * orthozoom) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(90)) * Matrix4.CreateTranslation(half);
                            this.OverlayShader["model"].Set(modelMatrix);
                            this.OverlayShader["u_color"].Set(Color.Red.Vec4());
                            this.ScaleArrow.Render();

                            modelMatrix = Matrix4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4.CreateScale(150f * orthozoom) * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-90)) * Matrix4.CreateTranslation(half);
                            this.OverlayShader["model"].Set(modelMatrix);
                            this.OverlayShader["u_color"].Set(Color.Green.Vec4());
                            this.ScaleArrow.Render();

                            modelMatrix = Matrix4.CreateScale(150f * orthozoom) * Matrix4.CreateTranslation(half);
                            this.OverlayShader["model"].Set(modelMatrix);
                            this.OverlayShader["u_color"].Set(Color.Blue.Vec4() * new Vector4(1, 1, 1, 0.75f));
                            this.MoveSide.Render();
                        }
                        else
                        {
                            Matrix4 viewProj = cam.ViewProj;
                            Vector4 posScreen = new Vector4(half, 1.0f) * viewProj;
                            Matrix4 modelMatrix = Matrix4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4.CreateScale(0.2f * posScreen.W) * Matrix4.CreateTranslation(half);

                            this.OverlayShader.Bind();
                            this.OverlayShader["view"].Set(cam.View);
                            this.OverlayShader["projection"].Set(cam.Projection);
                            this.OverlayShader["model"].Set(modelMatrix);
                            this.OverlayShader["u_color"].Set(Color.Blue.Vec4());
                            this.ScaleArrow.Render();

                            modelMatrix = Matrix4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4.CreateScale(0.2f * posScreen.W) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(90)) * Matrix4.CreateTranslation(half);
                            this.OverlayShader["model"].Set(modelMatrix);
                            this.OverlayShader["u_color"].Set(Color.Red.Vec4());
                            this.ScaleArrow.Render();

                            modelMatrix = Matrix4.CreateTranslation(new Vector3(0, 0, 0.5f)) * Matrix4.CreateScale(0.2f * posScreen.W) * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-90)) * Matrix4.CreateTranslation(half);
                            this.OverlayShader["model"].Set(modelMatrix);
                            this.OverlayShader["u_color"].Set(Color.Green.Vec4());
                            this.ScaleArrow.Render();

                            modelMatrix = Matrix4.CreateTranslation(new Vector3(0.5f, 0.5f, 0f)) * Matrix4.CreateScale(0.2f * posScreen.W) * Matrix4.CreateTranslation(half);
                            this.OverlayShader["model"].Set(modelMatrix);
                            this.OverlayShader["u_color"].Set(Color.Blue.Vec4() * new Vector4(1, 1, 1, 0.75f));
                            this.ScaleSide.Render();

                            modelMatrix = Matrix4.CreateTranslation(new Vector3(0.5f, 0.5f, 0f)) * Matrix4.CreateScale(0.2f * posScreen.W) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(-90)) * Matrix4.CreateTranslation(half);
                            this.OverlayShader["model"].Set(modelMatrix);
                            this.OverlayShader["u_color"].Set(Color.Red.Vec4() * new Vector4(1, 1, 1, 0.75f));
                            this.ScaleSide.Render();

                            modelMatrix = Matrix4.CreateTranslation(new Vector3(0.5f, 0.5f, 0f)) * Matrix4.CreateScale(0.2f * posScreen.W) * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(90)) * Matrix4.CreateTranslation(half);
                            this.OverlayShader["model"].Set(modelMatrix);
                            this.OverlayShader["u_color"].Set(Color.Green.Vec4() * new Vector4(1, 1, 1, 0.75f));
                            this.ScaleSide.Render();

                            Vector3 a = Vector3.Cross(Vector3.UnitZ, -cam.Direction);
                            Quaternion q = new Quaternion(a, 1 + Vector3.Dot(Vector3.UnitZ, -cam.Direction));
                            modelMatrix = Matrix4.CreateScale(0.5f * posScreen.W) * Matrix4.CreateFromQuaternion(q) * Matrix4.CreateTranslation(half);
                            this.OverlayShader.Bind();
                            this.OverlayShader["view"].Set(cam.View);
                            this.OverlayShader["projection"].Set(cam.Projection);
                            this.OverlayShader["model"].Set(modelMatrix);
                            this.OverlayShader["u_color"].Set(Color.White.Vec4());
                            this.RotateCircle.Render();
                        }

                        break;
                    }

                    case EditMode.Rotate:
                    {
                        if (is2d)
                        {
                            Matrix4 modelMatrix = Matrix4.CreateScale(220f * orthozoom) * Matrix4.CreateTranslation(half);
                            this.OverlayShader.Bind();
                            this.OverlayShader["view"].Set(cam.View);
                            this.OverlayShader["projection"].Set(cam.Projection);
                            this.OverlayShader["model"].Set(modelMatrix);
                            this.OverlayShader["u_color"].Set(Color.Blue.Vec4());
                            this.RotateCircle.Render();
                        }
                        else
                        {
                            Matrix4 viewProj = cam.ViewProj;
                            Vector4 posScreen = new Vector4(half, 1.0f) * viewProj;
                            Matrix4 modelMatrix = Matrix4.CreateScale(0.4f * posScreen.W) * Matrix4.CreateTranslation(half);

                            this.OverlayShader.Bind();
                            this.OverlayShader["view"].Set(cam.View);
                            this.OverlayShader["projection"].Set(cam.Projection);
                            this.OverlayShader["model"].Set(modelMatrix);
                            this.OverlayShader["u_color"].Set(Color.Blue.Vec4());
                            this.RotateCircle.Render();

                            modelMatrix = Matrix4.CreateScale(0.4f * posScreen.W) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(90)) * Matrix4.CreateTranslation(half);
                            this.OverlayShader.Bind();
                            this.OverlayShader["view"].Set(cam.View);
                            this.OverlayShader["projection"].Set(cam.Projection);
                            this.OverlayShader["model"].Set(modelMatrix);
                            this.OverlayShader["u_color"].Set(Color.Red.Vec4());
                            this.RotateCircle.Render();

                            modelMatrix = Matrix4.CreateScale(0.4f * posScreen.W) * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(90)) * Matrix4.CreateTranslation(half);
                            this.OverlayShader.Bind();
                            this.OverlayShader["view"].Set(cam.View);
                            this.OverlayShader["projection"].Set(cam.Projection);
                            this.OverlayShader["model"].Set(modelMatrix);
                            this.OverlayShader["u_color"].Set(Color.Green.Vec4());
                            this.RotateCircle.Render();

                            Vector3 a = Vector3.Cross(Vector3.UnitZ, -cam.Direction);
                            Quaternion q = new Quaternion(a, 1 + Vector3.Dot(Vector3.UnitZ, -cam.Direction));

                            modelMatrix = Matrix4.CreateScale(0.5f * posScreen.W) * Matrix4.CreateFromQuaternion(q) * Matrix4.CreateTranslation(half);
                            this.OverlayShader.Bind();
                            this.OverlayShader["view"].Set(cam.View);
                            this.OverlayShader["projection"].Set(cam.Projection);
                            this.OverlayShader["model"].Set(modelMatrix);
                            this.OverlayShader["u_color"].Set(Color.White.Vec4());
                            this.RotateCircle.Render();
                        }

                        break;
                    }
                }

                GL.DepthMask(true);
                GL.Disable(EnableCap.Blend);
                GL.Enable(EnableCap.DepthTest);
                GL.Disable(EnableCap.CullFace);
            }
        }

        private void RenderObjectMouseOver(Map m)
        {
            if (this.ObjectMouseOver != null && Client.Instance.Frontend.Renderer.SelectionManager.BoxSelectCandidates.Count == 0)
            {
                GL.Disable(EnableCap.CullFace);
                MapObject mo = this.ObjectMouseOver;
                AABox cBB = mo.ClientBoundingBox.Scale(mo.Scale);
                Vector3 size = cBB.Size;
                Vector3 cAvg = cBB.Start + ((cBB.End - cBB.Start) / 2);
                Matrix4 modelMatrix = Matrix4.CreateTranslation(cAvg) * Matrix4.CreateFromQuaternion(this.ObjectMouseOver.Rotation) * Matrix4.CreateTranslation(this.ObjectMouseOver.Position);
                ShaderProgram shader = this.HighlightShader;
                Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
                shader.Bind();
                shader["view"].Set(cam.View);
                shader["projection"].Set(cam.Projection);
                shader["model"].Set(modelMatrix);
                shader["u_color"].Set(Color.RoyalBlue.Vec4());
                float mD = Client.Instance.Frontend.UpdatesExisted % 180 / 90.0f;
                mD = mD / 2 % 1 * 2;
                float sineMod = ((MathF.Min(mD, 2 - mD) * 2) - 1) * 0.025f;
                shader["bounds"].Set(size + new Vector3(sineMod));
                this._boxVao.Bind();
                GL.DrawArrays(PrimitiveType.Triangles, 0, 864);
                GL.Enable(EnableCap.CullFace);
            }
        }

        private void RenderLights(Map m)
        {
            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            plr.Clear();
            Guid selfDarkvision = Guid.Empty;
            float dvLuma = 0;
            if (m.EnableDarkvision)
            {
                if (m.DarkvisionData.TryGetValue(Client.Instance.ID, out (Guid, float) kv))
                {
                    if (m.GetObject(kv.Item1, out MapObject mo))
                    {
                        selfDarkvision = mo.ID;
                        dvLuma = kv.Item2;
                    }
                }
            }

            lock (m.Lock)
            {
                foreach (MapObject mo in m.Objects)
                {
                    if (mo == null || mo.MapLayer > 0 || (!mo.LightsEnabled && !mo.ID.Equals(selfDarkvision)))
                    {
                        continue;
                    }

                    AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(mo.AssetID, AssetType.Model, out Asset a);
                    if (status == AssetStatus.Return && (a?.Model?.GLMdl?.glReady ?? false))
                    {
                        Matrix4 modelMatrix = mo.ClientCachedModelMatrix;
                        plr.ProcessScene(modelMatrix, a.Model.GLMdl, mo);

                        if (mo.ID.Equals(selfDarkvision))
                        {
                            plr.AddLightCandidate(new PointLight(
                                mo.Position + (a?.Model.GLMdl?.CombinedBounds.Center ?? Vector3.Zero),
                                Vector3.One, 0, mo, true, false, new VTT.Asset.Glb.GlbLight(Vector4.One, dvLuma, VTT.Asset.Glb.KhrLight.LightTypeEnum.Point)));
                        }
                    }
                }

                plr.DrawLights(m, m.EnableDirectionalShadows && Client.Instance.Settings.EnableDirectionalShadows, cam);
            }
        }

        private void RenderDeferred(Map m, double delta)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;
            this.UniformCommonData(m, delta);
            this.RenderLights(m);

            this.DeferredPipeline.RenderScene(m); 
            GL.ActiveTexture(TextureUnit.Texture0);

            int maxLayer = Client.Instance.IsAdmin ? 2 : 0;
            for (int i = -2; i <= maxLayer; ++i)
            {
                ShaderProgram shader = this.RenderShader;
                shader.Bind();
                if (i <= 0)
                {
                    Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.Uniform(shader);
                }
                else
                {
                    Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.UniformBlank(shader);
                }

                int cLayer = Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer;
                this._passthroughData.Alpha = i > 0 && i > cLayer ? 0.75f - (0.25f * (i - cLayer)) : 1.0f;
                this._passthroughData.GridAlpha = i == -2 && m.GridEnabled ? 1.0f : 0.0f;
                shader["alpha"].Set(this._passthroughData.Alpha);
                shader["grid_alpha"].Set(this._passthroughData.GridAlpha);
                foreach (MapObject mo in m.IterateObjects(i))
                {
                    AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(mo.AssetID, AssetType.Model, out Asset a);
                    if (status == AssetStatus.Return && (a?.Model?.GLMdl?.glReady ?? false))
                    {
                        if (i > 0 || mo.ClientDeferredRejectThisFrame)
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

                            mo.ClientRenderedThisFrame = true;
                            Matrix4 modelMatrix = mo.ClientCachedModelMatrix;
                            GL.Enable(EnableCap.Blend);
                            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                            GL.Enable(EnableCap.SampleAlphaToCoverage);
                            shader = this.RenderShader;
                            this._passthroughData.TintColor = mo.TintColor.Vec4();
                            shader["tint_color"].Set(this._passthroughData.TintColor);
                            bool hadCustomRenderShader = CustomShaderRenderer.Render(mo.ShaderID, m, this._passthroughData, double.NaN, delta, out ShaderProgram customShader);
                            if (hadCustomRenderShader)
                            {
                                if (i <= 0)
                                {
                                    Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.Uniform(customShader);
                                }
                                else
                                {
                                    Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.UniformBlank(customShader);
                                }
                            }

                            a.Model.GLMdl.Render(hadCustomRenderShader ? customShader : shader, modelMatrix, cam.Projection, cam.View, double.NaN);
                            if (hadCustomRenderShader)
                            {
                                this.RenderShader.Bind();
                            }

                            GL.Disable(EnableCap.SampleAlphaToCoverage);
                            GL.Disable(EnableCap.Blend);
                        }

                        if (mo.IsCrossedOut && mo.ClientRenderedThisFrame)
                        {
                            Matrix4 modelMatrix = mo.ClientCachedModelMatrix.ClearRotation();
                            shader = this.OverlayShader;
                            shader.Bind();
                            shader["model"].Set(modelMatrix);
                            shader["u_color"].Set(Color.Red.Vec4());
                            GL.Disable(EnableCap.DepthTest);
                            this.Cross.Render();
                            GL.Enable(EnableCap.DepthTest);
                            shader = this.RenderShader;
                            shader.Bind();
                        }
                    }
                    else
                    {
                        AABox assumedBox = new AABox(-0.5f, -0.5f, -0.5f, 0.5f, 0.5f, 0.5f).Scale(mo.Scale);
                        if (!Client.Instance.Frontend.Renderer.MapRenderer.IsAABoxInFrustrum(assumedBox, mo.Position))
                        {
                            mo.ClientRenderedThisFrame = false;
                            continue;
                        }

                        mo.ClientRenderedThisFrame = true;
                        GL.Disable(EnableCap.CullFace);
                        GL.Enable(EnableCap.DepthTest);
                        GL.DepthFunc(DepthFunction.Lequal);
                        Matrix4 modelMatrix = mo.ClientCachedModelMatrix;
                        shader = this.HighlightShader;
                        shader.Bind();
                        shader["model"].Set(modelMatrix);
                        shader["u_color"].Set(status == AssetStatus.Await ? Color.Blue.Vec4() : Color.Red.Vec4());
                        GL.ActiveTexture(TextureUnit.Texture0);
                        Client.Instance.Frontend.Renderer.White.Bind();
                        this._noAssetVao.Bind();
                        GL.DrawElements(PrimitiveType.Triangles, 12 * 6, DrawElementsType.UnsignedInt, IntPtr.Zero);
                        GL.Enable(EnableCap.CullFace);
                        shader = this.RenderShader;
                        shader.Bind();
                    }

                    if (Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Contains(mo))
                    {
                        GL.Disable(EnableCap.CullFace);
                        AABox cBB = mo.ClientBoundingBox.Scale(mo.Scale);
                        Vector3 size = cBB.Size;
                        Vector3 cAvg = cBB.Start + ((cBB.End - cBB.Start) / 2);
                        Matrix4 modelMatrix = Matrix4.CreateTranslation(cAvg) * Matrix4.CreateFromQuaternion(mo.Rotation) * Matrix4.CreateTranslation(mo.Position);
                        shader = this.HighlightShader;
                        shader.Bind();
                        shader["model"].Set(modelMatrix);
                        shader["u_color"].Set(Color.Orange.Vec4());
                        shader["bounds"].Set(size);
                        this._boxVao.Bind();
                        GL.DrawArrays(PrimitiveType.Triangles, 0, 864);
                        GL.Enable(EnableCap.CullFace);
                        shader = this.RenderShader;
                        shader.Bind();
                    }

                    if (Client.Instance.Frontend.Renderer.SelectionManager.BoxSelectCandidates.Contains(mo))
                    {
                        GL.Disable(EnableCap.CullFace);
                        AABox cBB = mo.ClientBoundingBox.Scale(mo.Scale);
                        Vector3 size = cBB.Size;
                        Vector3 cAvg = cBB.Start + ((cBB.End - cBB.Start) / 2);
                        Matrix4 modelMatrix = Matrix4.CreateTranslation(cAvg) * Matrix4.CreateFromQuaternion(mo.Rotation) * Matrix4.CreateTranslation(mo.Position);
                        shader = this.HighlightShader;
                        shader.Bind();
                        shader["model"].Set(modelMatrix);
                        shader["u_color"].Set(Color.SkyBlue.Vec4());
                        shader["bounds"].Set(size);
                        this._boxVao.Bind();
                        GL.DrawArrays(PrimitiveType.Triangles, 0, 864);
                        GL.Enable(EnableCap.CullFace);
                        shader = this.RenderShader;
                        shader.Bind();
                    }
                }
            }

            GL.ActiveTexture(TextureUnit.Texture0);
        }

        public void RenderHighlightBox(MapObject mo, Color c, float extraScale = 1.0f)
        {
            GL.Disable(EnableCap.CullFace);
            AABox cBB = mo.ClientBoundingBox.Scale(mo.Scale);
            Vector3 size = cBB.Size;
            Vector3 cAvg = cBB.Start + ((cBB.End - cBB.Start) / 2);
            Matrix4 modelMatrix = Matrix4.CreateTranslation(cAvg) * Matrix4.CreateFromQuaternion(mo.Rotation) * Matrix4.CreateTranslation(mo.Position);
            ShaderProgram shader = this.HighlightShader;
            shader.Bind();
            shader["model"].Set(modelMatrix);
            shader["u_color"].Set(c.Vec4());
            shader["bounds"].Set(size * extraScale);
            this._boxVao.Bind();
            GL.DrawArrays(PrimitiveType.Triangles, 0, 864);
            GL.Enable(EnableCap.CullFace);
        }

        public void SetDummyUBO(Camera cam, DirectionalLight sun, Vector4 clearColor, ShaderProgram shader)
        {
            if (shader != null)
            {
                shader["view"].Set(cam.View);
                shader["projection"].Set(cam.Projection);
                shader["frame"].Set((uint)Client.Instance.Frontend.FramesExisted);
                shader["update"].Set((uint)Client.Instance.Frontend.UpdatesExisted);
                shader["camera_position"].Set(cam.Position);
                shader["camera_direction"].Set(cam.Direction);
                shader["dl_direction"].Set(sun.Direction.Normalized());
                shader["dl_color"].Set(sun.Color);
                shader["al_color"].Set(new Vector3(0.03f));
                shader["sun_view"].Set(Matrix4.Identity);
                shader["sun_projection"].Set(Matrix4.Identity);
                shader["sky_color"].Set(clearColor.Xyz);
                shader["grid_color"].Set(Vector4.Zero);
                shader["grid_alpha"].Set(0.0f);
                shader["grid_size"].Set(1.0f);
                shader["cursor_position"].Set(Vector3.Zero);
                shader["dv_data"].Set(Vector4.Zero);
                shader["frame_delta"].Set(0f);
            }
            else
            {
                unsafe
                {
                    this.FrameUBOManager.memory->view = cam.View;
                    this.FrameUBOManager.memory->projection = cam.Projection;
                    this.FrameUBOManager.memory->frame = (uint)Client.Instance.Frontend.FramesExisted;
                    this.FrameUBOManager.memory->update = (uint)Client.Instance.Frontend.UpdatesExisted;
                    this.FrameUBOManager.memory->camera_position.Xyz = cam.Position;
                    this.FrameUBOManager.memory->camera_direction.Xyz = cam.Direction;
                    this.FrameUBOManager.memory->dl_direction.Xyz = sun.Direction.Normalized();
                    this.FrameUBOManager.memory->dl_color.Xyz = sun.Color;
                    this.FrameUBOManager.memory->al_color.Xyz = new Vector3(0.03f);
                    this.FrameUBOManager.memory->sun_view = Matrix4.Identity;
                    this.FrameUBOManager.memory->sun_projection = Matrix4.Identity;
                    this.FrameUBOManager.memory->sky_color.Xyz = clearColor.Xyz;
                    this.FrameUBOManager.memory->grid_color = Vector4.Zero;
                    this.FrameUBOManager.memory->grid_size = 1.0f;
                    this.FrameUBOManager.memory->cursor_position.Xyz = Vector3.Zero;
                    this.FrameUBOManager.memory->dv_data = Vector4.Zero;
                    this.FrameUBOManager.memory->frame_delta = 0f;
                }

                this.FrameUBOManager.Upload();
            }
        }

        private void UpdateUBO(Map m, double delta)
        {
            if (Client.Instance.Settings.UseUBO)
            {
                Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;

                unsafe
                {
                    this.FrameUBOManager.memory->view = cam.View;
                    this.FrameUBOManager.memory->projection = cam.Projection;
                    this.FrameUBOManager.memory->frame = (uint)Client.Instance.Frontend.FramesExisted;
                    this.FrameUBOManager.memory->update = (uint)Client.Instance.Frontend.UpdatesExisted;
                    this.FrameUBOManager.memory->camera_position.Xyz = cam.Position;
                    this.FrameUBOManager.memory->camera_direction.Xyz = cam.Direction;
                    this.FrameUBOManager.memory->dl_direction.Xyz = this._cachedSunDir;
                    this.FrameUBOManager.memory->dl_color.Xyz = this._cachedSunColor.Vec3() * m.SunIntensity;
                    this.FrameUBOManager.memory->al_color.Xyz = this._cachedAmbientColor * m.AmbietIntensity;
                    this.FrameUBOManager.memory->sun_view = this.DirectionalLightRenderer.SunView;
                    this.FrameUBOManager.memory->sun_projection = this.DirectionalLightRenderer.SunProjection;
                    this.FrameUBOManager.memory->sky_color.Xyz = this._cachedSkyColor.Vec3();
                    this.FrameUBOManager.memory->grid_color = m.GridColor.Vec4();
                    this.FrameUBOManager.memory->grid_size = m.GridSize;
                    this.FrameUBOManager.memory->cursor_position.Xyz = Client.Instance.Frontend.Renderer.RulerRenderer.TerrainHit ?? Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld ?? Vector3.Zero;
                    this.FrameUBOManager.memory->dv_data = Vector4.Zero;
                    this.FrameUBOManager.memory->frame_delta = (float)delta;
                    if (m.EnableDarkvision)
                    {
                        if (m.DarkvisionData.TryGetValue(Client.Instance.ID, out (Guid, float) kv))
                        {
                            if (m.GetObject(kv.Item1, out MapObject mo))
                            {
                                this.FrameUBOManager.memory->dv_data = new Vector4(mo.Position, kv.Item2 / m.GridUnit);
                            }
                        }
                    }
                }

                this.FrameUBOManager.Upload();
            }
        }

        public void UniformMainShaderData(Map m, ShaderProgram shader, double delta)
        {
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;
            if (!Client.Instance.Settings.UseUBO) // If UBOs are used all uniforms are in the UBO
            {
                shader.Bind();

                shader["view"].Set(cam.View);
                shader["projection"].Set(cam.Projection);
                shader["frame"].Set((uint)Client.Instance.Frontend.FramesExisted);
                shader["update"].Set((uint)Client.Instance.Frontend.UpdatesExisted);
                shader["camera_position"].Set(cam.Position);
                shader["camera_direction"].Set(cam.Direction);
                shader["dl_direction"].Set(this._cachedSunDir);
                shader["dl_color"].Set(this._cachedSunColor.Vec3() * m.SunIntensity);
                shader["al_color"].Set(this._cachedAmbientColor * m.AmbietIntensity);
                shader["sun_view"].Set(this.DirectionalLightRenderer.SunView);
                shader["sun_projection"].Set(this.DirectionalLightRenderer.SunProjection);
                shader["sky_color"].Set(this._cachedSkyColor.Vec3());
                shader["grid_color"].Set(m.GridColor.Vec4());
                shader["grid_size"].Set(m.GridSize);
                shader["cursor_position"].Set(Client.Instance.Frontend.Renderer.RulerRenderer.TerrainHit ?? Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld ?? Vector3.Zero);
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
            plr.UniformLights(shader);
        }

        private void UniformCommonData(Map m, double delta)
        {
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;

            ShaderProgram shader = this.RenderShader;
            shader.Bind();
            if (!Client.Instance.Settings.UseUBO)
            {
                shader["view"].Set(cam.View);
                shader["projection"].Set(cam.Projection);
                shader["frame"].Set((uint)Client.Instance.Frontend.FramesExisted);
                shader["update"].Set((uint)Client.Instance.Frontend.UpdatesExisted);
                shader["camera_position"].Set(cam.Position);
                shader["camera_direction"].Set(cam.Direction);
                shader["dl_direction"].Set(this._cachedSunDir);
                shader["dl_color"].Set(this._cachedSunColor.Vec3() * m.SunIntensity);
                shader["al_color"].Set(this._cachedAmbientColor * m.AmbietIntensity);
                shader["sun_view"].Set(this.DirectionalLightRenderer.SunView);
                shader["sun_projection"].Set(this.DirectionalLightRenderer.SunProjection);
                shader["sky_color"].Set(this._cachedSkyColor.Vec3());
                shader["grid_color"].Set(m.GridColor.Vec4());
                shader["grid_size"].Set(m.GridSize);
                shader["cursor_position"].Set(Client.Instance.Frontend.Renderer.RulerRenderer.TerrainHit ?? Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld ?? Vector3.Zero);
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
           
            GL.ActiveTexture(TextureUnit.Texture14);
            if (m.EnableShadows && Client.Instance.Settings.EnableSunShadows)
            {
                this.DirectionalLightRenderer.DepthTexture.Bind();
            }
            else
            {
                Client.Instance.Frontend.Renderer.White.Bind();
            }

            GL.ActiveTexture(TextureUnit.Texture13);
            plr.DepthMap.Bind();

            GL.ActiveTexture(TextureUnit.Texture0);
            plr.UniformLights(shader);

            shader = this.HighlightShader;
            shader.Bind();
            shader["view"].Set(cam.View);
            shader["projection"].Set(cam.Projection);
        }

        private void RenderForward(Map m, double delta)
        {
            GL.Enable(EnableCap.Multisample);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            int maxLayer = Client.Instance.IsAdmin ? 2 : 0;
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;
            this.RenderLights(m);
            this.UniformCommonData(m, delta);

            for (int i = -2; i <= maxLayer; ++i)
            {
                ShaderProgram shader = this.RenderShader;
                shader.Bind();
                if (i <= 0)
                {
                    Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.Uniform(shader);
                }
                else
                {
                    Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.UniformBlank(shader);
                }

                int cLayer = Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer; 
                this._passthroughData.Alpha = i > 0 && i > cLayer ? 0.75f - (0.25f * (i - cLayer)) : 1.0f;
                this._passthroughData.GridAlpha = i == -2 && m.GridEnabled ? 1.0f : 0.0f;
                shader["alpha"].Set(this._passthroughData.Alpha);
                shader["grid_alpha"].Set(this._passthroughData.GridAlpha);

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

                        mo.ClientRenderedThisFrame = true;
                        Matrix4 modelMatrix = mo.ClientCachedModelMatrix;
                        bool transparent = a.Model.GLMdl.HasTransparency || mo.TintColor.Alpha() < 1.0f - float.Epsilon;
                        if (i > cLayer || transparent)
                        {
                            GL.Enable(EnableCap.Blend);
                            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                            GL.Enable(EnableCap.SampleAlphaToCoverage);
                        }

                        shader = this.RenderShader;
                        this._passthroughData.TintColor = mo.TintColor.Vec4();
                        shader["tint_color"].Set(this._passthroughData.TintColor);
                        bool hadCustomRenderShader = CustomShaderRenderer.Render(mo.ShaderID, m, this._passthroughData, double.NaN, delta, out ShaderProgram customShader);
                        if (hadCustomRenderShader)
                        {
                            if (i <= 0)
                            {
                                Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.Uniform(customShader);
                            }
                            else
                            {
                                Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer.UniformBlank(customShader);
                            }
                        }

                        a.Model.GLMdl.Render(hadCustomRenderShader ? customShader : shader, modelMatrix, cam.Projection, cam.View, double.NaN);
                        if (hadCustomRenderShader)
                        {
                            this.RenderShader.Bind();
                        }

                        if (i > cLayer || transparent)
                        {
                            GL.Disable(EnableCap.Blend);
                            GL.Disable(EnableCap.SampleAlphaToCoverage);
                        }

                        if (mo.IsCrossedOut)
                        {
                            modelMatrix = mo.ClientCachedModelMatrix.ClearRotation();
                            shader = this.OverlayShader;
                            shader.Bind();
                            shader["model"].Set(modelMatrix);
                            shader["u_color"].Set(Color.Red.Vec4());
                            GL.Disable(EnableCap.DepthTest);
                            this.Cross.Render();
                            GL.Enable(EnableCap.DepthTest);
                            shader = this.RenderShader;
                            shader.Bind();
                        }
                    }
                    else
                    {
                        AABox assumedBox = new AABox(-0.5f, -0.5f, -0.5f, 0.5f, 0.5f, 0.5f).Scale(mo.Scale);
                        if (!Client.Instance.Frontend.Renderer.MapRenderer.IsAABoxInFrustrum(assumedBox, mo.Position))
                        {
                            mo.ClientRenderedThisFrame = false;
                            continue;
                        }

                        mo.ClientRenderedThisFrame = true;
                        GL.Disable(EnableCap.CullFace);
                        GL.Enable(EnableCap.DepthTest);
                        GL.DepthFunc(DepthFunction.Lequal);
                        Matrix4 modelMatrix = mo.ClientCachedModelMatrix;
                        shader = this.HighlightShader;
                        shader.Bind();
                        shader["model"].Set(modelMatrix);
                        shader["u_color"].Set(status == AssetStatus.Await ? Color.Blue.Vec4() : Color.Red.Vec4());
                        GL.ActiveTexture(TextureUnit.Texture0);
                        Client.Instance.Frontend.Renderer.White.Bind();
                        this._noAssetVao.Bind();
                        GL.DrawElements(PrimitiveType.Triangles, 12 * 6, DrawElementsType.UnsignedInt, IntPtr.Zero);
                        GL.Enable(EnableCap.CullFace);
                        shader = this.RenderShader;
                        shader.Bind();
                    }

                    if (Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Contains(mo))
                    {
                        GL.Disable(EnableCap.CullFace);
                        AABox cBB = mo.ClientBoundingBox.Scale(mo.Scale);
                        Vector3 size = cBB.Size;
                        Vector3 cAvg = cBB.Start + ((cBB.End - cBB.Start) / 2);
                        Matrix4 modelMatrix = Matrix4.CreateTranslation(cAvg) * Matrix4.CreateFromQuaternion(mo.Rotation) * Matrix4.CreateTranslation(mo.Position);
                        shader = this.HighlightShader;
                        shader.Bind();
                        shader["model"].Set(modelMatrix);
                        shader["u_color"].Set(Color.Orange.Vec4());
                        shader["bounds"].Set(size);
                        this._boxVao.Bind();
                        GL.DrawArrays(PrimitiveType.Triangles, 0, 864);
                        GL.Enable(EnableCap.CullFace);
                        shader = this.RenderShader;
                        shader.Bind();
                    }

                    if (Client.Instance.Frontend.Renderer.SelectionManager.BoxSelectCandidates.Contains(mo))
                    {
                        GL.Disable(EnableCap.CullFace);
                        AABox cBB = mo.ClientBoundingBox.Scale(mo.Scale);
                        Vector3 size = cBB.Size;
                        Vector3 cAvg = cBB.Start + ((cBB.End - cBB.Start) / 2);
                        Matrix4 modelMatrix = Matrix4.CreateTranslation(cAvg) * Matrix4.CreateFromQuaternion(mo.Rotation) * Matrix4.CreateTranslation(mo.Position);
                        shader = this.HighlightShader;
                        shader.Bind();
                        shader["model"].Set(modelMatrix);
                        shader["u_color"].Set(Color.SkyBlue.Vec4());
                        shader["bounds"].Set(size);
                        this._boxVao.Bind();
                        GL.DrawArrays(PrimitiveType.Triangles, 0, 864);
                        GL.Enable(EnableCap.CullFace);
                        shader = this.RenderShader;
                        shader.Bind();
                    }
                }
            }

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.Disable(EnableCap.Multisample);
        }

        private readonly List<MapObject> _auraCollection = new List<MapObject>();
        private readonly List<(float, Color)> _auraL = new List<(float, Color)>();
        private void RenderAuras(Map m)
        {
            this._auraCollection.Clear();
            foreach (MapObject mo in m.IterateObjects(null))
            {
                if (mo.Auras.Count > 0 && (mo.MapLayer <= 0 || Client.Instance.IsAdmin))
                {
                    this._auraCollection.Add(mo);
                }
            }

            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            this._auraCollection.Sort((l, r) => (r.Position - cam.Position).LengthSquared.CompareTo((l.Position - cam.Position).LengthSquared));
            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.SampleAlphaToCoverage);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            ShaderProgram shader = this.OverlayShader;
            shader.Bind();
            shader["view"].Set(cam.View);
            shader["projection"].Set(cam.Projection);
            foreach (MapObject mo in this._auraCollection)
            {
                lock (mo.Lock)
                {
                    this._auraL.Clear();
                    this._auraL.AddRange(mo.Auras);
                }

                this._auraL.Sort((l, r) => l.Item1.CompareTo(r.Item1));
                foreach ((float, Color) aData in this._auraL)
                {
                    Matrix4 model = Matrix4.CreateScale(aData.Item1 * 2.0f / m.GridUnit) * Matrix4.CreateTranslation(mo.Position);
                    shader["model"].Set(model);
                    shader["u_color"].Set(aData.Item2.Vec4() * new Vector4(1, 1, 1, 0.5f));
                    GL.CullFace(CullFaceMode.Front);
                    Client.Instance.Frontend.Renderer.RulerRenderer.ModelSphere.Render();
                    GL.CullFace(CullFaceMode.Back);
                    Client.Instance.Frontend.Renderer.RulerRenderer.ModelSphere.Render();
                }
            }

            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.SampleAlphaToCoverage);
        }
    }

    public enum EditMode
    {
        Select,
        Translate,
        Rotate,
        Scale,
        FOW,
        Measure
    }

    [StructLayout(LayoutKind.Explicit, Size = 420, Pack = 0)]
    public unsafe struct FrameUBO
    {
        [FieldOffset(0)] public Matrix4 view;
        [FieldOffset(64)] public Matrix4 projection;
        [FieldOffset(128)] public Matrix4 sun_view;
        [FieldOffset(192)] public Matrix4 sun_projection;
        [FieldOffset(256)] public Vector4 camera_position;
        [FieldOffset(272)] public Vector4 camera_direction;
        [FieldOffset(288)] public Vector4 dl_direction;
        [FieldOffset(304)] public Vector4 dl_color;
        [FieldOffset(320)] public Vector4 al_color;
        [FieldOffset(336)] public Vector4 sky_color;
        [FieldOffset(352)] public Vector4 cursor_position;
        [FieldOffset(368)] public Vector4 grid_color;
        [FieldOffset(384)] public Vector4 dv_data;
        [FieldOffset(400)] public uint frame;
        [FieldOffset(404)] public uint update;
        [FieldOffset(408)] public float grid_size;
        [FieldOffset(412)] public float frame_delta;
        [FieldOffset(416)] public int _padding;
    }

    public unsafe class FrameUBOManager
    {
        public FrameUBO* memory;

        private readonly GPUBuffer _ubo;

        public FrameUBOManager()
        {
            this.memory = (FrameUBO*)Marshal.AllocHGlobal(sizeof(FrameUBO));
            this._ubo = new GPUBuffer(BufferTarget.UniformBuffer, BufferUsageHint.StreamDraw);
            this._ubo.Bind();
            this._ubo.SetData(IntPtr.Zero, 420);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 1, this._ubo);
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal((IntPtr)this.memory);
            this._ubo.Dispose();
        }

        public unsafe void Upload()
        {
            this._ubo.Bind();
            this._ubo.SetSubData((IntPtr)this.memory, 420, 0);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
        }
    }
}
