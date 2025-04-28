namespace VTT.Render
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Numerics;
    using VTT.Asset.Obj;
    using VTT.Control;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.GLFW;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;
    using Plane = Util.Plane;

    public class RulerRenderer
    {
        public const float LineThickness = 0.02f;
        public const float StartEndCapScale = 0.15f;

        public List<RulerInfo> ActiveInfos { get; } = new List<RulerInfo>();

        public RulerInfo CurrentInfo { get; set; }
        public Vector4 CurrentColor { get; set; }
        public string CurrentTooltip { get; set; } = string.Empty;
        public bool RulersDisplayInfo { get; set; } = true;
        public Guid CurrentEraserMask { get; set; } = Guid.Empty;

        public ConcurrentQueue<RulerInfo> InfosToActUpon { get; } = new ConcurrentQueue<RulerInfo>();

        private bool _lmbDown;
        private bool _rmbDown;

        public RulerType CurrentMode { get; set; }
        public float CurrentExtraValue { get; set; }

        public WavefrontObject ModelSphere { get; set; }
        private WavefrontObject ModelArrow { get; set; }
        private WavefrontObject ModelCube { get; set; }
        private VertexArray _vao;
        private GPUBuffer _vbo;
        private GPUBuffer _ebo;

        private int _updateTicksCtr;


        private readonly List<(MapObject, Color)> _highlightedObjects = new List<(MapObject, Color)>();

        public Stopwatch CPUTimer { get; } = new Stopwatch();

        public void Update()
        {
            Map m = Client.Instance.CurrentMap;
            if (m == null)
            {
                return;
            }

            Vector3 tHitOrPt = Client.Instance.Frontend.Renderer.MapRenderer.GetTerrainCursorOrPointAlongsideView();
            if (this.CurrentColor.Equals(default))
            {
                this.CurrentColor = Extensions.FromArgb(Client.Instance.Settings.Color).Vec4();
            }

            bool imMouse = ImGuiNET.ImGui.GetIO().WantCaptureMouse;
            if (!imMouse && Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode == EditMode.Measure)
            {
                if (Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Left) && !this._lmbDown)
                {
                    this._lmbDown = true;
                    if (this.CurrentMode != RulerType.Eraser)
                    {
                        this.CurrentInfo = new RulerInfo()
                        {
                            OwnerID = Client.Instance.ID,
                            OwnerName = Client.Instance.Settings.Name,
                            Tooltip = this.CurrentTooltip,
                            Color = Extensions.FromVec4(this.CurrentColor),
                            Start = Client.Instance.Frontend.GameHandle.IsAnyAltDown() ? MapRenderer.SnapToGrid(tHitOrPt, m.GridSize) : tHitOrPt,
                            End = tHitOrPt,
                            ExtraInfo = this.CurrentExtraValue,
                            IsDead = false,
                            NextDeleteTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 1500,
                            SelfID = Guid.NewGuid(),
                            Type = this.CurrentMode,
                            DisplayInfo = this.RulersDisplayInfo
                        };

                        this.ActiveInfos.Add(this.CurrentInfo);
                        this.UpdateCurrentInfo();
                    }
                    else
                    {
                        for (int i = this.ActiveInfos.Count - 1; i >= 0; i--)
                        {
                            RulerInfo ri = this.ActiveInfos[i];
                            if (ri.KeepAlive && (ri.Type == RulerType.Polyline ? ri.Points.Any(x => (x - tHitOrPt).Length() <= this.CurrentExtraValue) : (ri.Start - tHitOrPt).Length() <= this.CurrentExtraValue))
                            {
                                if (this.CanErase(ri))
                                {
                                    ri.IsDead = true;
                                    ri.KeepAlive = false;
                                    new PacketRulerInfo() { Info = ri }.Send();
                                }
                            }
                        }

                    }
                }

                if (this._lmbDown && this.CurrentInfo != null && !this.CurrentInfo.IsDead)
                {
                    Vector3 now = tHitOrPt;

                    if (Client.Instance.Frontend.GameHandle.IsAnyShiftDown())
                    {
                        Plane p = new Plane(-Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Direction, 0f);
                        Ray r = Client.Instance.Frontend.Renderer.MapRenderer.RayFromCursor();
                        Vector3? v = p.Intersect(r, this.CurrentInfo.Start);
                        if (v.HasValue)
                        {
                            now = v.Value;
                        }
                    }

                    if (Client.Instance.Frontend.GameHandle.IsAnyAltDown())
                    {
                        now = MapRenderer.SnapToGrid(now, m.GridSize);
                    }

                    if (Client.Instance.Frontend.GameHandle.IsAnyControlDown())
                    {
                        now = new Vector3(now.X, now.Y, this.CurrentInfo.Start.Z);
                    }

                    if (now != this.CurrentInfo.End)
                    {
                        this.CurrentInfo.End = now;
                    }
                }

                if (Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Right) && !this._rmbDown)
                {
                    this._rmbDown = true;
                    if (this.CurrentInfo != null)
                    {
                        if (this.CurrentInfo.Type is not RulerType.Polyline and not RulerType.Eraser)
                        {
                            RulerInfo clone = new RulerInfo() { SelfID = Guid.NewGuid() };
                            clone.CloneData(this.CurrentInfo);
                            clone.KeepAlive = true;
                            new PacketRulerInfo() { Info = clone }.Send();
                            this.CurrentInfo.Start = this.CurrentInfo.End;
                            this.UpdateCurrentInfo();
                        }

                        if (this.CurrentInfo.Type == RulerType.Polyline)
                        {
                            Vector3[] nArr = new Vector3[this.CurrentInfo.Points.Length + 1];
                            Array.Copy(this.CurrentInfo.Points, nArr, this.CurrentInfo.Points.Length);
                            nArr[^1] = this.CurrentInfo.End;
                            this.CurrentInfo.Points = nArr;
                            this.UpdateCurrentInfo();
                        }
                    }
                    else
                    {
                        Vector3 now = tHitOrPt;
                        for (int i = this.ActiveInfos.Count - 1; i >= 0; i--)
                        {
                            RulerInfo ri = this.ActiveInfos[i];
                            if (ri.KeepAlive && (ri.OwnerID.Equals(Guid.Empty) || ri.OwnerID.Equals(Client.Instance.ID) || Client.Instance.IsAdmin))
                            {
                                float distance = (now - ri.Start).Length();
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

            if (!Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Left) && this._lmbDown)
            {
                this._lmbDown = false;
                if (this.CurrentInfo != null)
                {
                    if (this.CurrentInfo.Type == RulerType.Polyline && this.CurrentInfo.Points.Length > 2)
                    {
                        RulerInfo clone = new RulerInfo() { SelfID = Guid.NewGuid() };
                        clone.CloneData(this.CurrentInfo);
                        Vector3[] nArr = new Vector3[clone.Points.Length - 1];
                        Array.Copy(clone.Points, nArr, nArr.Length);
                        clone.Points = nArr;
                        clone.KeepAlive = true;
                        new PacketRulerInfo() { Info = clone }.Send();
                    }

                    this.CurrentInfo.IsDead = true;
                    this._updateTicksCtr = 0;
                    this.UpdateCurrentInfo();
                    this.CurrentInfo = null;
                }
            }

            if (!Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Right) && this._rmbDown)
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
                                float radiusSq = (ri.End.Xy() - ri.Start.Xy()).LengthSquared();
                                foreach (MapObject mo in m.IterateObjects(Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer))
                                {
                                    BBBox rBB = new BBBox(mo.ClientRaycastBox.Scale(mo.Scale), mo.Rotation);
                                    if (IsInCircle(rBB, mo.Position, ri.Start, radiusSq))
                                    {
                                        this._highlightedObjects.Add((mo, ri.Color));
                                    }
                                }

                                break;
                            }

                            case RulerType.Sphere:
                            {
                                float radiusSq = (ri.End - ri.Start).LengthSquared();
                                foreach (MapObject mo in m.IterateObjects(Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer))
                                {
                                    BBBox rBB = new BBBox(mo.ClientRaycastBox.Scale(mo.Scale), mo.Rotation);
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
                                    BBBox rBB = new BBBox(mo.ClientRaycastBox.Scale(mo.Scale), mo.Rotation);
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
                                    BBBox rBB = new BBBox(mo.ClientRaycastBox.Scale(mo.Scale), mo.Rotation);
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
                                    BBBox rBB = new BBBox(mo.ClientRaycastBox.Scale(mo.Scale), mo.Rotation);
                                    if (IsInLine(rBB, mo.Position, ri.Start, ri.End, ri.ExtraInfo))
                                    {
                                        this._highlightedObjects.Add((mo, ri.Color));
                                    }
                                }

                                break;
                            }

                            case RulerType.Polyline:
                            {
                                for (int j = 0; j < ri.Points.Length - 1; ++j)
                                {
                                    Vector3 start = ri.Points[j];
                                    Vector3 end = ri.Points[j + 1];
                                    foreach (MapObject mo in m.IterateObjects(Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer))
                                    {
                                        BBBox rBB = new BBBox(mo.ClientRaycastBox.Scale(mo.Scale), mo.Rotation);
                                        if (IsInLine(rBB, mo.Position, start, end, ri.ExtraInfo))
                                        {
                                            this._highlightedObjects.Add((mo, ri.Color));
                                        }
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
                                    BBBox rBB = new BBBox(mo.ClientRaycastBox.Scale(mo.Scale), mo.Rotation);
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
            this.ModelCube = OpenGLUtil.LoadModel("cube", VertexFormat.Pos);
            this._vao = new VertexArray();
            this._vbo = new GPUBuffer(BufferTarget.Array);
            this._ebo = new GPUBuffer(BufferTarget.ElementArray);
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

            this.CPUTimer.Restart();

            ShaderProgram shader = Client.Instance.Frontend.Renderer.ObjectRenderer.OverlayShader;
            Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
            shader.Bind();
            shader["view"].Set(cam.View);
            shader["projection"].Set(cam.Projection);
            Matrix4x4 model;
            Vector3? tHit = Client.Instance.Frontend.Renderer.MapRenderer.GetTerrainCursorOrPointAlongsideView();
            bool inMeasureMode = Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode == EditMode.Measure;

            foreach (RulerInfo ri in this.ActiveInfos)
            {
                model = Matrix4x4.CreateScale(StartEndCapScale) * Matrix4x4.CreateTranslation(ri.Start);
                shader["model"].Set(model);
                Vector4 riClr = ri.Color.Vec4();
                if (this.CurrentMode == RulerType.Eraser && tHit.HasValue && inMeasureMode)
                {
                    bool inRange = ri.Type == RulerType.Polyline ? ri.Points.Any(x => (x - tHit.Value).Length() <= this.CurrentExtraValue) : (ri.Start - tHit.Value).Length() <= this.CurrentExtraValue;
                    if (inRange)
                    {
                        if (this.CanErase(ri))
                        {
                            riClr = Client.Instance.Frontend.UpdatesExisted % 60 < 30 ? Vector4.One : riClr;
                        }
                    }
                }

                shader["u_color"].Set(riClr);
                this.ModelSphere.Render();
                Vector3 vE2S = (ri.End - ri.PreEnd).Normalized();
                Vector3 a = Vector3.Cross(Vector3.UnitY, vE2S);
                Quaternion q = new Quaternion(a, 1 + Vector3.Dot(Vector3.UnitY, vE2S)).Normalized();

                if (!ri.KeepAlive || ((ri.Type is RulerType.Ruler or RulerType.Polyline) && ri.CumulativeLength > 0.2f))
                {
                    model = Matrix4x4.CreateScale(StartEndCapScale) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(ri.End);
                    shader["model"].Set(model);
                    this.ModelArrow.Render();
                    if (ri.Type == RulerType.Polyline)
                    {
                        for (int i = 0; i < ri.Points.Length - 1; ++i)
                        {
                            if (i > 0)
                            {
                                model = Matrix4x4.CreateScale(StartEndCapScale) * Matrix4x4.CreateTranslation(ri.Points[i]);
                                shader["model"].Set(model);
                                this.ModelSphere.Render();
                            }

                            this.CreateLine(ri.Points[i], ri.Points[i + 1]);
                        }
                    }
                    else
                    {
                        this.CreateLine(ri.Start, ri.End);
                    }

                    this.UploadBuffers();
                    this._vao.Bind();
                    shader["model"].Set(Matrix4x4.Identity);
                    GL.Disable(Capability.CullFace);
                    GL.DrawElements(PrimitiveType.Triangles, this._indexData.Count, ElementsType.UnsignedInt, IntPtr.Zero);
                }
                else
                {
                    shader["model"].Set(Matrix4x4.Identity);
                    GL.Disable(Capability.CullFace);
                }

                this._vertexData.Clear();
                this._indexData.Clear();
                GL.Enable(Capability.Blend);
                if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
                {
                    GL.Enable(Capability.Multisample);
                    GL.Enable(Capability.SampleAlphaToCoverage);
                }

                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                switch (ri.Type)
                {
                    case RulerType.Circle:
                    {
                        this.CreateCircle(ri.Start, (ri.End - ri.Start).Length(), m.Is2D ? 0.075f : 0.12f);
                        this.UploadBuffers();
                        shader["u_color"].Set(ri.Color.Vec4() * new Vector4(1, 1, 1, 0.5f));
                        this._vao.Bind();
                        GL.DrawElements(PrimitiveType.Triangles, this._indexData.Count, ElementsType.UnsignedInt, IntPtr.Zero);
                        this._vertexData.Clear();
                        this._indexData.Clear();
                        break;
                    }

                    case RulerType.Sphere:
                    {
                        float radius = (ri.End - ri.Start).Length();
                        model = Matrix4x4.CreateScale(radius * 2) * Matrix4x4.CreateTranslation(ri.Start);
                        shader["model"].Set(model);
                        shader["u_color"].Set(ri.Color.Vec4() * new Vector4(1, 1, 1, 0.5f));
                        GL.Enable(Capability.CullFace);
                        GL.CullFace(PolygonFaceMode.Front);
                        this.ModelSphere.Render();
                        GL.CullFace(PolygonFaceMode.Back);
                        this.ModelSphere.Render();
                        GL.Disable(Capability.CullFace);
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

                        /*
                        model = Matrix4.CreateScale(r * 2, r * 2, hZ * 2) * Matrix4.CreateTranslation(sStart);
                        shader["model"].Set(model);
                        shader["u_color"].Set(ri.Color.Vec4() * new Vector4(1, 1, 1, 0.5f));
                        this.ModelSquare.Render();
                        */

                        Vector3 ftl = new Vector3(ri.Start.X - r, ri.Start.Y - r, z);
                        Vector3 bbr = new Vector3(ri.Start.X + r, ri.Start.Y + r, sStart.Z);
                        this.CreateSquare(ftl, bbr);
                        this.UploadBuffers();
                        shader["u_color"].Set(ri.Color.Vec4() * new Vector4(1, 1, 1, 0.5f));
                        this._vao.Bind();
                        GL.DrawElements(PrimitiveType.Triangles, this._indexData.Count, ElementsType.UnsignedInt, IntPtr.Zero);
                        this._vertexData.Clear();
                        this._indexData.Clear();
                        break;
                    }

                    case RulerType.Cube:
                    {
                        Vector3 vE2Snn = (ri.End - ri.Start);
                        float r = MathF.Max(MathF.Max(MathF.Abs(vE2Snn.X), MathF.Abs(vE2Snn.Y)), MathF.Abs(vE2Snn.Z));
                        model = Matrix4x4.CreateScale(r * 2) * Matrix4x4.CreateTranslation(ri.Start);
                        shader["model"].Set(model);
                        shader["u_color"].Set(ri.Color.Vec4() * new Vector4(1, 1, 1, 0.5f));
                        GL.Enable(Capability.CullFace);
                        GL.CullFace(PolygonFaceMode.Front);
                        this.ModelCube.Render();
                        GL.CullFace(PolygonFaceMode.Back);
                        this.ModelCube.Render();
                        GL.Disable(Capability.CullFace);
                        break;
                    }

                    case RulerType.Line:
                    {
                        Vector3 vE2Snn = ri.End - ri.Start;
                        float gFac = m.GridUnit;
                        model = Matrix4x4.CreateScale(ri.ExtraInfo / gFac, vE2Snn.Length(), ri.ExtraInfo / gFac) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(ri.Start + ((ri.End - ri.Start) / 2));
                        shader["model"].Set(model);
                        shader["u_color"].Set(ri.Color.Vec4() * new Vector4(1, 1, 1, 0.5f));
                        GL.Enable(Capability.CullFace);
                        GL.CullFace(PolygonFaceMode.Front);
                        this.ModelCube.Render();
                        GL.CullFace(PolygonFaceMode.Back);
                        this.ModelCube.Render();
                        GL.Disable(Capability.CullFace);
                        break;
                    }

                    case RulerType.Cone:
                    {
                        this.CreateCone(ri.Start, ri.End, ri.ExtraInfo / m.GridUnit);
                        this.UploadBuffers();
                        shader["u_color"].Set(ri.Color.Vec4() * new Vector4(1, 1, 1, 0.5f));
                        this._vao.Bind();
                        GL.DrawElements(PrimitiveType.Triangles, this._indexData.Count, ElementsType.UnsignedInt, IntPtr.Zero);
                        this._vertexData.Clear();
                        this._indexData.Clear();
                        break;
                    }
                }

                GL.Disable(Capability.Blend);
                if (Client.Instance.Settings.MSAA != ClientSettings.MSAAMode.Disabled)
                {
                    GL.Disable(Capability.Multisample);
                    GL.Disable(Capability.SampleAlphaToCoverage);
                }

                GL.Enable(Capability.CullFace);
            }

            if (this.CurrentMode == RulerType.Eraser && tHit.HasValue && inMeasureMode)
            {
                model = Matrix4x4.CreateScale(this.CurrentExtraValue * 2) * Matrix4x4.CreateTranslation(tHit.Value);
                shader["model"].Set(model);
                shader["u_color"].Set((new Vector4(1, 1, 1, this.CurrentColor.W * 2) - this.CurrentColor) * new Vector4(1, 1, 1, 0.3f));
                GL.Enable(Capability.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                this.ModelSphere.Render();
                GL.Disable(Capability.Blend);
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

            this.CPUTimer.Stop();
        }

        private readonly List<Vector2> _groundQuadGenTempList = new List<Vector2>();

        public void CreateCone(Vector3 start, Vector3 end, float radius)
        {
            Vector3 planeNormal = (end - start).Normalized();
            if (float.IsNaN(planeNormal.X) || float.IsNaN(planeNormal.Y) || float.IsNaN(planeNormal.Z))
            {
                return;
            }

            Vector4 planePerpendicular = new Vector4(planeNormal.ArbitraryOrthogonal(), 1.0f);
            this._vertexData.Add(start.X);
            this._vertexData.Add(start.Y);
            this._vertexData.Add(start.Z);
            this._vertexData.Add(end.X);
            this._vertexData.Add(end.Y);
            this._vertexData.Add(end.Z);
            for (int i = 0; i < 36; ++i)
            {
                float angleRad = i * 10f * MathF.PI / 180;
                Quaternion q = Quaternion.CreateFromAxisAngle(planeNormal, angleRad);
                Vector4 v = Vector4.Transform(planePerpendicular, q);
                Vector3 v3 = end + (v.Xyz().Normalized() * radius);
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

        public void CreateSquare(Vector3 ftl, Vector3 bbr)
        {
            Vector3 v0 = new Vector3(ftl.X, ftl.Y, bbr.Z);
            Vector3 v1 = new Vector3(bbr.X, ftl.Y, bbr.Z);
            Vector3 v2 = new Vector3(bbr.X, bbr.Y, bbr.Z);
            Vector3 v3 = new Vector3(ftl.X, bbr.Y, bbr.Z);

            Vector3 v4 = new Vector3(ftl.X, ftl.Y, ftl.Z);
            Vector3 v5 = new Vector3(bbr.X, ftl.Y, ftl.Z);
            Vector3 v6 = new Vector3(bbr.X, bbr.Y, ftl.Z);
            Vector3 v7 = new Vector3(ftl.X, bbr.Y, ftl.Z);

            // Add left and right sides
            uint i0 = (uint)this._vertexData.Count / 3;
            this.AddVertices(v0, v3, v7, v4);
            this._indexData.Add(i0 + 0);
            this._indexData.Add(i0 + 1);
            this._indexData.Add(i0 + 2);
            this._indexData.Add(i0 + 0);
            this._indexData.Add(i0 + 2);
            this._indexData.Add(i0 + 3);
            i0 += 4;
            this.AddVertices(v1, v2, v6, v5);
            this._indexData.Add(i0 + 0);
            this._indexData.Add(i0 + 1);
            this._indexData.Add(i0 + 2);
            this._indexData.Add(i0 + 0);
            this._indexData.Add(i0 + 2);
            this._indexData.Add(i0 + 3);

            // Add front and back sides
            i0 += 4;
            this.AddVertices(v0, v1, v5, v4);
            this._indexData.Add(i0 + 0);
            this._indexData.Add(i0 + 1);
            this._indexData.Add(i0 + 2);
            this._indexData.Add(i0 + 0);
            this._indexData.Add(i0 + 2);
            this._indexData.Add(i0 + 3);
            i0 += 4;
            this.AddVertices(v2, v3, v7, v6);
            this._indexData.Add(i0 + 0);
            this._indexData.Add(i0 + 1);
            this._indexData.Add(i0 + 2);
            this._indexData.Add(i0 + 0);
            this._indexData.Add(i0 + 2);
            this._indexData.Add(i0 + 3);

            // Create top and bottom outlines
            void AddVerticalLine(Vector3 from, Vector3 to, float thickness = LineThickness)
            {
                Vector3 tl = new Vector3(from.X - thickness, from.Y - thickness, from.Z);
                Vector3 tr = new Vector3(from.X + thickness, from.Y - thickness, from.Z);
                Vector3 bl = new Vector3(to.X - thickness, to.Y + thickness, from.Z);
                Vector3 br = new Vector3(to.X + thickness, to.Y + thickness, from.Z);
                uint lastIndex = (uint)(this._vertexData.Count / 3);
                this.AddVertices(tl, tr, br, bl);
                this._indexData.Add(lastIndex + 0);
                this._indexData.Add(lastIndex + 1);
                this._indexData.Add(lastIndex + 2);
                this._indexData.Add(lastIndex + 0);
                this._indexData.Add(lastIndex + 2);
                this._indexData.Add(lastIndex + 3);
            }

            void AddHorizontalLine(Vector3 from, Vector3 to, float thickness = LineThickness)
            {
                Vector3 tl = new Vector3(from.X - thickness, from.Y - thickness, from.Z);
                Vector3 tr = new Vector3(from.X - thickness, from.Y + thickness, from.Z);
                Vector3 bl = new Vector3(to.X + thickness, to.Y - thickness, from.Z);
                Vector3 br = new Vector3(to.X + thickness, to.Y + thickness, from.Z);
                uint lastIndex = (uint)(this._vertexData.Count / 3);
                this.AddVertices(tl, tr, br, bl);
                this._indexData.Add(lastIndex + 0);
                this._indexData.Add(lastIndex + 1);
                this._indexData.Add(lastIndex + 2);
                this._indexData.Add(lastIndex + 0);
                this._indexData.Add(lastIndex + 2);
                this._indexData.Add(lastIndex + 3);
            }

            AddVerticalLine(v0, v3);
            AddVerticalLine(v4, v7);
            AddVerticalLine(v1, v2);
            AddVerticalLine(v5, v6);

            AddHorizontalLine(v0, v1);
            AddHorizontalLine(v4, v5);
            AddHorizontalLine(v2, v3);
            AddHorizontalLine(v6, v7);
        }

        public void CreateCircle(Vector3 start, float radius, float outlineThickness)
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
            int numSegments = Math.Min((int)(36 * MathF.Max(1, (radius / 2.5f))), 120);
            float angleStep = 360.0f / numSegments * MathF.PI / 180;
            float cos = MathF.Cos(angleStep);
            float sin = MathF.Sin(angleStep);
            Vector3 v = Vector3.UnitY;
            this._groundQuadGenTempList.Clear();
            for (int i = 0; i < numSegments; ++i)
            {
                float dX = (v.X * cos) - (v.Y * sin);
                float dY = (v.X * sin) + (v.Y * cos);
                v = new Vector3(dX, dY, 0);
                this._groundQuadGenTempList.Add(v.Xy());

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
                Vector2 l1 = c2n.PerpendicularLeft();
                Vector2 l2 = p2c.PerpendicularRight();
                Vector2 l = Vector2.Lerp(l1, l2, 0.5f).Normalized();

                Vector3 v1 = start + new Vector3(current.X, current.Y, 0) + (new Vector3(l.X, l.Y, 0) * outlineThickness);
                Vector3 v2 = start + new Vector3(current.X, current.Y, 0) - (new Vector3(l.X, l.Y, 0) * outlineThickness);
                Vector3 v3 = start + new Vector3(current.X, current.Y, zHeight) + (new Vector3(l.X, l.Y, 0) * outlineThickness);
                Vector3 v4 = start + new Vector3(current.X, current.Y, zHeight) - (new Vector3(l.X, l.Y, 0) * outlineThickness);

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

        public void CreateLine(Vector3 start, Vector3 end, bool completeToEnd = false)
        {
            Vector3 vE2S = (end - start).Normalized();
            Vector3 a = Vector3.Cross(Vector3.UnitX, vE2S);
            Quaternion qZ = new Quaternion(a, 1).Normalized();

            Vector3 oZ = Vector4.Transform(new Vector4(0, 0, 1, 1), qZ).Xyz().Normalized() * LineThickness;
            Vector3 oX = Vector3.Cross(vE2S, oZ).Normalized() * LineThickness;

            if (!completeToEnd)
            {
                end -= vE2S * StartEndCapScale * 0.5f;
            }

            Vector3 v1 = start + oX + oZ; // 0
            Vector3 v2 = start + oX - oZ; // 1
            Vector3 v3 = start - oX + oZ; // 2
            Vector3 v4 = start - oX - oZ; // 3
            Vector3 v5 = end + oX + oZ;   // 4
            Vector3 v6 = end + oX - oZ;   // 5
            Vector3 v7 = end - oX + oZ;   // 6
            Vector3 v8 = end - oX - oZ;   // 7

            uint fvIdx = (uint)(this._vertexData.Count / 3);
            this.AddVertices(v1, v2, v3, v4, v5, v6, v7, v8);
            this._indexData.Add(fvIdx + 0);
            this._indexData.Add(fvIdx + 1);
            this._indexData.Add(fvIdx + 4);
            this._indexData.Add(fvIdx + 1);
            this._indexData.Add(fvIdx + 4);
            this._indexData.Add(fvIdx + 5);

            this._indexData.Add(fvIdx + 2);
            this._indexData.Add(fvIdx + 3);
            this._indexData.Add(fvIdx + 6);
            this._indexData.Add(fvIdx + 3);
            this._indexData.Add(fvIdx + 6);
            this._indexData.Add(fvIdx + 7);

            this._indexData.Add(fvIdx + 0);
            this._indexData.Add(fvIdx + 2);
            this._indexData.Add(fvIdx + 4);
            this._indexData.Add(fvIdx + 2);
            this._indexData.Add(fvIdx + 4);
            this._indexData.Add(fvIdx + 6);

            this._indexData.Add(fvIdx + 1);
            this._indexData.Add(fvIdx + 3);
            this._indexData.Add(fvIdx + 5);
            this._indexData.Add(fvIdx + 3);
            this._indexData.Add(fvIdx + 5);
            this._indexData.Add(fvIdx + 7);
        }

        public void AddVertices(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
        {
            this._vertexData.Add(v0.X);
            this._vertexData.Add(v0.Y);
            this._vertexData.Add(v0.Z);
            this._vertexData.Add(v1.X);
            this._vertexData.Add(v1.Y);
            this._vertexData.Add(v1.Z);
            this._vertexData.Add(v2.X);
            this._vertexData.Add(v2.Y);
            this._vertexData.Add(v2.Z);
            this._vertexData.Add(v3.X);
            this._vertexData.Add(v3.Y);
            this._vertexData.Add(v3.Z);
        }

        public void AddVertices(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4, Vector3 v5, Vector3 v6, Vector3 v7)
        {
            this._vertexData.Add(v0.X);
            this._vertexData.Add(v0.Y);
            this._vertexData.Add(v0.Z);
            this._vertexData.Add(v1.X);
            this._vertexData.Add(v1.Y);
            this._vertexData.Add(v1.Z);
            this._vertexData.Add(v2.X);
            this._vertexData.Add(v2.Y);
            this._vertexData.Add(v2.Z);
            this._vertexData.Add(v3.X);
            this._vertexData.Add(v3.Y);
            this._vertexData.Add(v3.Z);
            this._vertexData.Add(v4.X);
            this._vertexData.Add(v4.Y);
            this._vertexData.Add(v4.Z);
            this._vertexData.Add(v5.X);
            this._vertexData.Add(v5.Y);
            this._vertexData.Add(v5.Z);
            this._vertexData.Add(v6.X);
            this._vertexData.Add(v6.Y);
            this._vertexData.Add(v6.Z);
            this._vertexData.Add(v7.X);
            this._vertexData.Add(v7.Y);
            this._vertexData.Add(v7.Z);
        }

        public bool CanErase(RulerInfo ri)
        {
            return Client.Instance.IsAdmin
                ? this.CurrentEraserMask.Equals(Guid.Empty) || this.CurrentEraserMask.Equals(ri.OwnerID)
                : ri.OwnerID.Equals(Client.Instance.ID);
        }

        public void UploadBuffers()
        {
            this._vao.Bind();
            this._vbo.Bind();
            this._vbo.SetData(this._vertexData.ToArray());
            this._ebo.Bind();
            this._ebo.SetData(this._indexData.ToArray());
        }

        public void RenderBuffers()
        {
            GL.DrawElements(PrimitiveType.Triangles, this._indexData.Count, ElementsType.UnsignedInt, IntPtr.Zero);
        }

        public void ClearBuffers()
        {
            this._vertexData.Clear();
            this._indexData.Clear();
        }

        public void RenderUI(Map cMap)
        {
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoSavedSettings;
            foreach (RulerInfo ri in this.ActiveInfos)
            {
                if (!ri.IsDead && ri.DisplayInfo)
                {
                    if (ri.KeepAlive)
                    {
                        Vector3 screen = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.ToScreenspace((ri.Type == RulerType.Polyline ? ri.CumulativeCenter : ri.Start) + Vector3.UnitZ);
                        if (screen.Z >= 0)
                        {
                            float len = ri.Type == RulerType.Polyline ? ri.CumulativeLength * cMap.GridUnit : (ri.End - ri.Start).Length() * cMap.GridUnit;
                            string text = len.ToString("0.00");
                            Vector2 tLen = ImGuiHelper.CalcTextSize(ri.OwnerName);
                            Vector2 tLen2 = ImGuiHelper.CalcTextSize(ri.Tooltip);
                            Vector2 tLen3 = ImGuiHelper.CalcTextSize(text);
                            float maxW = MathF.Max(tLen.X, MathF.Max(tLen2.X, tLen3.X));
                            ImGui.SetNextWindowPos(screen.Xy() - (new Vector2(maxW, tLen.Y) / 2));
                            if (ImGui.Begin("TextOverlayData_" + ri.SelfID.ToString(), flags | ImGuiWindowFlags.AlwaysAutoResize))
                            {
                                float cX;
                                float delta;
                                if (tLen.X < maxW)
                                {
                                    cX = ImGui.GetCursorPosX();
                                    delta = maxW - tLen.X;
                                    ImGui.SetCursorPosX(cX + (delta / 2));
                                }

                                ImGui.PushStyleColor(ImGuiCol.Text, ri.Color.Abgr());
                                ImGui.TextUnformatted(ri.OwnerName);
                                ImGui.PopStyleColor();
                                if (!string.IsNullOrEmpty(ri.Tooltip))
                                {
                                    if (tLen2.X < maxW)
                                    {
                                        cX = ImGui.GetCursorPosX();
                                        delta = maxW - tLen2.X;
                                        ImGui.SetCursorPosX(cX + (delta / 2));
                                    }

                                    ImGui.TextUnformatted(ri.Tooltip);
                                }


                                if (tLen3.X < maxW)
                                {
                                    cX = ImGui.GetCursorPosX();
                                    delta = maxW - tLen3.X;
                                    ImGui.SetCursorPosX(cX + (delta / 2));
                                }

                                if (len > 0.01f)
                                {
                                    ImGui.PushStyleColor(ImGuiCol.Text, ri.Color.Abgr());
                                    ImGui.TextUnformatted(text);
                                    ImGui.PopStyleColor();
                                }
                            }

                            ImGui.End();
                        }
                    }
                    else
                    {
                        Vector3 half = ri.Type == RulerType.Polyline ? ri.CumulativeCenter : ri.Start + ((ri.End - ri.Start) / 2f);
                        Vector3 halfScreen = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.ToScreenspace(half);
                        if (halfScreen.Z >= 0)
                        {
                            float len = ri.Type == RulerType.Polyline ? ri.CumulativeLength * cMap.GridUnit : (ri.End - ri.Start).Length() * cMap.GridUnit;
                            string text = len.ToString("0.00");
                            Vector2 tLen = ImGuiHelper.CalcTextSize(text);
                            ImGui.SetNextWindowPos(halfScreen.Xy() - (tLen / 2));
                            if (ImGui.Begin("TextOverlayData_" + ri.SelfID.ToString(), flags))
                            {
                                ImGui.PushStyleColor(ImGuiCol.Text, ri.Color.Abgr());
                                ImGui.TextUnformatted(text);
                                ImGui.PopStyleColor();
                            }

                            ImGui.End();
                        }
                    }
                }
            }
        }

        private static bool IsInCircle(BBBox box, Vector3 offset, Vector3 point, float rSq)
        {
            Vector2 cPoint = point.Xy() - offset.Xy();
            Vector3 bRnd = Vector3.Transform(box.Start, box.Rotation);
            if ((bRnd.Xy() - cPoint).LengthSquared() <= rSq)
            {
                return true;
            }

            foreach (Vector3 v in box)
            {
                if ((v.Xy() - cPoint).LengthSquared() <= rSq)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInSphere(BBBox box, Vector3 offset, Vector3 point, float rSq)
        {
            point -= offset;
            Vector3 bRnd = Vector3.Transform(box.Start, box.Rotation);
            if ((bRnd - point).LengthSquared() <= rSq)
            {
                return true;
            }

            foreach (Vector3 v in box)
            {
                if ((v - point).LengthSquared() <= rSq)
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
            Vector3 bRnd = Vector3.Transform(box.Start, box.Rotation);
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
            Vector3 bRnd = Vector3.Transform(box.Start, box.Rotation);
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

            static bool IsAroundZero(float f, float eps = 1e-5f) => MathF.Abs(f) <= eps;

            Vector3 vE2S = vE2Snn.Normalized();
            Vector3 a = Vector3.Cross(Vector3.UnitY, vE2S);

            AABox originBox = new AABox(-0.5f, -0.5f, -0.5f, 0.5f, 0.5f, 0.5f);
            Vector3 scale = new Vector3(radius / gFac, vE2Snn.Length(), radius / gFac);
            Quaternion rotation = new Quaternion(a, 1 + Vector3.Dot(Vector3.UnitY, vE2S)).Normalized();
            Vector3 translation = start + (vE2Snn / 2);
            if (IsAroundZero(scale.X) || IsAroundZero(scale.Y) || IsAroundZero(scale.Z) || float.IsNaN(rotation.X) || float.IsNaN(rotation.Y) || float.IsNaN(rotation.Z) || float.IsNaN(rotation.W))
            {
                return false;
            }

            Matrix4x4 modelO = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);
            Matrix4x4.Invert(modelO, out Matrix4x4 model);
            foreach (Vector3 v in box)
            {
                Vector4 vT = Vector4.Transform(new Vector4(v + offset, 1.0f), model);
                if (originBox.Contains(vT.Xyz() / vT.W))
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
                if (IsPointInCone(offset + c, start, v.Normalized(), v.Length(), radius))
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
            float orthoDist = (point - start - (cDist * dir)).Length();
            return orthoDist < cRad;
        }
    
    }

    public enum RulerType
    {
        Ruler,    // X ------> Y
        Circle,   // From point XY circle
        Sphere,   // From point 3d sphere
        Square,   // From point XY quad
        Cube,     // From point 3d cube
        Line,     // From point 3d rectangle, ExtraInfo = radius
        Cone,
        Polyline, // Multiple point line
        Eraser,   // Erase data points, ExtraInfo = radius
    }
}
