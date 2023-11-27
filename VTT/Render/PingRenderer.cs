namespace VTT.Render
{
    using ImGuiNET;
    using OpenTK.Graphics.OpenGL;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using VTT.Asset.Obj;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Sound;
    using VTT.Util;

    public class PingRenderer
    {
        public Texture PingGeneric { get; set; }
        public Texture PingAttack { get; set; }
        public Texture PingDefend { get; set; }
        public Texture PingExclamation { get; set; }
        public Texture PingQuestion { get; set; }

        public ShaderProgram EmojiShader { get; set; }
        public Texture[] EmojiTextures { get; set; }

        public WavefrontObject ModelPing { get; set; }

        public GPUBuffer QuadVBO { get; set; }
        public GPUBuffer QuadEBO { get; set; }
        public VertexArray QuadVAO { get; set; }

        private readonly object _lock = new object();
        public List<Ping> ActivePings { get; } = new List<Ping>();

        public Stopwatch CPUTimer { get; } = new Stopwatch();

        public void Create()
        {
            this.PingDefend = OpenGLUtil.LoadUIImage("icons8-security-lock-80");
            this.PingAttack = OpenGLUtil.LoadUIImage("icons8-sword-80");
            this.PingGeneric = OpenGLUtil.LoadUIImage("icons8-double-down-80");
            this.PingExclamation = OpenGLUtil.LoadUIImage("icons8-box-important-80");
            this.PingQuestion = OpenGLUtil.LoadUIImage("icons8-help-80");
            this.ModelPing = OpenGLUtil.LoadModel("ping_lower", VertexFormat.Pos);
            this.EmojiTextures = new Texture[12];
            this.EmojiShader = OpenGLUtil.LoadShader("emojiping", ShaderType.VertexShader, ShaderType.FragmentShader);
            for (int i = 0; i < 12; ++i)
            {
                this.EmojiTextures[i] = OpenGLUtil.LoadUIImage("emoji-" + (Ping.PingType.Smiling + i).ToString().ToLower());
            }

            void AddTrapesoid(int index, float outerAngleRadStart, float innerAngleRadStart, float radStart, float radEnd, float angleMul = 2.5f)
            {
                for (int i = 0; i < 32; ++i)
                {
                    float d2r = outerAngleRadStart + (i * angleMul * MathF.PI / 180);
                    float ca = MathF.Cos(d2r);
                    float sa = MathF.Sin(d2r);
                    float d2r2 = innerAngleRadStart + (i * (angleMul * 1.075f) * MathF.PI / 180);
                    float ca2 = MathF.Cos(d2r2);
                    float sa2 = MathF.Sin(d2r2);
                    Vector2 vl = new Vector2(-ca, -sa);
                    Vector2 vl2 = new Vector2(-ca2, -sa2);
                    this._poly[index + i] = (Vector2.Normalize(vl) * (radStart + 3));
                    this._poly[index + 63 - i] = (vl * radEnd);
                    this._poly[index + 256 + i] = (Vector2.Normalize(vl2) * radStart);
                    this._poly[index + 319 - i] = (vl2 * (radEnd + 3));
                }
            }

            AddTrapesoid(0, 0.0872664626f, 0.0349066f, 40, 128); // [2-5]
            AddTrapesoid(64, 1.65806279f, 1.6057f, 40, 128);
            AddTrapesoid(128, 3.22886f, 3.1765f, 40, 128);
            AddTrapesoid(192, 4.79966f, 4.7473f, 40, 128);

            AddTrapesoid(512, 0.0872664626f, 0.0349066f, 40, 92);
            AddTrapesoid(512 + 64, 1.65806279f, 1.6057f, 40, 92);
            AddTrapesoid(512 + 128, 3.22886f, 3.1765f, 40, 92);
            AddTrapesoid(512 + 192, 4.79966f, 4.7473f, 40, 92);

            AddTrapesoid(1024, 0.0872664626f, 0.0349066f + 0.0349066f, 108, 160, 1.25f);
            AddTrapesoid(1024 + 64, 1.65806279f, 1.6057f + 0.0349066f, 108, 160, 1.25f);
            AddTrapesoid(1024 + 128, 3.22886f, 3.1765f + 0.0349066f, 108, 160, 1.25f);
            AddTrapesoid(1024 + 192, 4.79966f, 4.7473f + 0.0349066f, 108, 160, 1.25f);
            AddTrapesoid(1536, 0.0872664626f + 0.785398f, 0.0349066f + 0.0349066f + 0.785398f, 108, 160, 1.25f);
            AddTrapesoid(1536 + 64, 1.65806279f + 0.785398f, 1.6057f + 0.0349066f + 0.785398f, 108, 160, 1.25f);
            AddTrapesoid(1536 + 128, 3.22886f + 0.785398f, 3.1765f + 0.0349066f + 0.785398f, 108, 160, 1.25f);
            AddTrapesoid(1536 + 192, 4.79966f + 0.785398f, 4.7473f + 0.0349066f + 0.785398f, 108, 160, 1.25f);

            this.QuadVAO = new VertexArray();
            this.QuadVAO.Bind();
            this.QuadVBO = new GPUBuffer(BufferTarget.ArrayBuffer, BufferUsageHint.StaticDraw);
            this.QuadVBO.Bind();
            this.QuadVBO.SetData(new float[] 
            { 
                -1, -1, 0, 1,
                1, -1, 1, 1,
                1, 1, 1, 0,
                -1, 1, 0, 0
            });

            this.QuadEBO = new GPUBuffer(BufferTarget.ElementArrayBuffer, BufferUsageHint.StaticDraw);
            this.QuadEBO.Bind();
            this.QuadEBO.SetData(new uint[] {
                0, 1, 2, 0, 2, 3
            });

            this.QuadVAO.SetVertexSize(4 * sizeof(float));
            this.QuadVAO.PushElement(ElementType.Vec2);
            this.QuadVAO.PushElement(ElementType.Vec2);
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
                p.DeathTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + (p.IsEmote() ? 4000 : 10000);
                this.ActivePings.Add(p);
                if (Client.Instance.Settings.EnableSoundPing && !p.IsEmote())
                {
                    Client.Instance.Frontend.Sound.PlaySound(Client.Instance.Frontend.Sound.PingAny, SoundCategory.MapFX);
                }
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
        private bool _emojiUI;
        private OpenTK.Mathematics.Vector3 _pingUIAnchor;
        public void BeginPingUI(bool emoji)
        {
            this._pingUI = true;
            this._emojiUI = emoji;
            this._pingUIAnchor = Client.Instance.Frontend.Renderer.RulerRenderer.TerrainHit ?? Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld ?? (Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Position + (Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Direction * 5));
        }

        public void EndPingUI()
        {
            if (this._pingUI)
            {
                Vector3 c = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.ToScreenspace(this._pingUIAnchor).SystemVector();
                Vector2 uic = new Vector2(c.X, c.Y);
                Vector2 mouseC = ImGui.GetMousePos();

                if (!this._emojiUI)
                {
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
                else
                {
                    for (int i = 0; i < 12; ++i)
                    {
                        int iOffset = 512 * (i >> 2);
                        bool mOver = this.MouseOverPolygon(mouseC, 512 + iOffset + ((i & 3) * 64));
                        if (mOver)
                        {
                            Console.WriteLine("Have a ping of index " + i + " and type " + (Ping.PingType.Smiling + i));
                            Ping p = new Ping() { DeathTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 4000, OwnerColor = Extensions.FromArgb(Client.Instance.Settings.Color), OwnerID = Client.Instance.ID, OwnerName = Client.Instance.Settings.Name, Position = this._pingUIAnchor, Type = Ping.PingType.Smiling + i };
                            new PacketPing() { Ping = p }.Send();
                            break;
                        }
                    }
                }
            }

            this._pingUI = false;
            this._emojiUI = false;
        }

        public void Render(double time)
        {
            this.CPUTimer.Restart();

            if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
            {
                GL.Enable(EnableCap.Multisample);
                GL.Enable(EnableCap.SampleAlphaToCoverage);
            }

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;

            // Normal pings
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

                if (p.IsEmote())
                {
                    continue;
                }

                float scale = MathF.Max(0.00001f, (((p.DeathTime - now) % 2000) - 500) / 500f);
                float a = MathF.Min(1, MathF.Abs(1.0f / scale));
                shader["model"].Set(OpenTK.Mathematics.Matrix4.CreateScale(scale) * OpenTK.Mathematics.Matrix4.CreateTranslation(p.Position));
                shader["u_color"].Set(p.OwnerColor.Vec4() * new OpenTK.Mathematics.Vector4(1, 1, 1, a));
                this.ModelPing.Render();
            }

            GL.Disable(EnableCap.DepthTest);
            shader = this.EmojiShader;
            shader.Bind();
            shader["view"].Set(cam.View);
            shader["projection"].Set(cam.Projection);
            shader["u_color"].Set(new OpenTK.Mathematics.Vector4(1, 1, 1, 1));
            shader["screenSize"].Set(new OpenTK.Mathematics.Vector2(1f / Client.Instance.Frontend.Width, 1f / Client.Instance.Frontend.Height));
            this.QuadVAO.Bind();
            for (int i = this.ActivePings.Count - 1; i >= 0; i--)
            {
                Ping p = this.ActivePings[i];
                if (!p.IsEmote())
                {
                    continue;
                }

                float lifetimeProgression = 1.0f - MathF.Max(0.00001f, (p.DeathTime - now) / 4000f);
                bool is2D = Client.Instance.Frontend.Renderer.MapRenderer.IsOrtho;
                float zOffset = lifetimeProgression * 1.25f;
                float sizeInfluence = MathF.Sin(lifetimeProgression * MathF.PI) * (1 - MathF.Pow(lifetimeProgression, 4));
                float a = MathF.Min(1, 1 - MathF.Pow(lifetimeProgression - 0.4f, 3) * 4.6f);
                shader["position"].Set(new OpenTK.Mathematics.Vector4(p.Position + new OpenTK.Mathematics.Vector3(0, is2D ? zOffset : 0, is2D ? 0 : zOffset), 32 + 32 * sizeInfluence));
                shader["u_color"].Set(new OpenTK.Mathematics.Vector4(1, 1, 1, a));
                this.EmojiTextures[p.Type - Ping.PingType.Smiling].Bind();
                GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);
            }

            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.Blend);
            if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
            {
                GL.Disable(EnableCap.SampleAlphaToCoverage);
                GL.Disable(EnableCap.Multisample);
            }

            this.CPUTimer.Stop();
        }

        private readonly Vector2[] _poly = new Vector2[2048];
        private readonly Vector2[] _poly2 = new Vector2[2048];
        private readonly Vector2[] _emojiPositions = new Vector2[] 
        {
            new Vector2(45, 43),
            new Vector2(-43, 45),
            new Vector2(-45, -43),
            new Vector2(43, -45),

            new Vector2(121, 55),
            new Vector2(-55, 121),
            new Vector2(-121,-55),
            new Vector2(55, -121),
            new Vector2(47, 124),
            new Vector2(-124, 47),
            new Vector2(-47, -124),
            new Vector2(124, -47)
        };

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

                if (!this._emojiUI)
                {
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
                else
                {
                    for (int i = 512; i < 2048; ++i)
                    {
                        this._poly2[i] = this._poly[i] + uic;
                    }

                    Vector2 GetPolygonCenter(int offset)
                    {
                        Vector2 cumulativeVec = default;
                        for (int i = 0; i < 64; ++i)
                        {
                            cumulativeVec += this._poly[offset + i];
                        }

                        return cumulativeVec / 64;
                    }

                    for (int i = 0; i < 12; ++i)
                    {
                        int iOffset = 512 * (i >> 2);
                        bool mOver = this.MouseOverPolygon(mouseC, 512 + iOffset + ((i & 3) * 64));
                        winDrawList.AddConvexPolyFilled(ref this._poly2[iOffset + 512 + 256 + ((i & 3) * 64)], 64, mOver ? clrSelected : clrB);
                        winDrawList.AddConvexPolyFilled(ref this._poly2[iOffset + 512 + ((i & 3) * 64)], 64, mOver ? clrGBright : clrG);
                    }

                    for (int i = 0; i < 12; ++i)
                    {
                        Vector2 ePos = this._emojiPositions[i];
                        winDrawList.AddImage(this.EmojiTextures[i], uic - ePos - new Vector2(24, 24), uic - ePos + new Vector2(24, 24));
                    }
                }
            }

            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            for (int i = this.ActivePings.Count - 1; i >= 0; i--)
            {
                Ping p = this.ActivePings[i];
                if (p.IsEmote())
                {
                    continue;
                }

                Vector3 world2 = (p.Position + OpenTK.Mathematics.Vector3.UnitZ).SystemVector();
                Vector3 screen2 = cam.ToScreenspace(world2.GLVector()).SystemVector();
                Vector2 screenxy2 = new Vector2(screen2.X, screen2.Y);
                bool anyOOB = screenxy2.X < 16 || screenxy2.X > Client.Instance.Frontend.Width - 16 ||
                                screenxy2.Y < 16 || screenxy2.Y > Client.Instance.Frontend.Height - 16;
                screenxy2.X = OpenTK.Mathematics.MathHelper.Clamp(screenxy2.X, 16, Client.Instance.Frontend.Width - 16);
                screenxy2.Y = OpenTK.Mathematics.MathHelper.Clamp(screenxy2.Y, 16, Client.Instance.Frontend.Height - 16);

                Texture pTex = p.Type == Ping.PingType.Question ? this.PingQuestion : p.Type == Ping.PingType.Attack ? this.PingAttack : p.Type == Ping.PingType.Exclamation ? this.PingExclamation : p.Type == Ping.PingType.Generic ? this.PingGeneric : this.PingDefend;
                winDrawList.AddImage(pTex, screenxy2 - new Vector2(16, 16), screenxy2 + new Vector2(16, 16));

                if (anyOOB)
                {
                    long delta = p.DeathTime - now;
                    if (delta > 0)
                    {
                        for (int j = 0; j < 3; ++j)
                        {
                            long deltaMod = (delta + (j * 500)) % 3000;
                            float deltaF = deltaMod / 3000.0f;

                            Vector2 sVec = new Vector2(16, 16);
                            sVec += new Vector2(64, 64) * (deltaF < 0.5 ? 2 * deltaF * deltaF : 1 - (MathF.Pow((-2 * deltaF) + 2, 2) / 2));
                            winDrawList.AddImage(pTex, screenxy2 - sVec, screenxy2 + sVec);
                        }
                    }
                }

                string oName = p.OwnerName;
                Vector2 tSize = ImGuiHelper.CalcTextSize(oName);
                winDrawList.AddText(screenxy2 + new Vector2(2, 26) - (tSize / 2), Color.Black.Abgr(), oName);
                winDrawList.AddText(screenxy2 + new Vector2(1, 25) - (tSize / 2), Color.White.Abgr(), oName);
                winDrawList.AddText(screenxy2 + new Vector2(0, 24) - (tSize / 2), p.OwnerColor.Abgr(), oName);
            }
        }
    }
}
