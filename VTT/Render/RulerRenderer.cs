namespace VTT.Render
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using VTT.Asset.Obj;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public class RulerRenderer
    {
        public List<RulerInfo> ActiveInfos { get; } = new List<RulerInfo>();

        public RulerInfo CurrentInfo { get; set; }
        public Vector4 CurrentColor { get; set; }
        public string CurrentTooltip { get; set; } = string.Empty;

        public ConcurrentQueue<RulerInfo> InfosToActUpon { get; } = new ConcurrentQueue<RulerInfo>();

        private bool _lmbDown;
        private bool _rmbDown;

        public RulerType CurrentMode { get; set; }
        public float CurrentExtraValue { get; set; }

        public WavefrontObject ModelSphere { get; set; }
        private WavefrontObject ModelArrow { get; set; }
        private WavefrontObject ModelSquare { get; set; }
        private WavefrontObject ModelCube { get; set; }
        private VertexArray _vao;
        private GPUBuffer _vbo;
        private GPUBuffer _ebo;

        private int _updateTicksCtr;

        public Vector3? TerrainHit { get; set; }

        private readonly List<(MapObject, Color)> _highlightedObjects = new List<(MapObject, Color)>();
        public void Update(double delta)
        {
            Map m = Client.Instance.CurrentMap;
            if (m == null)
            {
                return;
            }

            if (this.CurrentColor.Equals(default))
            {
                this.CurrentColor = Extensions.FromArgb(Client.Instance.Settings.Color).Vec4();
            }

            Ray r = Client.Instance.Frontend.Renderer.MapRenderer.RayFromCursor();
            RaycastResut rr = RaycastResut.Raycast(r, m, o => o.MapLayer <= 0);
            this.TerrainHit = rr.Result ? rr.Hit : null;

            bool imMouse = ImGuiNET.ImGui.GetIO().WantCaptureMouse;
            if (!imMouse && Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode == EditMode.Measure)
            {
                if (Client.Instance.Frontend.GameHandle.IsMouseButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left) && !this._lmbDown)
                {
                    this._lmbDown = true;
                    this.CurrentInfo = new RulerInfo() {
                        OwnerID = Client.Instance.ID,
                        OwnerName = Client.Instance.Settings.Name,
                        Tooltip = this.CurrentTooltip,
                        Color = Extensions.FromVec4(this.CurrentColor),
                        Start = this.GetCursorWorldNow(),
                        End = this.GetCursorWorldNow(),
                        ExtraInfo = this.CurrentExtraValue,
                        IsDead = false,
                        NextDeleteTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 1500,
                        SelfID = Guid.NewGuid(),
                        Type = this.CurrentMode
                    };

                    this.ActiveInfos.Add(this.CurrentInfo);
                    this.UpdateCurrentInfo();
                }

                if (this._lmbDown && this.CurrentInfo != null && !this.CurrentInfo.IsDead)
                {
                    Vector3 now = this.GetCursorWorldNow();

                    if (Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftShift) || Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.RightShift))
                    {
                        Plane p = new Plane(-Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Direction, 0f);
                        r = Client.Instance.Frontend.Renderer.MapRenderer.RayFromCursor();
                        Vector3? v = p.Intersect(r, this.CurrentInfo.Start);
                        if (v.HasValue)
                        {
                            now = v.Value;
                        }
                    }

                    if (Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftAlt) || Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.RightAlt))
                    {
                        now = MapRenderer.SnapToGrid(now, m.GridSize);
                    }

                    if (Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftControl) || Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.RightControl))
                    {
                        now = new Vector3(now.X, now.Y, this.CurrentInfo.Start.Z);
                    }

                    if (now != this.CurrentInfo.End)
                    {
                        this.CurrentInfo.End = now;
                    }
                }

                if (Client.Instance.Frontend.GameHandle.IsMouseButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Right) && !this._rmbDown)
                {
                    this._rmbDown = true;
                    if (this.CurrentInfo != null)
                    {
                        RulerInfo clone = new RulerInfo() { SelfID = Guid.NewGuid() };
                        clone.CloneData(this.CurrentInfo);
                        clone.KeepAlive = true;
                        new PacketRulerInfo() { Info = clone }.Send();
                    }
                    else
                    {
                        Vector3 now = this.GetCursorWorldNow();
                        for (int i = this.ActiveInfos.Count - 1; i >= 0; i--)
                        {
                            RulerInfo ri = this.ActiveInfos[i];
                            if (ri.KeepAlive && (ri.OwnerID.Equals(Guid.Empty) || ri.OwnerID.Equals(Client.Instance.ID) || Client.Instance.IsAdmin))
                            {
                                float distance = (now - ri.Start).Length;
                                if (distance <= 0.2f)
                                {
                                    ri.IsDead = true;
                                    ri.KeepAlive = false;
                                    new PacketRulerInfo() { Info = ri }.Send();
                                }
                            }
                        }
                    }
                }

                if (++this._updateTicksCtr >= 10 && this.CurrentInfo != null && !this.CurrentInfo.IsDead)
                {
                    this._updateTicksCtr = 0;
                    this.UpdateCurrentInfo();
                }
            }

            if (!Client.Instance.Frontend.GameHandle.IsMouseButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left) && this._lmbDown)
            {
                this._lmbDown = false;
                if (this.CurrentInfo != null)
                {
                    this.CurrentInfo.IsDead = true;
                    this._updateTicksCtr = 0;
                    this.UpdateCurrentInfo();
                    this.CurrentInfo = null;
                }
            }

            if (!Client.Instance.Frontend.GameHandle.IsMouseButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Right) && this._rmbDown)
            {
                this._rmbDown = false;
            }

            this._highlightedObjects.Clear();
            this.ProcessAllInfos();
        }

        private void UpdateCurrentInfo()
        {
            PacketRulerInfo pri = new PacketRulerInfo() { Info = this.CurrentInfo };
            pri.Send();
            this.CurrentInfo.NextDeleteTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 1500;
        }

        private void ProcessAllInfos()
        {
            Map m = Client.Instance.CurrentMap;
            while (!this.InfosToActUpon.IsEmpty)
            {
                if (this.InfosToActUpon.TryDequeue(out RulerInfo ri))
                {
                    RulerInfo eInfo = this.ActiveInfos.Find(r => r.SelfID.Equals(ri.SelfID));
                    if (eInfo != null)
                    {
                        eInfo.CloneData(ri);
                    }
                    else
                    {
                        if (!ri.IsDead)
                        {
                            this.ActiveInfos.Add(ri);
                        }
                    }
                }
                else
                {
                    break;
                }
            }

            long cUnixTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (m != null)
            {
                for (int i = this.ActiveInfos.Count - 1; i >= 0; i--)
                {
                    RulerInfo ri = this.ActiveInfos[i];
                    if (ri.KeepAlive)
                    {
                        ri.NextDeleteTime = cUnixTime + 1500;
                    }

                    if (ri.IsDead || ri.NextDeleteTime <= cUnixTime)
                    {
                        this.ActiveInfos.RemoveAt(i);
                        continue;
                    }
                    else
                    {
                        switch (ri.Type)
                        {
                            case RulerType.Circle:
                            {
                                float radiusSq = (ri.End.Xy - ri.Start.Xy).LengthSquared;
                                foreach (MapObject mo in m.IterateObjects(Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer))
                                {
                                    BBBox rBB = new BBBox(mo.ClientBoundingBox.Scale(mo.Scale), mo.Rotation);
                                    if (IsInCircle(rBB, mo.Position, ri.Start, radiusSq))
                                    {
                                        this._highlightedObjects.Add((mo, ri.Color));
                                    }
                                }

                                break;
                            }

                            case RulerType.Sphere:
                            {
                                float radiusSq = (ri.End - ri.Start).LengthSquared;
                                foreach (MapObject mo in m.IterateObjects(Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer))
                                {
                                    BBBox rBB = new BBBox(mo.ClientBoundingBox.Scale(mo.Scale), mo.Rotation);
                                    if (IsInSphere(rBB, mo.Position, ri.Start, radiusSq))
                                    {
                                        this._highlightedObjects.Add((mo, ri.Color));
                                    }
                                }

                                break;
                            }

                            case RulerType.Square:
                            {
                                foreach (MapObject mo in m.IterateObjects(Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer))
                                {
                                    BBBox rBB = new BBBox(mo.ClientBoundingBox.Scale(mo.Scale), mo.Rotation);
                                    if (IsInSquare(rBB, mo.Position, ri.Start, ri.End))
                                    {
                                        this._highlightedObjects.Add((mo, ri.Color));
                                    }
                                }

                                break;
                            }

                            case RulerType.Cube:
                            {
                                foreach (MapObject mo in m.IterateObjects(Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer))
                                {
                                    BBBox rBB = new BBBox(mo.ClientBoundingBox.Scale(mo.Scale), mo.Rotation);
                                    if (IsInCube(rBB, mo.Position, ri.Start, ri.End))
                                    {
                                        this._highlightedObjects.Add((mo, ri.Color));
                                    }
                                }

                                break;
                            }

                            case RulerType.Line:
                            {
                                if (ri.ExtraInfo <= 1e-7f)
                                {
                                    break;
                                }

                                foreach (MapObject mo in m.IterateObjects(Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer))
                                {
                                    BBBox rBB = new BBBox(mo.ClientBoundingBox.Scale(mo.Scale), mo.Rotation);
                                    if (IsInLine(rBB, mo.Position, ri.Start, ri.End, ri.ExtraInfo))
                                    {
                                        this._highlightedObjects.Add((mo, ri.Color));
                                    }
                                }

                                break;
                            }

                            case RulerType.Cone:
                            {
                                if (ri.ExtraInfo <= 1e-7f)
                                {
                                    break;
                                }

                                foreach (MapObject mo in m.IterateObjects(Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer))
                                {
                                    BBBox rBB = new BBBox(mo.ClientBoundingBox.Scale(mo.Scale), mo.Rotation);
                                    if (IsInCone(rBB, mo.Position, ri.Start, ri.End, ri.ExtraInfo))
                                    {
                                        this._highlightedObjects.Add((mo, ri.Color));
                                    }
                                }

                                break;
                            }
                        }
                    }
                }
            }
        }

        private readonly List<float> _vertexData = new List<float>();
        private readonly List<uint> _indexData = new List<uint>();

        public void Create()
        {
            this.ModelSphere = OpenGLUtil.LoadModel("arrow_start", VertexFormat.Pos);
            this.ModelArrow = OpenGLUtil.LoadModel("arrow_ptr", VertexFormat.Pos);
            this.ModelSquare = OpenGLUtil.LoadModel("square", VertexFormat.Pos);
            this.ModelCube = OpenGLUtil.LoadModel("cube", VertexFormat.Pos);
            this._vao = new VertexArray();
            this._vbo = new GPUBuffer(BufferTarget.ArrayBuffer);
            this._ebo = new GPUBuffer(BufferTarget.ElementArrayBuffer);
            this._vao.Bind();
            this._vbo.Bind();
            this._vbo.SetData<float>(IntPtr.Zero, 16);
            this._ebo.Bind();
            this._ebo.SetData<uint>(IntPtr.Zero, 24);
            this._vao.Reset();
            this._vao.SetVertexSize<float>(3);
            this._vao.PushElement(ElementType.Vec3);
        }

        public void Render(double time)
        {
            Map m = Client.Instance.CurrentMap;
            if (m == null)
            {
                return;
            }

            ShaderProgram shader = Client.Instance.Frontend.Renderer.ObjectRenderer.OverlayShader;
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            shader.Bind();
            shader["view"].Set(cam.View);
            shader["projection"].Set(cam.Projection);
            Matrix4 model;
            foreach (RulerInfo ri in this.ActiveInfos)
            {
                model = Matrix4.CreateScale(0.2f) * Matrix4.CreateTranslation(ri.Start);
                shader["model"].Set(model);
                shader["u_color"].Set(ri.Color.Vec4());
                this.ModelSphere.Render();
                Vector3 vE2S = (ri.End - ri.Start).Normalized();
                Vector3 a = Vector3.Cross(Vector3.UnitY, vE2S);
                Quaternion q = new Quaternion(a, 1 + Vector3.Dot(Vector3.UnitY, vE2S)).Normalized();

                if (!ri.KeepAlive || (ri.Type == RulerType.Ruler && (ri.End - ri.Start).Length > 0.2f))
                {
                    model = Matrix4.CreateScale(0.2f) * Matrix4.CreateFromQuaternion(q) * Matrix4.CreateTranslation(ri.End);
                    shader["model"].Set(model);
                    this.ModelArrow.Render();
                    this.CreateLine(ri.Start, ri.End);
                    this.UploadBuffers();
                    this._vao.Bind();
                    shader["model"].Set(Matrix4.Identity);
                    GL.Disable(EnableCap.CullFace);
                    GL.DrawElements(PrimitiveType.Triangles, this._indexData.Count, DrawElementsType.UnsignedInt, IntPtr.Zero);
                }
                else
                { 
                    shader["model"].Set(Matrix4.Identity);
                    GL.Disable(EnableCap.CullFace);
                }

                this._vertexData.Clear();
                this._indexData.Clear();
                GL.Enable(EnableCap.Blend);
                GL.Enable(EnableCap.Multisample);
                GL.Enable(EnableCap.SampleAlphaToCoverage);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                switch (ri.Type)
                {
                    case RulerType.Circle:
                    {
                        this.CreateCircle(ri.Start, (ri.End - ri.Start).Length);
                        this.UploadBuffers();
                        shader["u_color"].Set(ri.Color.Vec4() * new Vector4(1, 1, 1, 0.5f));
                        this._vao.Bind();
                        GL.DrawElements(PrimitiveType.Triangles, this._indexData.Count, DrawElementsType.UnsignedInt, IntPtr.Zero);
                        this._vertexData.Clear();
                        this._indexData.Clear();
                        break;
                    }

                    case RulerType.Sphere:
                    {
                        float radius = (ri.End - ri.Start).Length;
                        model = Matrix4.CreateScale(radius * 2) * Matrix4.CreateTranslation(ri.Start);
                        shader["model"].Set(model);
                        shader["u_color"].Set(ri.Color.Vec4() * new Vector4(1, 1, 1, 0.5f));
                        GL.Enable(EnableCap.CullFace);
                        GL.CullFace(CullFaceMode.Front);
                        this.ModelSphere.Render();
                        GL.CullFace(CullFaceMode.Back);
                        this.ModelSphere.Render();
                        GL.Disable(EnableCap.CullFace);
                        break;
                    }

                    case RulerType.Square:
                    {
                        Vector3 vE2Snn = (ri.End - ri.Start);
                        float r = MathF.Max(MathF.Abs(vE2Snn.X), MathF.Abs(vE2Snn.Y));
                        Vector3 sStart = ri.Start;
                        sStart.Z = MathF.Min(sStart.Z, ri.End.Z);
                        float z = MathF.Max(ri.Start.Z, ri.End.Z) + 1.0f;
                        float hZ = MathF.Max(1, MathF.Abs(z - sStart.Z));
                        model = Matrix4.CreateScale(r * 2, r * 2, hZ * 2) * Matrix4.CreateTranslation(sStart);
                        shader["model"].Set(model);
                        shader["u_color"].Set(ri.Color.Vec4() * new Vector4(1, 1, 1, 0.5f));
                        this.ModelSquare.Render();
                        break;
                    }

                    case RulerType.Cube:
                    {
                        Vector3 vE2Snn = (ri.End - ri.Start);
                        float r = MathF.Max(MathF.Max(MathF.Abs(vE2Snn.X), MathF.Abs(vE2Snn.Y)), MathF.Abs(vE2Snn.Z));
                        model = Matrix4.CreateScale(r * 2) * Matrix4.CreateTranslation(ri.Start);
                        shader["model"].Set(model);
                        shader["u_color"].Set(ri.Color.Vec4() * new Vector4(1, 1, 1, 0.5f));
                        GL.Enable(EnableCap.CullFace);
                        GL.CullFace(CullFaceMode.Front);
                        this.ModelCube.Render();
                        GL.CullFace(CullFaceMode.Back);
                        this.ModelCube.Render();
                        GL.Disable(EnableCap.CullFace);
                        break;
                    }

                    case RulerType.Line:
                    {
                        Vector3 vE2Snn = (ri.End - ri.Start);
                        float gFac = m.GridUnit;
                        model = Matrix4.CreateScale(ri.ExtraInfo / gFac, vE2Snn.Length, ri.ExtraInfo / gFac) * Matrix4.CreateFromQuaternion(q) * Matrix4.CreateTranslation(ri.Start + ((ri.End - ri.Start) / 2));
                        shader["model"].Set(model);
                        shader["u_color"].Set(ri.Color.Vec4() * new Vector4(1, 1, 1, 0.5f));
                        GL.Enable(EnableCap.CullFace);
                        GL.CullFace(CullFaceMode.Front);
                        this.ModelCube.Render();
                        GL.CullFace(CullFaceMode.Back);
                        this.ModelCube.Render();
                        GL.Disable(EnableCap.CullFace);
                        break;
                    }

                    case RulerType.Cone:
                    {
                        this.CreateCone(ri.Start, ri.End, ri.ExtraInfo / m.GridUnit);
                        this.UploadBuffers();
                        shader["u_color"].Set(ri.Color.Vec4() * new Vector4(1, 1, 1, 0.5f));
                        this._vao.Bind();
                        GL.DrawElements(PrimitiveType.Triangles, this._indexData.Count, DrawElementsType.UnsignedInt, IntPtr.Zero);
                        this._vertexData.Clear();
                        this._indexData.Clear();
                        break;
                    }
                }

                GL.Disable(EnableCap.Blend);
                GL.Disable(EnableCap.SampleAlphaToCoverage);
                GL.Disable(EnableCap.Multisample);
                GL.Enable(EnableCap.CullFace);
            }

            foreach ((MapObject, Color) d in this._highlightedObjects)
            {
                d.Item1.ClientRulerRendererAccumData = 0;
            }

            foreach ((MapObject, Color) d in this._highlightedObjects)
            {
                float scaleMod = 1.0f + (d.Item1.ClientRulerRendererAccumData++ / 64.0f);
                Client.Instance.Frontend.Renderer.ObjectRenderer.RenderHighlightBox(d.Item1, d.Item2, scaleMod);
            }
        }

        private readonly List<Vector2> _groundQuadGenTempList = new List<Vector2>();

        private static Vector3 ArbitraryOrthogonal(Vector3 vec)
        {
            bool b0 = (vec.X < vec.Y) && (vec.X < vec.Z);
            bool b1 = (vec.Y <= vec.X) && (vec.Y < vec.Z);
            bool b2 = (vec.Z <= vec.X) && (vec.Z <= vec.Y);
            return Vector3.Cross(vec, new Vector3(b0 ? 1 : 0, b1 ? 1 : 0, b2 ? 1 : 0));
        }

        public void CreateCone(Vector3 start, Vector3 end, float radius)
        {
            Vector3 planeNormal = (end - start).Normalized();
            if (float.IsNaN(planeNormal.X) || float.IsNaN(planeNormal.Y) || float.IsNaN(planeNormal.Z))
            {
                return;
            }

            Vector4 planePerpendicular = new Vector4(ArbitraryOrthogonal(planeNormal), 1.0f);
            this._vertexData.Add(start.X);
            this._vertexData.Add(start.Y);
            this._vertexData.Add(start.Z);
            this._vertexData.Add(end.X);
            this._vertexData.Add(end.Y);
            this._vertexData.Add(end.Z);
            for (int i = 0; i < 36; ++i)
            {
                float angleRad = MathHelper.DegreesToRadians(i * 10f);
                Quaternion q = Quaternion.FromAxisAngle(planeNormal, angleRad);
                Vector4 v = q * planePerpendicular;
                Vector3 v3 = end + (v.Xyz.Normalized() * radius);
                this._vertexData.Add(v3.X);
                this._vertexData.Add(v3.Y);
                this._vertexData.Add(v3.Z);
            }

            for (uint i = 0; i < 36; ++i)
            {
                this._indexData.Add(0);
                this._indexData.Add(2u + i);
                this._indexData.Add(2u + ((i + 1u) % 36));
            }

            for (uint i = 0; i < 36; ++i)
            {
                this._indexData.Add(1);
                this._indexData.Add(2u + i);
                this._indexData.Add(2u + ((i + 1u) % 36));
            }
        }

        public void CreateCircle(Vector3 start, float radius)
        {
            float zHeight = start.Z;
            zHeight += 1.0f * MathF.Sign(zHeight);
            if (zHeight is <= float.Epsilon or >= (-float.Epsilon))
            {
                zHeight = 1;
            }

            float rEffect = radius / 5.0f;
            if (zHeight < -float.Epsilon)
            {
                rEffect = -rEffect;
            }

            zHeight += rEffect;
            start *= new Vector3(1, 1, 0); // Discard Z component
            int numSegments = (int)(12 * MathF.Max(1, (radius / 2.5f)));
            float angleStep = MathHelper.DegreesToRadians(360.0f / numSegments);
            float cos = MathF.Cos(angleStep);
            float sin = MathF.Sin(angleStep);
            Vector3 v = Vector3.UnitY;
            this._groundQuadGenTempList.Clear();
            for (int i = 0; i < numSegments; ++i)
            {
                float dX = (v.X * cos) - (v.Y * sin);
                float dY = (v.X * sin) + (v.Y * cos);
                v = new Vector3(dX, dY, 0);
                this._groundQuadGenTempList.Add(v.Xy);

                this._vertexData.Add(start.X + (v.X * radius));
                this._vertexData.Add(start.Y + (v.Y * radius));
                this._vertexData.Add(0);

                this._vertexData.Add(start.X + (v.X * radius));
                this._vertexData.Add(start.Y + (v.Y * radius));
                this._vertexData.Add(zHeight);

                uint cB = (uint)i * 2;
                uint cT = cB + 1;
                uint nB = (uint)((cB + 2) % (numSegments * 2));
                uint nT = nB + 1;

                this._indexData.Add(cB);
                this._indexData.Add(cT);
                this._indexData.Add(nB);

                this._indexData.Add(cT);
                this._indexData.Add(nB);
                this._indexData.Add(nT);
            }

            int index0 = this._vertexData.Count / 3;
            for (int i = 0; i < numSegments; ++i)
            {
                Vector2 current = this._groundQuadGenTempList[i] * radius;
                Vector2 prev = i == 0 ? this._groundQuadGenTempList[^1] : this._groundQuadGenTempList[i - 1];
                Vector2 next = i == numSegments - 1 ? this._groundQuadGenTempList[0] : this._groundQuadGenTempList[i + 1];
                Vector2 c2n = next - current;
                Vector2 p2c = prev - current;
                Vector2 l1 = c2n.PerpendicularLeft;
                Vector2 l2 = p2c.PerpendicularRight;
                Vector2 l = Vector2.Lerp(l1, l2, 0.5f).Normalized();

                Vector3 v1 = start + new Vector3(current.X, current.Y, 0) + (new Vector3(l.X, l.Y, 0) * 0.2f);
                Vector3 v2 = start + new Vector3(current.X, current.Y, 0) - (new Vector3(l.X, l.Y, 0) * 0.2f);
                Vector3 v3 = start + new Vector3(current.X, current.Y, zHeight) + (new Vector3(l.X, l.Y, 0) * 0.2f);
                Vector3 v4 = start + new Vector3(current.X, current.Y, zHeight) - (new Vector3(l.X, l.Y, 0) * 0.2f);

                this.AddVertices(v1, v2, v3, v4);

                uint cBl = (uint)(index0 + (i * 4));
                uint cBr = cBl + 1;
                uint cTl = cBl + 2;
                uint cTr = cBl + 3;

                uint nBl = (uint)(index0 + (((i * 4) + 4) % (numSegments * 4)));
                uint nBr = nBl + 1;
                uint nTl = nBl + 2;
                uint nTr = nBl + 3;

                this._indexData.Add(cBl);
                this._indexData.Add(cBr);
                this._indexData.Add(nBl);

                this._indexData.Add(cBr);
                this._indexData.Add(nBr);
                this._indexData.Add(nBl);

                this._indexData.Add(cTl);
                this._indexData.Add(cTr);
                this._indexData.Add(nTl);

                this._indexData.Add(cTr);
                this._indexData.Add(nTr);
                this._indexData.Add(nTl);
            }
        }

        public void CreateLine(Vector3 start, Vector3 end)
        {
            Vector3 vE2S = (end - start).Normalized();
            Vector3 a = Vector3.Cross(Vector3.UnitX, vE2S);
            Quaternion qZ = new Quaternion(a, 1 + Vector3.Dot(Vector3.UnitX, vE2S)).Normalized();

            Vector3 oZ = (qZ * new Vector4(0, 0, 1, 1)).Xyz.Normalized() * 0.03f;
            Vector3 oX = Vector3.Cross(vE2S, oZ).Normalized() * 0.03f;

            end -= vE2S * 0.1f;

            Vector3 v1 = start + oX + oZ; // 0
            Vector3 v2 = start + oX - oZ; // 1
            Vector3 v3 = start - oX + oZ; // 2
            Vector3 v4 = start - oX - oZ; // 3
            Vector3 v5 = end + oX + oZ;   // 4
            Vector3 v6 = end + oX - oZ;   // 5
            Vector3 v7 = end - oX + oZ;   // 6
            Vector3 v8 = end - oX - oZ;   // 7

            this.AddVertices(v1, v2, v3, v4, v5, v6, v7, v8);
            this._indexData.Add(0);
            this._indexData.Add(1);
            this._indexData.Add(4);
            this._indexData.Add(1);
            this._indexData.Add(4);
            this._indexData.Add(5);

            this._indexData.Add(2);
            this._indexData.Add(3);
            this._indexData.Add(6);
            this._indexData.Add(3);
            this._indexData.Add(6);
            this._indexData.Add(7);

            this._indexData.Add(0);
            this._indexData.Add(2);
            this._indexData.Add(4);
            this._indexData.Add(2);
            this._indexData.Add(4);
            this._indexData.Add(6);

            this._indexData.Add(1);
            this._indexData.Add(3);
            this._indexData.Add(5);
            this._indexData.Add(3);
            this._indexData.Add(5);
            this._indexData.Add(7);
        }

        public void AddVertices(params Vector3[] vecs)
        {
            foreach (Vector3 v in vecs)
            {
                this._vertexData.Add(v.X);
                this._vertexData.Add(v.Y);
                this._vertexData.Add(v.Z);
            }
        }

        public void UploadBuffers()
        {
            this._vao.Bind();
            this._vbo.Bind();
            this._vbo.SetData(this._vertexData.ToArray());
            this._ebo.Bind();
            this._ebo.SetData(this._indexData.ToArray());
        }

        public Vector3 GetCursorWorldNow()
        {
            if (this.TerrainHit.HasValue)
            {
                return this.TerrainHit.Value;
            }

            if (Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld.HasValue)
            {
                return Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld.Value;
            }

            Ray r = Client.Instance.Frontend.Renderer.MapRenderer.RayFromCursor();
            return r.Origin + (r.Direction * 5.0f);
        }

        private static bool IsInCircle(BBBox box, Vector3 offset, Vector3 point, float rSq)
        {
            Vector2 cPoint = point.Xy - offset.Xy;
            Vector3 bRnd = box.Rotation * box.Start;
            if ((bRnd.Xy - cPoint).LengthSquared <= rSq)
            {
                return true;
            }

            foreach (Vector3 v in box)
            {
                if ((v.Xy - cPoint).LengthSquared <= rSq)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInSphere(BBBox box, Vector3 offset, Vector3 point, float rSq)
        {
            point -= offset;
            Vector3 bRnd = box.Rotation * box.Start;
            if ((bRnd - point).LengthSquared <= rSq)
            {
                return true;
            }

            foreach (Vector3 v in box)
            {
                if ((v - point).LengthSquared <= rSq)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInSquare(BBBox box, Vector3 offset, Vector3 start, Vector3 end)
        {
            start -= offset;
            end -= offset;
            float w = MathF.Abs(end.X - start.X);
            float h = MathF.Abs(end.Y - start.Y);
            float mv = MathF.Max(w, h);
            RectangleF rect = new RectangleF(start.X - mv, start.Y - mv, mv * 2, mv * 2);
            Vector3 bRnd = box.Rotation * box.Start;
            if (rect.Contains(bRnd.X, bRnd.Y))
            {
                return true;
            }

            foreach (Vector3 v in box)
            {
                if (rect.Contains(v.X, v.Y))
                {
                    return true;
                } 
            }

            return false;
        }

        private static bool IsInCube(BBBox box, Vector3 offset, Vector3 start, Vector3 end)
        {
            start -= offset;
            end -= offset;
            float w = MathF.Abs(end.X - start.X);
            float l = MathF.Abs(end.Y - start.Y);
            float h = MathF.Abs(end.Z - start.Z);
            float mv = MathF.Max(h, MathF.Max(w, l));

            AABox cBox = new AABox(start.X - mv, start.Y - mv, start.Z - mv, start.X + mv, start.Y + mv, start.Z + mv);
            Vector3 bRnd = box.Rotation * box.Start;
            if (cBox.Contains(bRnd))
            {
                return true;
            }

            foreach (Vector3 v in box)
            {
                if (cBox.Contains(v))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsInLine(BBBox box, Vector3 offset, Vector3 start, Vector3 end, float radius)
        {
            Vector3 vE2Snn = end - start;
            float gFac = Client.Instance.CurrentMap?.GridUnit ?? 5;

            Vector3 vE2S = vE2Snn.Normalized();
            Vector3 a = Vector3.Cross(Vector3.UnitY, vE2S);

            AABox originBox = new AABox(-0.5f, -0.5f, -0.5f, 0.5f, 0.5f, 0.5f);
            Vector3 scale = new Vector3(radius / gFac, vE2Snn.Length, radius / gFac);
            Quaternion rotation = new Quaternion(a, 1 + Vector3.Dot(Vector3.UnitY, vE2S)).Normalized();
            Vector3 translation = start + (vE2Snn / 2);
            Matrix4 model = Matrix4.CreateScale(scale) * Matrix4.CreateFromQuaternion(rotation) * Matrix4.CreateTranslation(translation);
            model.Invert();
            foreach (Vector3 v in box)
            {
                Vector4 vT = new Vector4(v + offset, 1.0f) * model;
                if (originBox.Contains(vT.Xyz / vT.W))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsInCone(BBBox box, Vector3 offset, Vector3 start, Vector3 end, float radius)
        {
            Vector3 v = end - start;
            float gFac = Client.Instance.CurrentMap?.GridUnit ?? 5;
            radius /= gFac;

            foreach (Vector3 c in box)
            {
                if (IsPointInCone(offset + c, start, v.Normalized(), v.Length, radius))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPointInCone(Vector3 point, Vector3 start, Vector3 dir, float len, float rad)
        {
            float cDist = Vector3.Dot(point - start, dir);
            if (cDist < 0 || cDist > len)
            {
                return false;
            }

            float cRad = cDist / len * rad;
            float orthoDist = (point - start - (cDist * dir)).Length;
            return orthoDist < cRad;
        }
    }

    public enum RulerType
    {
        Ruler,  // X ------> Y
        Circle, // From point XY circle
        Sphere, // From point 3d sphere
        Square, // From point XY quad
        Cube,   // From point 3d cube
        Line,   // From point 3d rectangle, ExtraInfo = radius
        Cone
    }
}
