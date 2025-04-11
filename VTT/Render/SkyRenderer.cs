namespace VTT.Render
{
    using System.Numerics;
    using SixLabors.ImageSharp;
    using System;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;
    using VTT.GL.Bindings;
    using VTT.Control;
    using VTT.Asset;
    using VTT.Asset.Glb;
    using SixLabors.ImageSharp.PixelFormats;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Data;

    public class SkyRenderer
    {
        private VertexArray _vao;
        private GPUBuffer _vbo;

        public ShaderProgram SkyShader { get; set; }
        public Skybox SkyboxRenderer { get; set; }

        public Gradient<Vector3> SkyGradient { get; set; } = new Gradient<Vector3>
        {
            [0] = Color.ParseHex("003469").Vec3() / 5.0f,
            [1] = Color.ParseHex("003469").Vec3() / 5.0f,
            [2] = Color.ParseHex("003469").Vec3() / 5.0f,
            [3] = Color.ParseHex("003e6b").Vec3() / 5.0f,
            [4] = Color.ParseHex("003e6b").Vec3() / 3.0f,
            [5] = Color.ParseHex("045f88").Vec3() / 1.0f,
            [5.25f] = Color.ParseHex("67cac7").Vec3(),
            [5.5f] = Color.ParseHex("ece16a").Vec3(),
            [5.75f] = Color.ParseHex("e0ebbe").Vec3(),
            [6] = Color.ParseHex("ece16a").Vec3(),
            [6.5f] = Color.ParseHex("e0ebbe").Vec3(),
            [7] = Color.ParseHex("67cac7").Vec3(),
            [8] = Color.ParseHex("119bba").Vec3(),

            [15] = Color.ParseHex("119bba").Vec3(),
            [16.5f] = Color.ParseHex("1097b6").Vec3(),
            [17.4f] = Color.ParseHex("e0ebbe").Vec3(),
            [17.5f] = Color.ParseHex("ffb26e").Vec3(),
            [17.75f] = Color.ParseHex("ffb26e").Vec3(),
            [17.8f] = Color.ParseHex("feae5b").Vec3(),
            [17.9f] = Color.ParseHex("fea85a").Vec3(),
            [18] = Color.ParseHex("f38e4b").Vec3(),
            [18.25f] = Color.ParseHex("f1717a").Vec3(),
            [18.5f] = Color.ParseHex("d0608c").Vec3(),
            [18.75f] = Color.ParseHex("673184").Vec3(),
            [19f] = Color.ParseHex("3e1d7a").Vec3(),

            [20f] = Color.ParseHex("2a166c").Vec3(),
            [22f] = Color.ParseHex("040e40").Vec3() / 3.0f,
            [23f] = Color.ParseHex("040c3d").Vec3() / 5.0f,
            [24f] = Color.ParseHex("012356").Vec3() / 5.0f
        };

        public Gradient<Vector3> AmbientGradient { get; set; } = new Gradient<Vector3>
        {
            [0] = Color.ParseHex("003469").Vec3() / 30.0f,
            [1] = Color.ParseHex("003469").Vec3() / 30.0f,
            [2] = Color.ParseHex("003469").Vec3() / 30.0f,
            [3] = Color.ParseHex("003e6b").Vec3() / 30.0f,
            [4] = Color.ParseHex("003e6b").Vec3() / 30.0f,
            [5] = Color.ParseHex("045f88").Vec3() / 50.0f,
            [5.25f] = Color.ParseHex("67cac7").Vec3() / 50.0f,
            [5.5f] = Color.ParseHex("ece16a").Vec3() / 50.0f,
            [5.75f] = Color.ParseHex("e0ebbe").Vec3() / 50.0f,
            [6] = Color.ParseHex("ece16a").Vec3() / 50.0f,
            [6.5f] = Color.ParseHex("e0ebbe").Vec3() / 50.0f,
            [7] = Color.ParseHex("67cac7").Vec3() / 50.0f,
            [8] = Color.ParseHex("ffffff").Vec3() / 50.0f,

            [15] = Color.ParseHex("ffffff").Vec3() / 50.0f,
            [16.5f] = Color.ParseHex("1097b6").Vec3() / 50.0f,
            [17.4f] = Color.ParseHex("e0ebbe").Vec3() / 50.0f,
            [17.5f] = Color.ParseHex("ffb26e").Vec3() / 50.0f,
            [17.75f] = Color.ParseHex("ffb26e").Vec3() / 50.0f,
            [17.8f] = Color.ParseHex("feae5b").Vec3() / 50.0f,
            [17.9f] = Color.ParseHex("fea85a").Vec3() / 50.0f,
            [18] = Color.ParseHex("f38e4b").Vec3() / 50.0f,
            [18.25f] = Color.ParseHex("f1717a").Vec3() / 50.0f,
            [18.5f] = Color.ParseHex("d0608c").Vec3() / 50.0f,
            [18.75f] = Color.ParseHex("673184").Vec3() / 50.0f,
            [19f] = Color.ParseHex("3e1d7a").Vec3() / 50.0f,

            [20f] = Color.ParseHex("2a166c").Vec3() / 50.0f,
            [22f] = Color.ParseHex("040e40").Vec3() / 30.0f,
            [23f] = Color.ParseHex("040c3d").Vec3() / 30.0f,
            [24f] = Color.ParseHex("012356").Vec3() / 30.0f
        };

        public Gradient<Vector3> LightGrad { get; set; } = new Gradient<Vector3>
        {
            //[0] = Color.ParseHex("040c3d").Vec3(),
            //[5] = Color.ParseHex("040c3d").Vec3(),
            [0] = Color.ParseHex("000000").Vec3(),
            [5] = Color.ParseHex("000000").Vec3(),
            [5.25f] = Color.ParseHex("67cac7").Vec3(),
            [5.5f] = Color.ParseHex("ece16a").Vec3(),
            [5.75f] = Color.ParseHex("e0ebbe").Vec3(),
            [6] = Color.ParseHex("ece16a").Vec3(),
            [6.5f] = Color.ParseHex("e0ebbe").Vec3(),
            [7] = Color.ParseHex("ffffff").Vec3(),
            [8] = Color.ParseHex("ffffff").Vec3(),

            [15] = Color.ParseHex("ffffff").Vec3(),
            [16] = Color.ParseHex("ffffff").Vec3(),
            [17] = Color.ParseHex("ffffff").Vec3(),
            [17.4f] = Color.ParseHex("e0ebbe").Vec3(),
            [17.5f] = Color.ParseHex("ffb26e").Vec3(),
            [17.75f] = Color.ParseHex("ffb26e").Vec3(),
            [17.8f] = Color.ParseHex("feae5b").Vec3(),
            [17.9f] = Color.ParseHex("fea85a").Vec3(),
            [18] = Color.ParseHex("f38e4b").Vec3(),
            [18.25f] = Color.ParseHex("f1717a").Vec3(),
            [18.5f] = Color.ParseHex("d0608c").Vec3(),
            [18.75f] = Color.ParseHex("673184").Vec3(),
            [19f] = Color.ParseHex("3e1d7a").Vec3(),

            //[20f] = Color.ParseHex("2a166c").Vec3(),
            //[22f] = Color.ParseHex("040e40").Vec3(),
            //[23f] = Color.ParseHex("040c3d").Vec3(),
            //[24f] = Color.ParseHex("012356").Vec3()

            [20f] = Color.ParseHex("000000").Vec3(), // No sun at night
            [24f] = Color.ParseHex("000000").Vec3()
        };

        public void Create()
        {
            this.SkyShader = OpenGLUtil.LoadShader("sky", ShaderType.Vertex, ShaderType.Fragment);
            this._vao = new VertexArray();
            this._vbo = new GPUBuffer(BufferTarget.Array);
            this._vao.Bind();
            this._vbo.Bind();
            this._vbo.SetData(new float[] {
                -1f, -1f, 0f,
                1f, -1f, 0f,
                -1f, 1f, 0f,
                1f, 1f, 0f,
            });

            this._vao.Reset();
            this._vao.SetVertexSize<float>(3);
            this._vao.PushElement(ElementType.Vec3);
            this.SkyboxRenderer = new Skybox();
            this.SkyboxRenderer.Init();
        }

        public Vector3 GetCurrentSunDirection() => Client.Instance.CurrentMap.SunEnabled ? this.GetSunDirection(Client.Instance.CurrentMap?.SunYaw ?? 1, Client.Instance.CurrentMap?.SunPitch ?? 1) : -Vector3.UnitZ;
        public Vector3 GetCurrentSunUp() => this.GetSunUp(Client.Instance.CurrentMap?.SunYaw ?? 1, Client.Instance.CurrentMap?.SunPitch ?? 1);

        public Vector3 GetSunDirection(float yaw, float pitch)
        {
            Vector4 vec = -Vector4.UnitZ;
            Quaternion q = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, yaw);
            Quaternion q1 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, pitch);
            return Vector4.Transform(Vector4.Transform(vec, q1), q).Xyz().Normalized();
        }

        public Vector3 GetSunUp(float yaw, float pitch)
        {
            Vector4 vec = -Vector4.UnitY;
            Quaternion q = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, yaw);
            Quaternion q1 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, pitch);
            return Vector4.Transform(Vector4.Transform(vec, q1), q).Xyz().Normalized();
        }

        public void Render(Map map, double time)
        {
            if (map == null)
            {
                return;
            }

            this.SkyboxRenderer.Render(map, time);

            if (map.Is2D)
            {
                return;
            }

            float pitch = map.SunPitch;
            if (!map.SunEnabled || pitch < -1.6493362 || pitch > 1.6580629)
            {
                return;
            }

            GL.Enable(Capability.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            Vector3 sunDir = this.GetCurrentSunDirection();
            this.SkyShader.Bind();
            this.SkyShader["projection"].Set(cam.Projection);
            this.SkyShader["view"].Set(cam.View);
            Vector3 a = Vector3.Cross(Vector3.UnitZ, -cam.Direction);
            Quaternion q = new Quaternion(a, 1 + Vector3.Dot(Vector3.UnitZ, -cam.Direction)).Normalized();
            Matrix4x4 model = Matrix4x4.CreateScale(8) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(cam.Position - (sunDir * 99));
            this.SkyShader["model"].Set(model);
            this._vao.Bind();

            GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
            GL.Disable(Capability.Blend);
        }

        public float GetDayProgress(Map map) => map == null ? 0 : !map.SunEnabled ? 12 : (float)(map.SunPitch + MathF.PI) / MathF.PI * 12;

        public Color GetSkyColor()
        {
            Map map = Client.Instance.CurrentMap;
            if (map == null)
            {
                return Color.Black;
            }

            if (!map.SunEnabled)
            {
                return Extensions.FromVec3(map.DaySkyboxColors.GetColor(map, 12));
            }

            float dayProgress = this.GetDayProgress(map);
            const float cutoutPointNightToDayStart = 4.8f;
            const float cutoutPointNightToDayEnd = 7.6f;
            const float cutoutPointDayToNightStart = 16f;
            const float cutoutPointDayToNightEnd = 21f;
            float dayNightFactor =
                dayProgress < cutoutPointNightToDayStart ? 1.0f
                : dayProgress < cutoutPointNightToDayEnd ? 1.0f - (dayProgress - cutoutPointNightToDayStart) / (cutoutPointNightToDayEnd - cutoutPointNightToDayStart)
                : dayProgress > cutoutPointDayToNightStart ? (dayProgress - cutoutPointDayToNightStart) / (cutoutPointDayToNightEnd - cutoutPointDayToNightStart)
                : dayProgress > cutoutPointDayToNightEnd ? 1.0f : 0.0f;
            return Extensions.FromVec3(Vector3.Lerp(map.DaySkyboxColors.GetColor(map, dayProgress), map.NightSkyboxColors.GetColor(map, dayProgress), dayNightFactor));

            // Legacy
            // float pitch = map.SunPitch + MathF.PI;
            // float idx = pitch / MathF.PI * 12;
            // return Extensions.FromVec3(this.SkyGradient.Interpolate(idx, GradientInterpolators.LerpVec3));
        }

        public Color GetSunColor()
        {
            Map map = Client.Instance.CurrentMap;
            if (map == null)
            {
                return Color.White;
            }

            if (!map.SunEnabled)
            {
                return map.SunColor;
            }

            float pitch = map.SunPitch + MathF.PI;
            float idx = pitch / MathF.PI * 12;
            return Extensions.FromVec3(this.LightGrad.Interpolate(idx, GradientInterpolators.LerpVec3) / 4.0f);
        }

        public Color GetAmbientColor()
        {
            Map map = Client.Instance.CurrentMap;
            if (map == null)
            {
                return Color.White;
            }

            if (!map.SunEnabled)
            {
                return map.AmbientColor;
            }

            float pitch = map.SunPitch + MathF.PI;
            float idx = pitch / MathF.PI * 12;
            return Extensions.FromVec3(this.AmbientGradient.Interpolate(idx, GradientInterpolators.CubVec3));
        }

        public class Skybox
        {
            private Guid _dayAssetID = Guid.Empty;
            private Guid _nightAssetID = Guid.Empty;

            private Texture _skyboxArray;
            private TextureAnimation _skyboxDayAnimation;
            private TextureAnimation _skyboxNightAnimation;
            private Texture _skyboxBlank;
            private ShaderProgram _skyboxShader;

            public Texture SkyboxTextureArray => this._skyboxArray ?? this._skyboxBlank;
            public TextureAnimation DayAnimation => this._skyboxDayAnimation;
            public TextureAnimation NightAnimation => this._skyboxNightAnimation;

            private GPUBuffer _vbo;
            private VertexArray _vao;

            private bool _waitingOnAsync;

            public Skybox()
            {
            }

            public void Init()
            {
                this._skyboxBlank = new Texture(TextureTarget.Texture2DArray);
                this._skyboxBlank.Bind();
                this._skyboxBlank.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
                this._skyboxBlank.SetFilterParameters(FilterParam.Nearest, FilterParam.Nearest);
                GL.TexImage3D(TextureTarget.Texture2DArray, 0, SizedInternalFormat.Rgba8, 4, 3, 2, PixelDataFormat.Rgba, PixelDataType.Byte, 0);
                using Image<Rgba32> img = new Image<Rgba32>(4, 3, new Rgba32(255, 255, 255, 255));
                unsafe
                {
                    img.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> mem);
                    System.Buffers.MemoryHandle hnd = mem.Pin();
                    GL.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, 0, 4, 3, 1, PixelDataFormat.Rgba, PixelDataType.Byte, (nint)hnd.Pointer);
                    GL.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, 1, 4, 3, 1, PixelDataFormat.Rgba, PixelDataType.Byte, (nint)hnd.Pointer);
                    hnd.Dispose();
                }

                this._vao = new VertexArray();
                this._vbo = new GPUBuffer(BufferTarget.Array);
                this._vao.Bind();
                this._vbo.Bind();
                this._vao.SetVertexSize(sizeof(float) * 3);
                this._vao.PushElement(ElementType.Vec3);
                this._vbo.SetData(new float[] {
                    -1.0f,  1.0f, -1.0f,
                    -1.0f, -1.0f, -1.0f,
                     1.0f, -1.0f, -1.0f,
                     1.0f, -1.0f, -1.0f,
                     1.0f,  1.0f, -1.0f,
                    -1.0f,  1.0f, -1.0f,

                    -1.0f, -1.0f,  1.0f,
                    -1.0f, -1.0f, -1.0f,
                    -1.0f,  1.0f, -1.0f,
                    -1.0f,  1.0f, -1.0f,
                    -1.0f,  1.0f,  1.0f,
                    -1.0f, -1.0f,  1.0f,

                     1.0f, -1.0f, -1.0f,
                     1.0f, -1.0f,  1.0f,
                     1.0f,  1.0f,  1.0f,
                     1.0f,  1.0f,  1.0f,
                     1.0f,  1.0f, -1.0f,
                     1.0f, -1.0f, -1.0f,

                    -1.0f, -1.0f,  1.0f,
                    -1.0f,  1.0f,  1.0f,
                     1.0f,  1.0f,  1.0f,
                     1.0f,  1.0f,  1.0f,
                     1.0f, -1.0f,  1.0f,
                    -1.0f, -1.0f,  1.0f,

                    -1.0f,  1.0f, -1.0f,
                     1.0f,  1.0f, -1.0f,
                     1.0f,  1.0f,  1.0f,
                     1.0f,  1.0f,  1.0f,
                    -1.0f,  1.0f,  1.0f,
                    -1.0f,  1.0f, -1.0f,

                    -1.0f, -1.0f, -1.0f,
                    -1.0f, -1.0f,  1.0f,
                     1.0f, -1.0f, -1.0f,
                     1.0f, -1.0f, -1.0f,
                    -1.0f, -1.0f,  1.0f,
                     1.0f, -1.0f,  1.0f
                });

                this._skyboxShader = OpenGLUtil.LoadShader("skybox", ShaderType.Vertex, ShaderType.Fragment);
                this._skyboxShader.Bind();
                this._skyboxShader["tex_skybox"].Set(6);
            }

            public void Clear()
            {
                this._dayAssetID = Guid.Empty;
                this._nightAssetID = Guid.Empty;
                this._waitingOnAsync = false;
            }

            public void Render(Map m, double dt)
            {
                this.ValidateSkyboxTextures(m);
                Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
                GL.Enable(Capability.DepthTest);
                GL.DepthFunction(ComparisonMode.LessOrEqual);
                GL.DepthMask(false);

                this._skyboxShader.Bind();
                this.UniformShader(this._skyboxShader, m);
                this._skyboxShader["projection"].Set(m.Is2D ? Matrix4x4.CreatePerspectiveFieldOfView(Client.Instance.Settings.FOV * MathF.PI / 180.0f, (float)Client.Instance.Frontend.Width / Client.Instance.Frontend.Height, 0.01f, 100f) : cam.Projection);
                this._skyboxShader["view"].Set(cam.View.ClearTranslation());

                this._vao.Bind();
                GL.DrawArrays(PrimitiveType.Triangles, 0, 36);

                GL.DepthMask(true);
                GL.DepthFunction(ComparisonMode.Less);
                GL.Disable(Capability.DepthTest);
            }

            public void UniformShader(ShaderProgram shader, Map m)
            {
                float dayProgress = Client.Instance.Frontend.Renderer.SkyRenderer.GetDayProgress(m);
                const float cutoutPointNightToDayStart = 4.8f;
                const float cutoutPointNightToDayEnd = 7.6f;
                const float cutoutPointDayToNightStart = 16f;
                const float cutoutPointDayToNightEnd = 21f;
                float dayNightFactor =
                    dayProgress < cutoutPointNightToDayStart ? 1.0f
                    : dayProgress < cutoutPointNightToDayEnd ? 1.0f - (dayProgress - cutoutPointNightToDayStart) / (cutoutPointNightToDayEnd - cutoutPointNightToDayStart)
                    : dayProgress > cutoutPointDayToNightStart ? (dayProgress - cutoutPointDayToNightStart) / (cutoutPointDayToNightEnd - cutoutPointDayToNightStart)
                    : dayProgress > cutoutPointDayToNightEnd ? 1.0f : 0.0f;

                shader["animation_day"].Set(this.DayAnimation?.FindFrameForIndex(double.NaN).LocationUniform ?? new Vector4(0, 0, 1, 1));
                shader["animation_night"].Set(this.NightAnimation?.FindFrameForIndex(double.NaN).LocationUniform ?? new Vector4(0, 0, 1, 1));
                shader["daynight_blend"].Set(dayNightFactor);
                shader["day_color"].Set(m.DaySkyboxColors.GetColor(m, dayProgress));
                shader["night_color"].Set(m.NightSkyboxColors.GetColor(m, dayProgress));
                GL.ActiveTexture(6);
                this.SkyboxTextureArray.Bind();
                GL.ActiveTexture(0);
            }

            public void UniformBlank(ShaderProgram shader, Vector3 color)
            {
                shader["animation_day"].Set(new Vector4(0, 0, 1, 1));
                shader["animation_night"].Set(new Vector4(0, 0, 1, 1));
                shader["daynight_blend"].Set(0f);
                shader["day_color"].Set(color);
                shader["night_color"].Set(color);
                GL.ActiveTexture(6);
                this._skyboxBlank.Bind();
                GL.ActiveTexture(0);
            }

            private void ValidateSkyboxTextures(Map m)
            {
                if (!Guid.Equals(this._dayAssetID, m.DaySkyboxAssetID) || !this.ValidateAssetStatus(this._dayAssetID) || !Guid.Equals(this._nightAssetID, m.NightSkyboxAssetID) || !this.ValidateAssetStatus(this._nightAssetID))
                {
                    this._skyboxArray?.Dispose();
                    this._skyboxArray = null;
                    this._skyboxDayAnimation = null;
                    this._skyboxNightAnimation = null;
                    this._dayAssetID = m.DaySkyboxAssetID;
                    this._nightAssetID = m.NightSkyboxAssetID;
                }

                if (this._skyboxArray == null)
                {
                    this.CreateSkybox();
                }
            }

            private unsafe void CreateSkybox()
            {
                if (this._dayAssetID.IsEmpty() && this._nightAssetID.IsEmpty()) // No skybox if we have no asset pointer
                {
                    return;
                }

                Asset dayAsset = null;
                Asset nightAsset = null;

                if (!this._dayAssetID.IsEmpty())
                {
                    AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(this._dayAssetID, AssetType.Texture, out dayAsset);
                    if (status != AssetStatus.Return || dayAsset == null || dayAsset.Type != AssetType.Texture || dayAsset.Texture == null || !dayAsset.Texture.glReady)
                    {
                        return;
                    }
                }

                if (!this._nightAssetID.IsEmpty())
                {
                    AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(this._nightAssetID, AssetType.Texture, out nightAsset);
                    if (status != AssetStatus.Return || nightAsset == null || nightAsset.Type != AssetType.Texture || nightAsset.Texture == null || !nightAsset.Texture.glReady)
                    {
                        return;
                    }
                }

                if (this._waitingOnAsync)
                {
                    return;
                }

                ThreadPool.QueueUserWorkItem(this.LoadImageAsync, (dayAsset, nightAsset));
                this._waitingOnAsync = true;
            }

            private void LoadImageAsync(object state)
            {
                (Asset, Asset) stateAssets = ((Asset, Asset))state;
                static void CallbackMain(Guid idDay, Guid idNight, Image<Rgba32> imgDay, Image<Rgba32> imgNight, TextureAnimation animDay, TextureAnimation animNight) => Client.Instance.Frontend.EnqueueTask(() => Client.Instance.Frontend.Renderer.SkyRenderer.SkyboxRenderer.HandleAsyncCallback(idDay, idNight, imgDay, imgNight, animDay, animNight));
                static TextureAnimation CreateDummyAnimation(Size sz) => new TextureAnimation(
                    new TextureAnimation.Frame[]
                    {
                        new TextureAnimation.Frame()
                        {
                            Duration = 0,
                            Index = 0,
                            Location = new RectangleF(0, 0, 4f / sz.Width, 3f / sz.Height)
                        }
                    });

                Image<Rgba32> dayImage = stateAssets.Item1 == null ? new Image<Rgba32>(4, 3, new Rgba32(255, 255, 255, 255)) : stateAssets.Item1.Texture.CompoundImage();
                Image<Rgba32> nightImage = stateAssets.Item2 == null ? new Image<Rgba32>(4, 3, new Rgba32(255, 255, 255, 255)) : stateAssets.Item2.Texture.CompoundImage();
                Size maxSize = new Size(Math.Max(dayImage.Width, nightImage.Width), Math.Max(dayImage.Height, nightImage.Height));
                TextureAnimation dayAnim = stateAssets.Item1?.Texture?.CachedAnimation ?? CreateDummyAnimation(maxSize);
                TextureAnimation nightAnim = stateAssets.Item2?.Texture?.CachedAnimation ?? CreateDummyAnimation(maxSize);
                CallbackMain(stateAssets.Item1?.ID ?? Guid.Empty, stateAssets.Item2?.ID ?? Guid.Empty, dayImage, nightImage, dayAnim, nightAnim);
            }

            private void HandleAsyncCallback(Guid idDay, Guid idNight, Image<Rgba32> imgDay, Image<Rgba32> imgNight, TextureAnimation animDay, TextureAnimation animNight)
            {
                if (this._waitingOnAsync)
                {
                    if (Guid.Equals(idDay, this._dayAssetID) && Guid.Equals(idNight, this._nightAssetID))
                    {
                        this._waitingOnAsync = false;
                        this._skyboxArray = new Texture(TextureTarget.Texture2DArray);
                        this._skyboxArray.Bind();
                        this._skyboxArray.SetWrapParameters(WrapParam.ClampToEdge, WrapParam.ClampToEdge, WrapParam.ClampToEdge);
                        this._skyboxArray.SetFilterParameters(FilterParam.Linear, FilterParam.Linear);
                        Size maxSz = new Size(Math.Max(imgDay.Size.Width, imgNight.Size.Width), Math.Max(imgDay.Size.Height, imgNight.Size.Height));
                        GL.TexImage3D(TextureTarget.Texture2DArray, 0, SizedInternalFormat.Rgba8, maxSz.Width, maxSz.Height, 2, PixelDataFormat.Rgba, PixelDataType.Byte, IntPtr.Zero);
                        imgDay.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> mem);
                        System.Buffers.MemoryHandle hnd = mem.Pin();
                        unsafe
                        {
                            GL.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, 0, imgDay.Width, imgDay.Height, 1, PixelDataFormat.Rgba, PixelDataType.Byte, (nint)hnd.Pointer);
                            hnd.Dispose();
                            imgNight.DangerousTryGetSinglePixelMemory(out mem);
                            hnd = mem.Pin();
                            GL.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, 1, imgNight.Width, imgNight.Height, 1, PixelDataFormat.Rgba, PixelDataType.Byte, (nint)hnd.Pointer);
                            hnd.Dispose();
                        }
                    }
                }

                imgDay?.Dispose();
                imgNight?.Dispose();
            }

            private bool ValidateAssetStatus(Guid id) => id.IsEmpty() || (Client.Instance.AssetManager.ClientAssetLibrary.Assets.GetStatus(id, out AssetStatus astat) && astat == AssetStatus.Return);
        }
    }
}