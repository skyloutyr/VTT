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
    using System.Threading;
    using VTT.Render.Shaders;
    using System.Collections.Generic;
    using System.Runtime.Intrinsics;

    public class SkyRenderer
    {
        public FastAccessShader<CelestialBodyUniforms> CelestialBodyShader { get; set; }

        public Skybox SkyboxRenderer { get; set; }

        public Gradient<Vector4> SkyGradient { get; set; } = new Gradient<Vector4>
        {
            [0] = new Vector4(Color.ParseHex("003469").Vec3() / 5.0f, 1.0f),
            [1] = new Vector4(Color.ParseHex("003469").Vec3() / 5.0f, 1.0f),
            [2] = new Vector4(Color.ParseHex("003469").Vec3() / 5.0f, 1.0f),
            [3] = new Vector4(Color.ParseHex("003e6b").Vec3() / 5.0f, 1.0f),
            [4] = new Vector4(Color.ParseHex("003e6b").Vec3() / 3.0f, 1.0f),
            [5] = new Vector4(Color.ParseHex("045f88").Vec3() / 1.0f, 1.0f),
            [5.25f] = new Vector4(Color.ParseHex("67cac7").Vec3(), 1.0f),
            [5.5f] = new Vector4(Color.ParseHex("ece16a").Vec3(), 1.0f),
            [5.75f] = new Vector4(Color.ParseHex("e0ebbe").Vec3(), 1.0f),
            [6] = new Vector4(Color.ParseHex("ece16a").Vec3(), 1.0f),
            [6.5f] = new Vector4(Color.ParseHex("e0ebbe").Vec3(), 1.0f),
            [7] = new Vector4(Color.ParseHex("67cac7").Vec3(), 1.0f),
            [8] = new Vector4(Color.ParseHex("119bba").Vec3(), 1.0f),

            [15] = new Vector4(Color.ParseHex("119bba").Vec3(), 1.0f),
            [16.5f] = new Vector4(Color.ParseHex("1097b6").Vec3(), 1.0f),
            [17.4f] = new Vector4(Color.ParseHex("e0ebbe").Vec3(), 1.0f),
            [17.5f] = new Vector4(Color.ParseHex("ffb26e").Vec3(), 1.0f),
            [17.75f] = new Vector4(Color.ParseHex("ffb26e").Vec3(), 1.0f),
            [17.8f] = new Vector4(Color.ParseHex("feae5b").Vec3(), 1.0f),
            [17.9f] = new Vector4(Color.ParseHex("fea85a").Vec3(), 1.0f),
            [18] = new Vector4(Color.ParseHex("f38e4b").Vec3(), 1.0f),
            [18.25f] = new Vector4(Color.ParseHex("f1717a").Vec3(), 1.0f),
            [18.5f] = new Vector4(Color.ParseHex("d0608c").Vec3(), 1.0f),
            [18.75f] = new Vector4(Color.ParseHex("673184").Vec3(), 1.0f),
            [19f] = new Vector4(Color.ParseHex("3e1d7a").Vec3(), 1.0f),

            [20f] = new Vector4(Color.ParseHex("2a166c").Vec3(), 1.0f),
            [22f] = new Vector4(Color.ParseHex("040e40").Vec3() / 3.0f, 1.0f),
            [23f] = new Vector4(Color.ParseHex("040c3d").Vec3() / 5.0f, 1.0f),
            [24f] = new Vector4(Color.ParseHex("012356").Vec3() / 5.0f, 1.0f)
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

        public Gradient<Vector4> LightGrad { get; set; } = new Gradient<Vector4>
        {
            //[0] = Color.ParseHex("040c3d").Vec3(),
            //[5] = Color.ParseHex("040c3d").Vec3(),
            [0] = new Vector4(Color.ParseHex("000000").Vec3(), 1.0f),
            [5] = new Vector4(Color.ParseHex("000000").Vec3(), 1.0f),
            [5.25f] = new Vector4(Color.ParseHex("67cac7").Vec3(), 1.0f),
            [5.5f] = new Vector4(Color.ParseHex("ece16a").Vec3(), 1.0f),
            [5.75f] = new Vector4(Color.ParseHex("e0ebbe").Vec3(), 1.0f),
            [6] = new Vector4(Color.ParseHex("ece16a").Vec3(), 1.0f),
            [6.5f] = new Vector4(Color.ParseHex("e0ebbe").Vec3(), 1.0f),
            [7] = new Vector4(Color.ParseHex("ffffff").Vec3(), 1.0f),
            [8] = new Vector4(Color.ParseHex("ffffff").Vec3(), 1.0f),

            [15] = new Vector4(Color.ParseHex("ffffff").Vec3(), 1.0f),
            [16] = new Vector4(Color.ParseHex("ffffff").Vec3(), 1.0f),
            [17] = new Vector4(Color.ParseHex("ffffff").Vec3(), 1.0f),
            [17.4f] = new Vector4(Color.ParseHex("e0ebbe").Vec3(), 1.0f),
            [17.5f] = new Vector4(Color.ParseHex("ffb26e").Vec3(), 1.0f),
            [17.75f] = new Vector4(Color.ParseHex("ffb26e").Vec3(), 1.0f),
            [17.8f] = new Vector4(Color.ParseHex("feae5b").Vec3(), 1.0f),
            [17.9f] = new Vector4(Color.ParseHex("fea85a").Vec3(), 1.0f),
            [18] = new Vector4(Color.ParseHex("f38e4b").Vec3(), 1.0f),
            [18.25f] = new Vector4(Color.ParseHex("f1717a").Vec3(), 1.0f),
            [18.5f] = new Vector4(Color.ParseHex("d0608c").Vec3(), 1.0f),
            [18.75f] = new Vector4(Color.ParseHex("673184").Vec3(), 1.0f),
            [19f] = new Vector4(Color.ParseHex("3e1d7a").Vec3(), 1.0f),

            //[20f] = Color.ParseHex("2a166c").Vec3(),
            //[22f] = Color.ParseHex("040e40").Vec3(),
            //[23f] = Color.ParseHex("040c3d").Vec3(),
            //[24f] = Color.ParseHex("012356").Vec3()

            [20f] = new Vector4(Color.ParseHex("000000").Vec3(), 1.0f), // No sun at night
            [24f] = new Vector4(Color.ParseHex("000000").Vec3(), 1.0f)
        };

        public Gradient<Vector4> SunGrad { get; set; } = new Gradient<Vector4>()
        {
            [0f] = new Vector4(0, 0, 0, 0),
            [5.25f] = new Vector4(0.7f, 0f, 0f, 0f),
            [6f] = new Vector4(1, 0.8f, 0.6f, 1f),
            [7f] = new Vector4(1, 1, 1, 1),
            [12f] = new Vector4(1, 1, 1, 1),
            [17.25f] = new Vector4(1, 1, 1, 1),
            [18f] = new Vector4(1, 0.3f, 0f, 1f),
            [19f] = new Vector4(1, 0, 0, 0),
            [24f] = new Vector4(0, 0, 0, 0),
        };

        private GlbScene _modelSun;
        private GlbScene _modelMoon;
        private GlbScene _modelPlanetA;
        private GlbScene _modelPlanetB;
        private GlbScene _modelPlanetC;
        private GlbScene _modelPlanetD;
        private GlbScene _modelPlanetE;

        public Texture GetBuiltInTexture(CelestialBody.RenderPolicy policy)
        {
            GlbScene s = policy switch
            {
                CelestialBody.RenderPolicy.BuiltInSun => this._modelSun,
                CelestialBody.RenderPolicy.BuiltInMoon => this._modelMoon,
                CelestialBody.RenderPolicy.BuiltInPlanetA => this._modelPlanetA,
                CelestialBody.RenderPolicy.BuiltInPlanetB => this._modelPlanetB,
                CelestialBody.RenderPolicy.BuiltInPlanetC => this._modelPlanetC,
                CelestialBody.RenderPolicy.BuiltInPlanetD => this._modelPlanetD,
                CelestialBody.RenderPolicy.BuiltInPlanetE => this._modelPlanetE,
                _ => null
            };

            return s?.Materials[0].BaseColorTexture;
        }

        public void Create()
        {
#if USE_VTX_COMPRESSION
            this.CelestialBodyShader = new FastAccessShader<CelestialBodyUniforms>(OpenGLUtil.LoadShader("celestial_body", stackalloc ShaderType[2] { ShaderType.Vertex, ShaderType.Fragment }, new DefineRule[] { new DefineRule(DefineRule.Mode.Define, "USE_VTX_COMPRESSION") }));
#else
            this.CelestialBodyShader = new FastAccessShader<CelestialBodyUniforms>(OpenGLUtil.LoadShader("celestial_body", stackalloc ShaderType[2] { ShaderType.Vertex, ShaderType.Fragment }, new DefineRule[] { }));
#endif
            this.CelestialBodyShader.Bind();
            this.CelestialBodyShader.Program.BindUniformBlock("Material", 3);
            this.CelestialBodyShader.Uniforms.DiffuseSampler.Set(0);
            this._modelSun = new GlbScene(TextureData.CreateFromExistingGLTexture(OpenGLUtil.LoadUIImage("sun")));
            this._modelMoon = new GlbScene(TextureData.CreateFromExistingGLTexture(OpenGLUtil.LoadUIImage("moon")));
            this._modelPlanetA = new GlbScene(TextureData.CreateFromExistingGLTexture(OpenGLUtil.LoadUIImage("planetA")));
            this._modelPlanetB = new GlbScene(TextureData.CreateFromExistingGLTexture(OpenGLUtil.LoadUIImage("planetB")));
            this._modelPlanetC = new GlbScene(TextureData.CreateFromExistingGLTexture(OpenGLUtil.LoadUIImage("planetC")));
            this._modelPlanetD = new GlbScene(TextureData.CreateFromExistingGLTexture(OpenGLUtil.LoadUIImage("planetD")));
            this._modelPlanetE = new GlbScene(TextureData.CreateFromExistingGLTexture(OpenGLUtil.LoadUIImage("planetE")));
            this.SkyboxRenderer = new Skybox();
            this.SkyboxRenderer.Init();
        }

        public Vector3 GetCurrentSunDirection()
        {
            Map m = Client.Instance.CurrentMap;
            if (m == null)
            {
                return -Vector3.UnitZ;
            }

            CelestialBody sun = m.CelestialBodies.Sun;
            return !sun.Enabled ? -Vector3.UnitZ : GetDirectionFromEulerAngles(sun.SunYaw, sun.SunPitch, sun.SunRoll);
        }

        public Vector3 GetSunDirection(float yaw, float pitch)
        {
            Vector4 vec = -Vector4.UnitZ;
            Quaternion q = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, yaw);
            Quaternion q1 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, pitch);
            return Vector4.Transform(Vector4.Transform(vec, q1), q).Xyz().Normalized();
        }

        public Vector3 GetDirectionFromEulerAngles(float yaw, float pitch, float roll)
        {
            Vector4 vec = -Vector4.UnitZ;
            Quaternion qy = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, yaw);
            Quaternion qp = Quaternion.CreateFromAxisAngle(Vector3.UnitY, pitch);
            Quaternion qr = Quaternion.CreateFromAxisAngle(Vector3.UnitX, roll);
            return Vector4.Transform(Vector4.Transform(Vector4.Transform(vec, qr), qp), qy).Xyz().Normalized();
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

            OpenGLUtil.StartSection("Skybox");
            this.SkyboxRenderer.Render(map, time);
            OpenGLUtil.EndSection();

            if (map.Is2D)
            {
                return;
            }

            CelestialBody sun = map.CelestialBodies.Sun;
            OpenGLUtil.StartSection("Celestial bodies");
            GLState.Blend.Set(true);
            GLState.BlendFunc.Set((BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha));
            GLState.DepthTest.Set(true);
            GL.Disable(Capability.DepthClamp);
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            Vector3 sunDir = this.GetCurrentSunDirection();
            float sunTime = this.GetDayProgress(map);
            this.CelestialBodyShader.Bind();
            foreach (CelestialBody b in map.CelestialBodies)
            {
                if (!b.Enabled)
                {
                    continue;
                }

                // Determine body position
                Vector3 position = Vector3.Zero;
                switch (b.PositionKind)
                {
                    case CelestialBody.PositionPolicy.Angular:
                    {
                        position = -GetDirectionFromEulerAngles(b.Position.X, b.Position.Y, b.Position.Z) * 99;
                        break;
                    }

                    case CelestialBody.PositionPolicy.FollowsSun:
                    case CelestialBody.PositionPolicy.OpposesSun:
                    {
                        Vector3 bposAsAngles = b.Position * MathF.PI / 180.0f;
                        Vector3 angulars = new Vector3(sun.SunYaw + bposAsAngles.X, sun.SunPitch + bposAsAngles.Y, sun.SunRoll + bposAsAngles.Z);
                        position = GetDirectionFromEulerAngles(angulars.X, angulars.Y, angulars.Z) * 99;
                        if (b.PositionKind == CelestialBody.PositionPolicy.OpposesSun)
                        {
                            position = -position;
                        }

                        break;
                    }

                    case CelestialBody.PositionPolicy.Static:
                    {
                        position = b.Position;
                        break;
                    }
                }

                // There were issues with using the previous approach of a billbiard quaternion, so here we construct the billboard matrix manually
                Vector3 look = Vector3.Normalize(-position); // Invert look due to a -Z forward (instead of expected +Y)
                Vector3 right = Vector3.Cross(cam.Up, look);
                Vector3 up = Vector3.Cross(look, right);
                Matrix4x4 billboardRotation = new Matrix4x4(
                    right.X, right.Y, right.Z, 0,
                    up.X, up.Y, up.Z, 0,
                    look.X, look.Y, look.Z, 0,
                    0, 0, 0, 1
                );

                // Determine body rotation
                Matrix4x4 rot = Matrix4x4.CreateFromYawPitchRoll(b.Rotation.X, b.Rotation.Y, b.Rotation.Z);
                if (b.Billboard)
                {
                    rot = rot * billboardRotation;
                }

                Matrix4x4 model = Matrix4x4.CreateScale(b.Scale) * rot * Matrix4x4.CreateTranslation(cam.Position + position);
                GlbScene scene = null;
                switch (b.RenderKind)
                {
                    case CelestialBody.RenderPolicy.BuiltInSun:
                    {
                        scene = this._modelSun;
                        break;
                    }

                    case CelestialBody.RenderPolicy.BuiltInMoon:
                    {
                        scene = this._modelMoon;
                        break;
                    }

                    case CelestialBody.RenderPolicy.BuiltInPlanetA:
                    {
                        scene = this._modelPlanetA;
                        break;
                    }

                    case CelestialBody.RenderPolicy.BuiltInPlanetB:
                    {
                        scene = this._modelPlanetB;
                        break;
                    }

                    case CelestialBody.RenderPolicy.BuiltInPlanetC:
                    {
                        scene = this._modelPlanetC;
                        break;
                    }

                    case CelestialBody.RenderPolicy.BuiltInPlanetD:
                    {
                        scene = this._modelPlanetD;
                        break;
                    }

                    case CelestialBody.RenderPolicy.BuiltInPlanetE:
                    {
                        scene = this._modelPlanetE;
                        break;
                    }

                    case CelestialBody.RenderPolicy.Custom:
                    {
                        AssetStatus aStat = Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(b.AssetRef, AssetType.Model, out Asset a);
                        if (aStat == AssetStatus.Return && a.Type == AssetType.Model && a.ModelGlReady)
                        {
                            scene = a.Model.GLMdl;
                        }

                        break;
                    }
                }

                float ownTime = (float)(b.SunPitch + MathF.PI) / MathF.PI * 12;
                this.CelestialBodyShader.Uniforms.Color.Set(b.OwnColor.GetColor(map, b.UseOwnTime ? ownTime : sunTime));
                scene?.Render(in this.CelestialBodyShader.Uniforms.glbEssentials, model, cam.Projection, cam.View, double.NaN, null, 0, null);
            }

            GL.Enable(Capability.DepthClamp);
            GLState.DepthTest.Set(false);
            GLState.Blend.Set(false);
            OpenGLUtil.EndSection();
        }

        public float GetDayProgress(Map map)
        {
            if (map == null)
            {
                return 0;
            }

            CelestialBody sun = map.CelestialBodies.Sun;
            return !sun.Enabled ? 12 : (float)(sun.SunPitch + MathF.PI) / MathF.PI * 12;
        }

        public Color GetSkyColor()
        {
            Map map = Client.Instance.CurrentMap;
            if (map == null)
            {
                return Color.Black;
            }

            if (!map.CelestialBodies.Sun.Enabled)
            {
                return Extensions.FromVec4(map.DaySkyboxColors.GetColor(map, 12));
            }

            float dayProgress = this.GetDayProgress(map);
            const float cutoutPointNightToDayStart = 4.8f;
            const float cutoutPointNightToDayEnd = 7.6f;
            const float cutoutPointDayToNightStart = 16f;
            const float cutoutPointDayToNightEnd = 21f;
            float dayNightFactor =
                dayProgress < cutoutPointNightToDayStart ? 1.0f
                : dayProgress < cutoutPointNightToDayEnd ? 1.0f - ((dayProgress - cutoutPointNightToDayStart) / (cutoutPointNightToDayEnd - cutoutPointNightToDayStart))
                : dayProgress > cutoutPointDayToNightStart ? (dayProgress - cutoutPointDayToNightStart) / (cutoutPointDayToNightEnd - cutoutPointDayToNightStart)
                : dayProgress > cutoutPointDayToNightEnd ? 1.0f : 0.0f;
            return Extensions.FromVec4(Vector4.Lerp(map.DaySkyboxColors.GetColor(map, dayProgress), map.NightSkyboxColors.GetColor(map, dayProgress), dayNightFactor));

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

            CelestialBody sun = map.CelestialBodies.Sun;
            if (!sun.Enabled)
            {
                return map.SunColor;
            }

            float pitch = sun.SunPitch + MathF.PI;
            float idx = pitch / MathF.PI * 12;
            return (Color)(sun.LightColor.GetColor(map, idx) * new Vector4(0.25f, 0.25f, 0.25f, 1.0f));
        }

        public Color GetAmbientColor()
        {
            Map map = Client.Instance.CurrentMap;
            if (map == null)
            {
                return Color.White;
            }

            CelestialBody sun = map.CelestialBodies.Sun;
            if (!sun.Enabled)
            {
                return map.AmbientColor;
            }

            float pitch = sun.SunPitch + MathF.PI;
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
            private FastAccessShader<SkyboxUniforms> _skyboxShader;

            public Texture SkyboxTextureArray => this._skyboxArray ?? this._skyboxBlank;
            public Texture BlankTextureArray => this._skyboxBlank;
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
                OpenGLUtil.NameObject(GLObjectType.Texture, this._skyboxBlank, "Skybox blank texture");
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

                this._skyboxShader = new FastAccessShader<SkyboxUniforms>(OpenGLUtil.LoadShader("skybox", stackalloc ShaderType[2] { ShaderType.Vertex, ShaderType.Fragment }));
                this._skyboxShader.Bind();
                this._skyboxShader.Uniforms.Skybox.Sampler.Set(6);
                OpenGLUtil.NameObject(GLObjectType.VertexArray, this._vao, "Skybox cube vao");
                OpenGLUtil.NameObject(GLObjectType.Buffer, this._vbo, "Skybox cube vbo");
            }

            public void Clear()
            {
                this._dayAssetID = Guid.Empty;
                this._nightAssetID = Guid.Empty;
                this._waitingOnAsync = false;
            }

            public float CachedBlendDelta { get; set; }
            public Vector4 CachedDayColors { get; set; }
            public Vector4 CachedNightColors { get; set; }

            public void Render(Map m, double dt)
            {
                this.ValidateSkyboxTextures(m);
                Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
                GLState.DepthTest.Set(true);
                GLState.DepthFunc.Set(ComparisonMode.LessOrEqual);
                GLState.DepthMask.Set(false);

                this._skyboxShader.Bind();
                this.UniformShader(this._skyboxShader.Uniforms.Skybox, m);
                this._skyboxShader.Uniforms.Transform.Projection.Set(m.Is2D ? Matrix4x4.CreatePerspectiveFieldOfView(Client.Instance.Settings.FOV * MathF.PI / 180.0f, (float)Client.Instance.Frontend.Width / Client.Instance.Frontend.Height, 0.01f, 100f) : cam.Projection);
                this._skyboxShader.Uniforms.Transform.View.Set(cam.View.ClearTranslation());

                this._vao.Bind();
                GLState.DrawArrays(PrimitiveType.Triangles, 0, 36);

                GLState.DepthMask.Set(true);
                GLState.DepthFunc.Set(ComparisonMode.Less);
                GLState.DepthTest.Set(false);

                float dayProgress = Client.Instance.Frontend.Renderer.SkyRenderer.GetDayProgress(m);
                const float cutoutPointNightToDayStart = 4.8f;
                const float cutoutPointNightToDayEnd = 7.6f;
                const float cutoutPointDayToNightStart = 16f;
                const float cutoutPointDayToNightEnd = 21f;
                float dayNightFactor =
                    dayProgress < cutoutPointNightToDayStart ? 1.0f
                    : dayProgress < cutoutPointNightToDayEnd ? 1.0f - ((dayProgress - cutoutPointNightToDayStart) / (cutoutPointNightToDayEnd - cutoutPointNightToDayStart))
                    : dayProgress > cutoutPointDayToNightStart ? (dayProgress - cutoutPointDayToNightStart) / (cutoutPointDayToNightEnd - cutoutPointDayToNightStart)
                    : dayProgress > cutoutPointDayToNightEnd ? 1.0f : 0.0f;

                this.CachedBlendDelta = dayNightFactor;
                this.CachedDayColors = m.DaySkyboxColors.GetColor(m, dayProgress);
                this.CachedNightColors = m.NightSkyboxColors.GetColor(m, dayProgress);
            }

            public void UniformShaderWithRespectToUBO(UniformBlockSkybox uniforms, Map m)
            {
                /* Old non-ubo handling code
                if (!Client.Instance.Settings.UseUBO)
                {
                    this.UniformShader(uniforms, m);
                }
                */

                GLState.ActiveTexture.Set(6);
                this.SkyboxTextureArray.Bind();
                GLState.ActiveTexture.Set(0);
            }

            public void UniformShader(UniformBlockSkybox uniforms, Map m)
            {
                uniforms.DayAnimationFrame.Set(this.DayAnimation?.FindFrameForIndex(double.NaN).LocationUniform ?? new Vector4(0, 0, 1, 1));
                uniforms.NightAnimationFrame.Set(this.NightAnimation?.FindFrameForIndex(double.NaN).LocationUniform ?? new Vector4(0, 0, 1, 1));
                uniforms.ColorsBlend.Set(new Vector4(
                    VTTMath.UInt32BitsToSingle(this.CachedDayColors.Rgba()),
                    VTTMath.UInt32BitsToSingle(this.CachedNightColors.Rgba()),
                    this.CachedBlendDelta,
                    0
                ));

                GLState.ActiveTexture.Set(6);
                this.SkyboxTextureArray.Bind();
                GLState.ActiveTexture.Set(0);
            }

            public void UniformBlank(UniformBlockSkybox uniforms, Vector3 color)
            {
                uniforms.DayAnimationFrame.Set(new Vector4(0, 0, 1, 1));
                uniforms.NightAnimationFrame.Set(new Vector4(0, 0, 1, 1));
                uniforms.ColorsBlend.Set(new Vector4(
                    VTTMath.UInt32BitsToSingle(color.Rgba()),
                    VTTMath.UInt32BitsToSingle(color.Rgba()),
                    0, 0
                ));

                GLState.ActiveTexture.Set(6);
                this._skyboxBlank.Bind();
                GLState.ActiveTexture.Set(0);
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
                        OpenGLUtil.NameObject(GLObjectType.Texture, this._skyboxArray, "Skybox texture array");
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