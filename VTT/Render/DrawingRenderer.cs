namespace VTT.Render
{
    using ImGuiNET;
    using OpenTK.Mathematics;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using VTT.Asset.Obj;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;
    using static VTT.Network.ClientSettings;
    using OGL = OpenTK.Graphics.OpenGL.GL;

    public class DrawingRenderer
    {
        public const int MaxPointsPerContainer = short.MaxValue;

        public List<DrawingPointContainer> Containers { get; } = new List<DrawingPointContainer>();

        public ShaderProgram Shader { get; set; }
        public WavefrontObject EraserSphere { get; set; }
        public Stopwatch CPUTimer { get; set; } = new Stopwatch();

        public bool IsDrawing { get; set; } = true;
        public float CurrentRadius { get; set; } = 0.333f;
        public Vector4 CurrentColor { get; set; }
        public Guid CurrentEraserMask { get; set; } = Guid.Empty;

        public void Init()
        {
            this.Shader = OpenGLUtil.LoadShader("drawing", OpenTK.Graphics.OpenGL.ShaderType.VertexShader, OpenTK.Graphics.OpenGL.ShaderType.FragmentShader);
            this.CurrentColor = Extensions.FromArgb(Client.Instance.Settings.Color).Vec4();
            this.EraserSphere = OpenGLUtil.LoadModel("sphere_mediumres", VertexFormat.Pos);
        }

        public void FreeAll()
        {
            this._lmbDown = false;
            if (this._editedDPC != null)
            {
                this._editedDPC.Free();
                this._editedDPC = null;
            }

            foreach (DrawingPointContainer dpc in this.Containers)
            {
                dpc.Free();
            }

            this.Containers.Clear();
        }

        public void AddContainers(IEnumerable<DrawingPointContainer> dpcs)
        {
            this.Containers.AddRange(dpcs);
            foreach (DrawingPointContainer dpc in dpcs)
            {
                dpc.NotifyUpdate();
            }
        }

        public void AddContainer(DrawingPointContainer dpc)
        {
            this.Containers.Add(dpc);
            dpc.NotifyUpdate();
        }

        public void UpdateContainer(DrawingPointContainer lDpc)
        {
            DrawingPointContainer dpc = this.Containers.Find(d => d.ID.Equals(lDpc.ID));
            if (dpc == null) //?
            {
            }
            else
            {
                dpc.UpdateFrom(lDpc);
                dpc.NotifyUpdate();
            }
        }

        public void RemoveContainer(Guid cId)
        {
            DrawingPointContainer dpc = this.Containers.Find(d => d.ID.Equals(cId));
            if (dpc != null)
            {
                dpc.Free();
                this.Containers.Remove(dpc);
            }
        }

        private DrawingPointContainer _editedDPC;
        private Vector3 _lastCursorWorld;
        private bool _lmbDown;
        private readonly HashSet<Guid> _erasedDrawings = new HashSet<Guid>();

        private void CommitToServer(Guid mId, DrawingPointContainer dpc)
        {
            new PacketAddOrUpdateDrawing() { DPC = dpc, MapID = mId }.Send();
            dpc.Free();
            this.Containers.Remove(dpc);
        }

        public void Update(Map m)
        {
            bool CheckCurrentDrawingSize()
            {
                if (this._editedDPC.Points.Count > MaxPointsPerContainer)
                {
                    this.CommitToServer(m.ID, this._editedDPC);
                    this._editedDPC = new DrawingPointContainer(Guid.NewGuid(), Client.Instance.ID, this._editedDPC.Radius, this._editedDPC.Color);
                    this.Containers.Add(this._editedDPC);
                    return false;
                }

                return true;
            }

            bool gLmbDown = Client.Instance.Frontend.GameHandle.IsMouseButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left);
            if (!this._lmbDown && gLmbDown)
            {
                this._lmbDown = true;
                if (this.IsDrawing && !ImGui.GetIO().WantCaptureMouse && Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode == EditMode.Draw && m.EnableDrawing)
                {
                    Vector3? cw = Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld;
                    if (cw.HasValue)
                    {
                        this._lastCursorWorld = cw.Value;
                        this._editedDPC = new DrawingPointContainer(Guid.NewGuid(), Client.Instance.ID, this.CurrentRadius, this.CurrentColor);
                        this._editedDPC.Points.Add(new DrawingPoint(this._lastCursorWorld));
                        this._editedDPC.NotifyUpdate();
                        this.Containers.Add(this._editedDPC);
                    }
                }
            }

            if (this._lmbDown)
            {
                if (gLmbDown)
                {
                    if (this._editedDPC != null)
                    {
                        Vector3? cw = Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld;
                        if (cw.HasValue)
                        {
                            float cDelta = (this._lastCursorWorld - cw.Value).Length;
                            float acceptibleCDelta = this._editedDPC.Radius * 0.25f;
                            if (cDelta > acceptibleCDelta)
                            {
                                int nSteps = (int)MathF.Floor(cDelta / acceptibleCDelta);
                                if (nSteps <= 1)
                                {
                                    this._editedDPC.Points.Add(new DrawingPoint(cw.Value));
                                    this._editedDPC.NotifyUpdate();
                                    CheckCurrentDrawingSize();
                                }
                                else
                                {
                                    // Account for fast mouse movements
                                    float step = 1f / nSteps;
                                    for (int i = 0; i < nSteps; ++i)
                                    {
                                        Vector3 loc = Vector3.Lerp(this._lastCursorWorld, cw.Value, step * i);
                                        this._editedDPC.Points.Add(new DrawingPoint(loc));
                                        CheckCurrentDrawingSize();
                                    }

                                    this._editedDPC.NotifyUpdate();
                                }

                                this._lastCursorWorld = cw.Value;
                            }
                        }
                    }
                    else
                    {
                        if (!this.IsDrawing && !ImGui.GetIO().WantCaptureMouse)
                        {
                            Vector3? cw = Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld;
                            if (cw.HasValue)
                            {
                                Vector3 rVec = new Vector3(this.CurrentRadius, this.CurrentRadius, this.CurrentRadius) * 0.5f;
                                AABox cBox = new AABox(cw.Value - rVec, cw.Value + rVec);
                                foreach (DrawingPointContainer dpc in this.Containers)
                                {
                                    if (this.CanErase(dpc) && dpc.TotalBounds.Intersects(cBox))
                                    {
                                        int rems = dpc.Points.RemoveAll(x => x.IsInRange(cw.Value, this.CurrentRadius, dpc.Radius));
                                        if (rems > 0)
                                        {
                                            // TODO server update
                                            if (dpc.Points.Count > 0)
                                            {
                                                dpc.NotifyUpdate();
                                                this._erasedDrawings.Add(dpc.ID);
                                            }
                                            else
                                            {
                                                this._erasedDrawings.Remove(dpc.ID);
                                                new PacketRemoveDrawing() { MapID = m.ID, DrawingID = dpc.ID }.Send();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    this._lmbDown = false;
                    if (this._editedDPC != null)
                    {
                        this.CommitToServer(m.ID, this._editedDPC);
                    }

                    if (this._erasedDrawings.Count > 0)
                    {
                        foreach (Guid id in this._erasedDrawings)
                        {
                            DrawingPointContainer dpc = this.Containers.Find(x => x.ID.Equals(id));
                            if (dpc != null)
                            {
                                new PacketAddOrUpdateDrawing() { DPC = dpc, MapID = m.ID }.Send();
                            }
                        }

                        this._erasedDrawings.Clear();
                    }

                    this._editedDPC = null;
                }
            }
        }

        public void Render(Camera cam)
        {
            this.CPUTimer.Restart();
            if (Client.Instance.Settings.DrawingsPerformance == DrawingsResourceAllocationMode.None)
            {
                this.CPUTimer.Stop();
                return;
            }

            int maxInstances = Client.Instance.Settings.DrawingsPerformance switch
            {
                DrawingsResourceAllocationMode.None => 0,
                DrawingsResourceAllocationMode.Minimum => 100000,
                DrawingsResourceAllocationMode.Limited => 650000,
                DrawingsResourceAllocationMode.Standard => 2147483,
                DrawingsResourceAllocationMode.Extra => 17179864,
                _ => int.MaxValue
            };

            int maxDrawCalls = Client.Instance.Settings.DrawingsPerformance switch
            {
                DrawingsResourceAllocationMode.None => 0,
                DrawingsResourceAllocationMode.Minimum => 100,
                DrawingsResourceAllocationMode.Limited => 255,
                DrawingsResourceAllocationMode.Standard => 6553,
                DrawingsResourceAllocationMode.Extra => 65535,
                _ => int.MaxValue
            };

            OGL.Enable(OpenTK.Graphics.OpenGL.EnableCap.CullFace);
            OGL.CullFace(OpenTK.Graphics.OpenGL.CullFaceMode.Back);

            this.Shader.Bind();
            this.Shader["projection"].Set(cam.Projection);
            this.Shader["view"].Set(cam.View);
            int dTotal = 0;
            int dCalls = 0;
            foreach (DrawingPointContainer item in Containers)
            {
                if (cam.IsAABoxInFrustrum(item.TotalBounds))
                {
                    this.Shader["u_color"].Set(item.Color);
                    dTotal += item.Draw();
                    if (dTotal > maxInstances)
                    {
                        break;
                    }

                    if (dCalls++ > maxDrawCalls)
                    {
                        break;
                    }
                }
            }

            if (!this.IsDrawing)
            {
                Vector3? tHit = Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld;
                if (tHit.HasValue)
                {
                    Matrix4 model = Matrix4.CreateScale(this.CurrentRadius) * Matrix4.CreateTranslation(tHit.Value);
                    ShaderProgram shader = Client.Instance.Frontend.Renderer.ObjectRenderer.OverlayShader;
                    shader.Bind();
                    shader["view"].Set(cam.View);
                    shader["projection"].Set(cam.Projection);
                    shader["model"].Set(model);
                    shader["u_color"].Set((new Vector4(1, 1, 1, this.CurrentColor.W * 2) - this.CurrentColor) * new Vector4(1, 1, 1, 0.3f));
                    OGL.Enable(OpenTK.Graphics.OpenGL.EnableCap.Blend);
                    OGL.BlendFunc(OpenTK.Graphics.OpenGL.BlendingFactor.SrcAlpha, OpenTK.Graphics.OpenGL.BlendingFactor.OneMinusSrcAlpha);
                    this.EraserSphere.Render();
                    OGL.Disable(OpenTK.Graphics.OpenGL.EnableCap.Blend);
                }
            }

            this.CPUTimer.Stop();
        }

        public bool CanErase(DrawingPointContainer dpc)
        {
            return Client.Instance.IsAdmin
                ? this.CurrentEraserMask.Equals(Guid.Empty) || this.CurrentEraserMask.Equals(dpc.OwnerID)
                : dpc.OwnerID.Equals(Client.Instance.ID);
        }
    }


}
