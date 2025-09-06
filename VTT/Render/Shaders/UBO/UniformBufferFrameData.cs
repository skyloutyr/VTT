namespace VTT.Render.Shaders.UBO
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Runtime.Intrinsics;
    using VTT.Control;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Render.LightShadow;
    using VTT.Util;

    public sealed class UniformBufferFrameData : UniformBufferObject<FrameUBO>
    {
        public UniformBufferFrameData() : base(1)
        {
            OpenGLUtil.NameObject(GLObjectType.Buffer, this._buffer, "Frame UBO");
        }

        public unsafe void SetBlank(Camera cam, DirectionalLight sun, Vector4 clearColor)
        {
            this._cpuMemory->view = cam.View;
            this._cpuMemory->projection = cam.Projection;
            // this._cpuMemory->sun_matrix = Matrix4x4.Identity; // Do not care about this anymore
            this._cpuMemory->camera_position = cam.Position;
            this._cpuMemory->SunDirection = sun.Direction;
            this._cpuMemory->camera_direction = cam.Direction;
            this._cpuMemory->SunColor = sun.Color;
            this._cpuMemory->AmbientColor = new Vector3(0.03f);
            this._cpuMemory->SkyColor = clearColor.Xyz();
            this._cpuMemory->viewport_size = new Vector2(Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Width, Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Height);
            this._cpuMemory->cursor_position = Vector3.Zero;
            this._cpuMemory->GridColor = Vector4.Zero;
            this._cpuMemory->Frame = (uint)Client.Instance.Frontend.FramesExisted;
            this._cpuMemory->Update = (uint)Client.Instance.Frontend.UpdatesExisted;
            this._cpuMemory->update_dt = 0f;
            this._cpuMemory->grid_size = 1f;
            this._cpuMemory->SkyboxDayColor = clearColor;
            this._cpuMemory->SkyboxNightColor = clearColor;
            this._cpuMemory->skybox_blend_factor = 0f;
            this._cpuMemory->PointLightCount = 0;
            this._cpuMemory->skybox_animation_day = new Vector4(0, 0, 1, 1);
            this._cpuMemory->skybox_animation_night = new Vector4(0, 0, 1, 1);
            this._cpuMemory->shadow_cascade_0 = Matrix4x4.Identity;
            this._cpuMemory->shadow_cascade_1 = Matrix4x4.Identity;
            this._cpuMemory->shadow_cascade_2 = Matrix4x4.Identity;
            this._cpuMemory->shadow_cascade_3 = Matrix4x4.Identity;
            this._cpuMemory->shadow_cascade_4 = Matrix4x4.Identity;
            this._cpuMemory->shadow_cascade_plane_distances = Vector4.Zero;
            // No need to uniform point lights here as with a count of 0 up1loaded we don't care about the actual data on the GPU
            this._cpuMemory->fow_scale = 0;
            this._cpuMemory->fow_mod = 0;
            this.Upload();
        }

        public unsafe void SetData(Map m, double delta)
        {
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            SkyRenderer.Skybox skybox = Client.Instance.Frontend.Renderer.SkyRenderer.SkyboxRenderer;
            MapObjectRenderer mor = Client.Instance.Frontend.Renderer.ObjectRenderer;
            SunShadowRenderer dlRenderer = Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer;
            PointLightsRenderer plr = Client.Instance.Frontend.Renderer.PointLightsRenderer;
            plr.PackLightData(out int plNum, out Span<Vector4> plPosColors, out Span<float> plCutoutData);
            float dayProgress = Client.Instance.Frontend.Renderer.SkyRenderer.GetDayProgress(m);
            FOWRenderer fow = Client.Instance.Frontend.Renderer.MapRenderer.FOWRenderer;
            unsafe
            {
                this._cpuMemory->view = cam.View;
                this._cpuMemory->projection = cam.Projection;
                // this._cpuMemory->sun_matrix = Matrix4x4.Identity; // Sun matrix is obsolete in favour of cascades
                this._cpuMemory->camera_position = cam.Position;
                this._cpuMemory->SunDirection = mor.CachedSunDirection;
                this._cpuMemory->camera_direction = cam.Direction;
                this._cpuMemory->SunColor = mor.CachedSunColor * m.SunIntensity;
                this._cpuMemory->AmbientColor = mor.CachedAmbientColor * m.AmbientIntensity;
                this._cpuMemory->SkyColor = mor.CachedSkyColor;
                this._cpuMemory->viewport_size = new Vector2(Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Width, Client.Instance.Frontend.GameHandle.FramebufferSize.Value.Height);
                this._cpuMemory->cursor_position = Client.Instance.Frontend.Renderer.MapRenderer.TerrainHit ?? Vector3.Zero;
                this._cpuMemory->GridColor = m.GridColor.Vec4();
                this._cpuMemory->Frame = (uint)Client.Instance.Frontend.FramesExisted;
                this._cpuMemory->Update = (uint)Client.Instance.Frontend.UpdatesExisted;
                this._cpuMemory->update_dt = (float)delta;
                this._cpuMemory->grid_size = m.GridSize;
                this._cpuMemory->SkyboxDayColorV3 = skybox.CachedDayColors.AsVector128().AsVector3();
                this._cpuMemory->SkyboxNightColorV3 = skybox.CachedNightColors.AsVector128().AsVector3();
                this._cpuMemory->skybox_blend_factor = skybox.CachedBlendDelta;
                this._cpuMemory->skybox_animation_day = skybox.DayAnimation?.FindFrameForIndex(double.NaN).LocationUniform ?? new Vector4(0, 0, 1, 1);
                this._cpuMemory->skybox_animation_night = skybox.NightAnimation?.FindFrameForIndex(double.NaN).LocationUniform ?? new Vector4(0, 0, 1, 1);
                this._cpuMemory->SunShadowCascades = dlRenderer.Cascades.CascadeArray.AsSpan();
                this._cpuMemory->shadow_cascade_plane_distances = dlRenderer.Cascades.ShadowCascadeLevelsAsVec4 * 100; // TODO fix hardcoded far plane!
                this._cpuMemory->PointLightCount = plNum;
                this._cpuMemory->PointLightPositionsWPackedColors = plPosColors;
                this._cpuMemory->PointLightCutoutValues = plCutoutData;
                this._cpuMemory->fow_scale = fow.FOWWorldSize.X;
                this._cpuMemory->fow_mod = Client.Instance.IsAdmin ? Client.Instance.Settings.FOWAdmin : 1.0f;
            }

            this.Upload();
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 992, Pack = 1)]
    public unsafe struct FrameUBO
    {
        [FieldOffset(0)] public Matrix4x4 view;
        [FieldOffset(64)] public Matrix4x4 projection;
        [FieldOffset(128)] public Matrix4x4 sun_matrix;
        [FieldOffset(192)] public Vector3 camera_position;
        [FieldOffset(204)] public float sun_direction_packed;
        [FieldOffset(208)] public Vector3 camera_direction;
        [FieldOffset(220)] public float sun_color_packed;
        [FieldOffset(224)] public float ambient_color_packed;
        [FieldOffset(228)] public float sky_color_packed;
        [FieldOffset(232)] public Vector2 viewport_size;
        [FieldOffset(240)] public Vector3 cursor_position;
        [FieldOffset(252)] public float grid_color_packed;
        [FieldOffset(256)] public float frame_reinterpreted;
        [FieldOffset(260)] public float update_reinterpreted;
        [FieldOffset(264)] public float update_dt;
        [FieldOffset(268)] public float grid_size;
        [FieldOffset(272)] public float skybox_day_color_packed;
        [FieldOffset(276)] public float skybox_night_color_packed;
        [FieldOffset(280)] public float skybox_blend_factor;
        [FieldOffset(284)] public float pl_num_packed;
        [FieldOffset(288)] public Vector4 skybox_animation_day;
        [FieldOffset(304)] public Vector4 skybox_animation_night;
        [FieldOffset(320)] public Matrix4x4 shadow_cascade_0;
        [FieldOffset(384)] public Matrix4x4 shadow_cascade_1;
        [FieldOffset(448)] public Matrix4x4 shadow_cascade_2;
        [FieldOffset(512)] public Matrix4x4 shadow_cascade_3;
        [FieldOffset(576)] public Matrix4x4 shadow_cascade_4;
        [FieldOffset(640)] public Vector4 shadow_cascade_plane_distances;
        [FieldOffset(656)] public Vector4 pl_position_color_packed_0;
        [FieldOffset(672)] public Vector4 pl_position_color_packed_1;
        [FieldOffset(688)] public Vector4 pl_position_color_packed_2;
        [FieldOffset(704)] public Vector4 pl_position_color_packed_3;
        [FieldOffset(720)] public Vector4 pl_position_color_packed_4;
        [FieldOffset(736)] public Vector4 pl_position_color_packed_5;
        [FieldOffset(752)] public Vector4 pl_position_color_packed_6;
        [FieldOffset(768)] public Vector4 pl_position_color_packed_7;
        [FieldOffset(784)] public Vector4 pl_position_color_packed_8;
        [FieldOffset(800)] public Vector4 pl_position_color_packed_9;
        [FieldOffset(816)] public Vector4 pl_position_color_packed_10;
        [FieldOffset(832)] public Vector4 pl_position_color_packed_11;
        [FieldOffset(848)] public Vector4 pl_position_color_packed_12;
        [FieldOffset(864)] public Vector4 pl_position_color_packed_13;
        [FieldOffset(880)] public Vector4 pl_position_color_packed_14;
        [FieldOffset(896)] public Vector4 pl_position_color_packed_15;
        [FieldOffset(912)] public Vector4 pl_cutouts_0123;
        [FieldOffset(928)] public Vector4 pl_cutouts_4567;
        [FieldOffset(944)] public Vector4 pl_cutouts_891011;
        [FieldOffset(960)] public Vector4 pl_cutouts_12131415;
        [FieldOffset(976)] public float fow_scale;
        [FieldOffset(980)] public float fow_mod;
        [FieldOffset(984)] public float padding984;
        [FieldOffset(988)] public float padding988;

        public Vector3 SunDirection
        {
            set => this.sun_direction_packed = value.PackNorm101010();
        }

        public Vector3 SunColor
        {
            set => this.sun_color_packed = VTTMath.UInt32BitsToSingle(value.Rgba());
        }

        public Vector3 AmbientColor
        {
            set => this.ambient_color_packed = VTTMath.UInt32BitsToSingle(value.Rgba());
        }

        public Vector3 SkyColor
        {
            set => this.sky_color_packed = VTTMath.UInt32BitsToSingle(value.Rgba());
        }

        public Vector4 GridColor
        {
            set => this.grid_color_packed = VTTMath.UInt32BitsToSingle(value.Rgba());
        }

        public uint Update
        {
            set => this.update_reinterpreted = VTTMath.UInt32BitsToSingle(value);
        }

        public uint Frame
        {
            set => this.frame_reinterpreted = VTTMath.UInt32BitsToSingle(value);
        }

        public Vector4 SkyboxDayColor
        {
            set => this.skybox_day_color_packed = VTTMath.UInt32BitsToSingle(value.Rgba());
        }

        public Vector3 SkyboxDayColorV3
        {
            set => this.skybox_day_color_packed = VTTMath.UInt32BitsToSingle(value.Rgba());
        }

        public Vector4 SkyboxNightColor
        {
            set => this.skybox_night_color_packed = VTTMath.UInt32BitsToSingle(value.Rgba());
        }

        public Vector3 SkyboxNightColorV3
        {
            set => this.skybox_night_color_packed = VTTMath.UInt32BitsToSingle(value.Rgba());
        }

        // Very spooky - but should be fine as UBO by spec requires address to be allocated on the heap (outside of GC jurisdiction) so it is 'pinned' by default
        // DO NOT DO THIS, this is **really** unsafe!
        public Span<Matrix4x4> SunShadowCascades
        {
            private get
            {
                void* self = Unsafe.AsPointer(ref this);
                return new Span<Matrix4x4>((byte*)self + 320, 5);
            }

            set => value.CopyTo(this.SunShadowCascades);
        }

        public Vector4 SunShadowCascadesPlaneDistances
        {
            set => this.shadow_cascade_plane_distances = value;
        }

        public int PointLightCount
        {
            set => this.pl_num_packed = VTTMath.Int32BitsToSingle(value);
        }

        public Span<Vector4> PointLightPositionsWPackedColors
        {
            private get
            {
                void* self = Unsafe.AsPointer(ref this);
                return new Span<Vector4>((byte*)self + 656, 16);
            }

            set => value.CopyTo(this.PointLightPositionsWPackedColors);
        }

        public Span<float> PointLightCutoutValues
        {
            private get
            {
                void* self = Unsafe.AsPointer(ref this);
                return new Span<float>((byte*)self + 912, 16);
            }

            set => value.CopyTo(this.PointLightCutoutValues);
        }
    }
}
