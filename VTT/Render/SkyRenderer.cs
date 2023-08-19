namespace VTT.Render
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using System;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public class SkyRenderer
    {
        private VertexArray _vao;
        private GPUBuffer _vbo;

        public ShaderProgram SkyShader { get; set; }

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
            this.SkyShader = OpenGLUtil.LoadShader("sky", ShaderType.VertexShader, ShaderType.FragmentShader);
            this._vao = new VertexArray();
            this._vbo = new GPUBuffer(BufferTarget.ArrayBuffer);
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
        }

        public Vector3 GetCurrentSunDirection() => Client.Instance.CurrentMap.SunEnabled ? this.GetSunDirection(Client.Instance.CurrentMap?.SunYaw ?? 1, Client.Instance.CurrentMap?.SunPitch ?? 1) : -Vector3.UnitZ;
        public Vector3 GetCurrentSunUp() => this.GetSunUp(Client.Instance.CurrentMap?.SunYaw ?? 1, Client.Instance.CurrentMap?.SunPitch ?? 1);

        public Vector3 GetSunDirection(float yaw, float pitch)
        {
            Vector4 vec = -Vector4.UnitZ;
            Quaternion q = Quaternion.FromAxisAngle(Vector3.UnitZ, yaw);
            Quaternion q1 = Quaternion.FromAxisAngle(Vector3.UnitY, pitch);
            return (q * (q1 * vec)).Xyz.Normalized();
        }

        public Vector3 GetSunUp(float yaw, float pitch)
        {
            Vector4 vec = -Vector4.UnitY;
            Quaternion q = Quaternion.FromAxisAngle(Vector3.UnitZ, yaw);
            Quaternion q1 = Quaternion.FromAxisAngle(Vector3.UnitY, pitch);
            return (q * (q1 * vec)).Xyz.Normalized();
        }

        public void Render(double time)
        {
            Control.Map map = Client.Instance.CurrentMap;
            if (map == null)
            {
                return;
            }

            float pitch = map.SunPitch;
            if (!map.SunEnabled || pitch < -1.6493362 || pitch > 1.6580629)
            {
                return;
            }

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            Vector3 sunDir = this.GetCurrentSunDirection();
            this.SkyShader.Bind();
            this.SkyShader["projection"].Set(cam.Projection);
            this.SkyShader["view"].Set(cam.View);
            Vector3 a = Vector3.Cross(Vector3.UnitZ, -cam.Direction);
            Quaternion q = new Quaternion(a, 1 + Vector3.Dot(Vector3.UnitZ, -cam.Direction));
            Matrix4 model = Matrix4.CreateScale(8) * Matrix4.CreateFromQuaternion(q) * Matrix4.CreateTranslation(cam.Position - (sunDir * 99));
            this.SkyShader["model"].Set(model);
            this._vao.Bind();

            GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
            GL.Disable(EnableCap.Blend);
        }

        public Color GetSkyColor()
        {
            Control.Map map = Client.Instance.CurrentMap;
            if (map == null)
            {
                return Color.Black;
            }

            if (!map.SunEnabled)
            {
                return map.BackgroundColor;
            }

            float pitch = map.SunPitch + MathF.PI;
            float idx = pitch / MathF.PI * 12;
            return Extensions.FromVec3(this.SkyGradient.Interpolate(idx, GradientInterpolators.LerpVec3));
        }

        public Color GetSunColor()
        {
            Control.Map map = Client.Instance.CurrentMap;
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
            Control.Map map = Client.Instance.CurrentMap;
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
    }
}