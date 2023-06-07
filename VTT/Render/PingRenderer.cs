namespace VTT.Render
{
    using ImGuiNET;
    using OpenTK.Graphics.OpenGL;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using VTT.Asset.Obj;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public class PingRenderer
    {
        public Texture PingGeneric { get; set; }
        public Texture PingAttack { get; set; }
        public Texture PingDefend { get; set; }
        public Texture PingExclamation { get; set; }
        public Texture PingQuestion { get; set; }

        public WavefrontObject ModelPing { get; set; }

        private readonly object _lock = new object();
        public List<Ping> ActivePings { get; } = new List<Ping>();

        public void Create()
        {
            this.PingDefend = OpenGLUtil.LoadUIImage("icons8-security-lock-80");
            this.PingAttack = OpenGLUtil.LoadUIImage("icons8-sword-80");
            this.PingGeneric = OpenGLUtil.LoadUIImage("icons8-double-down-80");
            this.PingExclamation = OpenGLUtil.LoadUIImage("icons8-box-important-80");
            this.PingQuestion = OpenGLUtil.LoadUIImage("icons8-help-80");
            this.ModelPing = OpenGLUtil.LoadModel("ping_lower", VertexFormat.Pos);

            for (int i = 0; i < 32; ++i)
            {
                float d2r = 0.0872664626f + (i * 2.5f * MathF.PI / 180);
                float ca = MathF.Cos(d2r);
                float sa = MathF.Sin(d2r);
                float d2r2 = 0.0349066f + (i * 2.6875f * MathF.PI / 180);
                float ca2 = MathF.Cos(d2r2);
                float sa2 = MathF.Sin(d2r2);
                Vector2 vl = new Vector2(-ca, -sa);
                Vector2 vl2 = new Vector2(-ca2, -sa2);
                this._poly[i] = (Vector2.Normalize(vl) * 43);
                this._poly[63 - i] = (vl * 128);
                this._poly[256 + i] = (Vector2.Normalize(vl2) * 40);
                this._poly[319 - i] = (vl2 * 131);
            }

            for (int i = 0; i < 32; ++i)
            {
                float d2r = 1.65806279f + (i * 2.5f * MathF.PI / 180);
                float ca = MathF.Cos(d2r);
                float sa = MathF.Sin(d2r);
                float d2r2 = 1.6057f + (i * 2.6875f * MathF.PI / 180);
                float ca2 = MathF.Cos(d2r2);
                float sa2 = MathF.Sin(d2r2);
                Vector2 vl = new Vector2(-ca, -sa);
                Vector2 vl2 = new Vector2(-ca2, -sa2);
                this._poly[64 + i] = (Vector2.Normalize(vl) * 43);
                this._poly[127 - i] = (vl * 128);
                this._poly[320 + i] = (Vector2.Normalize(vl2) * 40);
                this._poly[383 - i] = (vl2 * 131);
            }

            for (int i = 0; i < 32; ++i)
            {
                float d2r = 3.22886f + (i * 2.5f * MathF.PI / 180);
                float ca = MathF.Cos(d2r);
                float sa = MathF.Sin(d2r);
                float d2r2 = 3.1765f + (i * 2.6875f * MathF.PI / 180);
                float ca2 = MathF.Cos(d2r2);
                float sa2 = MathF.Sin(d2r2);
                Vector2 vl = new Vector2(-ca, -sa);
                Vector2 vl2 = new Vector2(-ca2, -sa2);
                this._poly[128 + i] = (Vector2.Normalize(vl) * 43);
                this._poly[191 - i] = (vl * 128);
                this._poly[384 + i] = (Vector2.Normalize(vl2) * 40);
                this._poly[447 - i] = (vl2 * 131);
            }

            for (int i = 0; i < 32; ++i)
            {
                float d2r = 4.79966f + (i * 2.5f * MathF.PI / 180);
                float ca = MathF.Cos(d2r);
                float sa = MathF.Sin(d2r);
                float d2r2 = 4.7473f + (i * 2.6875f * MathF.PI / 180);
                float ca2 = MathF.Cos(d2r2);
                float sa2 = MathF.Sin(d2r2);
                Vector2 vl = new Vector2(-ca, -sa);
                Vector2 vl2 = new Vector2(-ca2, -sa2);
                this._poly[192 + i] = (Vector2.Normalize(vl) * 43);
                this._poly[255 - i] = (vl * 128);
                this._poly[448 + i] = (Vector2.Normalize(vl2) * 40);
                this._poly[511 - i] = (vl2 * 131);
            }
        }

        public bool MouseOverPolygon(Vector2 p, int index)
        {
            bool inside = false;
            Vector2[] polygon = this._poly2;
            for (int i = index, j = index + 63; i < index + 64; j = i++)
            {
                if ((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y) &&
                     p.X < ((polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y)) + polygon[i].X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public void AddPing(Ping p)
        {
            lock (this._lock)
            {
                p.DeathTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 10000;
                this.ActivePings.Add(p);
            }
        }

        public void ClearPings()
        {
            lock (this._lock)
            {
                this.ActivePings.Clear();
            }

            this._pingUI = false;
        }

        private bool _pingUI;
        private OpenTK.Mathematics.Vector3 _pingUIAnchor;
        public void BeginPingUI()
        {
            this._pingUI = true;
            this._pingUIAnchor = Client.Instance.Frontend.Renderer.RulerRenderer.TerrainHit ?? Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld ?? (Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Position + (Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Direction * 5));
        }

        public void EndPingUI()
        {
            if (this._pingUI)
            {
                Vector3 c = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.ToScreenspace(this._pingUIAnchor).SystemVector();
                Vector2 uic = new Vector2(c.X, c.Y);
                Vector2 mouseC = ImGui.GetMousePos();
                bool mouseOverCircle = (mouseC - uic).Length() <= 24;

                bool b0 = this.MouseOverPolygon(mouseC, 0);
                bool b1 = this.MouseOverPolygon(mouseC, 64);
                bool b2 = this.MouseOverPolygon(mouseC, 128);
                bool b3 = this.MouseOverPolygon(mouseC, 192);

                if (mouseOverCircle || b0 || b1 || b2 || b3)
                {
                    Ping.PingType t =
                        mouseOverCircle ? Ping.PingType.Generic :
                        b0 ? Ping.PingType.Attack :
                        b1 ? Ping.PingType.Defend :
                        b2 ? Ping.PingType.Exclamation : Ping.PingType.Question;

                    Ping p = new Ping() { DeathTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 10000, OwnerColor = Extensions.FromArgb(Client.Instance.Settings.Color), OwnerID = Client.Instance.ID, OwnerName = Client.Instance.Settings.Name, Position = this._pingUIAnchor, Type = t };
                    new PacketPing() { Ping = p }.Send();
                }
            }

            this._pingUI = false;
        }

        public void Render(double time)
        {
            if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
            {
                GL.Enable(EnableCap.Multisample);
                GL.Enable(EnableCap.SampleAlphaToCoverage);
            }

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            ShaderProgram shader = Client.Instance.Frontend.Renderer.ObjectRenderer.OverlayShader;
            shader.Bind();
            shader["view"].Set(cam.View);
            shader["projection"].Set(cam.Projection);

            for (int i = this.ActivePings.Count - 1; i >= 0; i--)
            {
                Ping p = this.ActivePings[i];
                if (p.DeathTime <= now)
                {
                    this.ActivePings.RemoveAt(i);
                    continue;
                }

                float scale = MathF.Max(0.00001f, (((p.DeathTime - now) % 2000) - 500) / 500f);
                float a = MathF.Min(1, MathF.Abs(1.0f / scale));
                shader["model"].Set(OpenTK.Mathematics.Matrix4.CreateScale(scale) * OpenTK.Mathematics.Matrix4.CreateTranslation(p.Position));
                shader["u_color"].Set(p.OwnerColor.Vec4() * new OpenTK.Mathematics.Vector4(1, 1, 1, a));
                this.ModelPing.Render();
            }

            GL.Disable(EnableCap.Blend);
            if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
            {
                GL.Disable(EnableCap.SampleAlphaToCoverage);
                GL.Disable(EnableCap.Multisample);
            }
        }

        private readonly Vector2[] _poly = new Vector2[512];
        private readonly Vector2[] _poly2 = new Vector2[512];

        private readonly uint clrG = Extensions.FromHex("404040").Abgr();
        private readonly uint clrGBright = Extensions.FromHex("606060").Abgr();
        private readonly uint clrB = Color.Black.Abgr();
        private readonly uint clrSelected = Color.RoyalBlue.Abgr();
        private readonly uint clrA = Extensions.FromHex("4e2623").Abgr();
        private readonly uint clrAB = Extensions.FromHex("8f0e1f").Abgr();
        private readonly uint clrD = Extensions.FromHex("242238").Abgr();
        private readonly uint clrDB = Extensions.FromHex("0f2073").Abgr();
        private readonly uint clrE = Extensions.FromHex("3b4451").Abgr();
        private readonly uint clrEB = Extensions.FromHex("195f97").Abgr();
        private readonly uint clrQ = Extensions.FromHex("373222").Abgr();
        private readonly uint clrQB = Extensions.FromHex("54470b").Abgr();

        public void RenderUI()
        {
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            ImDrawListPtr winDrawList = ImGui.GetForegroundDrawList();
            if (this._pingUI)
            {
                Vector3 c = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.ToScreenspace(this._pingUIAnchor).SystemVector();
                Vector2 uic = new Vector2(c.X, c.Y);
                Vector2 mouseC = ImGui.GetMousePos();
                
                bool mouseOverCircle = (mouseC - uic).Length() <= 24;
                winDrawList.AddCircleFilled(uic, 27, mouseOverCircle ? clrSelected : clrB);
                winDrawList.AddCircleFilled(uic, 24, mouseOverCircle ? clrGBright : clrG);
                winDrawList.AddImage(this.PingGeneric, uic - new Vector2(20, 20), uic + new Vector2(20, 20));

                for (int i = 0; i < 512; ++i)
                {
                    this._poly2[i] = this._poly[i] + uic;
                }

                bool b0 = this.MouseOverPolygon(mouseC, 0);
                bool b1 = this.MouseOverPolygon(mouseC, 64);
                bool b2 = this.MouseOverPolygon(mouseC, 128);
                bool b3 = this.MouseOverPolygon(mouseC, 192);
                winDrawList.AddConvexPolyFilled(ref this._poly2[256], 64, b0 ? clrSelected : clrB);
                winDrawList.AddConvexPolyFilled(ref this._poly2[0], 64, b0 ? clrAB : clrA);
                winDrawList.AddConvexPolyFilled(ref this._poly2[320], 64, b1 ? clrSelected : clrB);
                winDrawList.AddConvexPolyFilled(ref this._poly2[64], 64, b1 ? clrDB : clrD);
                winDrawList.AddConvexPolyFilled(ref this._poly2[384], 64, b2 ? clrSelected : clrB);
                winDrawList.AddConvexPolyFilled(ref this._poly2[128], 64, b2 ? clrEB : clrE);
                winDrawList.AddConvexPolyFilled(ref this._poly2[448], 64, b3 ? clrSelected : clrB);
                winDrawList.AddConvexPolyFilled(ref this._poly2[192], 64, b3 ? clrQB : clrQ);

                winDrawList.AddImage(this.PingAttack, uic - new Vector2(78, 78), uic - new Vector2(40, 40));
                winDrawList.AddImage(this.PingDefend, uic - new Vector2(-40, 78), uic - new Vector2(-78, 40));
                winDrawList.AddImage(this.PingExclamation, uic - new Vector2(-40, -40), uic - new Vector2(-78, -78));
                winDrawList.AddImage(this.PingQuestion, uic - new Vector2(78, -40), uic - new Vector2(40, -78));
            }

            for (int i = this.ActivePings.Count - 1; i >= 0; i--)
            {
                Ping p = this.ActivePings[i];
                Vector3 world2 = (p.Position + OpenTK.Mathematics.Vector3.UnitZ).SystemVector();
                Vector3 screen2 = cam.ToScreenspace(world2.GLVector()).SystemVector();
                Vector2 screenxy2 = new Vector2(screen2.X, screen2.Y);
                screenxy2.X = OpenTK.Mathematics.MathHelper.Clamp(screenxy2.X, 16, Client.Instance.Frontend.Width - 16);
                screenxy2.Y = OpenTK.Mathematics.MathHelper.Clamp(screenxy2.Y, 16, Client.Instance.Frontend.Height - 16);

                Texture pTex = p.Type == Ping.PingType.Question ? this.PingQuestion : p.Type == Ping.PingType.Attack ? this.PingAttack : p.Type == Ping.PingType.Exclamation ? this.PingExclamation : p.Type == Ping.PingType.Generic ? this.PingGeneric : this.PingDefend;
                winDrawList.AddImage(pTex, screenxy2 - new Vector2(16, 16), screenxy2 + new Vector2(16, 16));
                string oName = p.OwnerName;
                Vector2 tSize = ImGui.CalcTextSize(oName);
                winDrawList.AddText(screenxy2 + new Vector2(2, 26) - (tSize / 2), Color.Black.Abgr(), oName);
                winDrawList.AddText(screenxy2 + new Vector2(1, 25) - (tSize / 2), Color.White.Abgr(), oName);
                winDrawList.AddText(screenxy2 + new Vector2(0, 24) - (tSize / 2), p.OwnerColor.Abgr(), oName);
            }
        }
    }
}
