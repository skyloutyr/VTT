namespace VTT.Render
{
    using System;
    using System.Linq;
    using System.Numerics;
    using VTT.Control;
    using VTT.GLFW;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public class MapRenderer
    {
        public Camera ClientCamera { get; set; } = new VectorCamera(new Vector3(5, 5, 5), new Vector3(-1, -1, -1).Normalized());
        public float FOV { get; set; } = 60.0f * MathF.PI / 180;
        public bool IsOrtho { get; set; }

        public GridRenderer GridRenderer { get; set; }
        public FOWRenderer FOWRenderer { get; set; }
        public DrawingRenderer DrawingRenderer { get; set; }

        public Vector3? GroundHitscanResult { get; set; }
        public Vector3? TerrainRaycastResult { get; set; }

        public Vector3? TerrainHit => this.TerrainRaycastResult ?? this.GroundHitscanResult;

        public int CurrentLayer { get; set; }
        public float ZoomOrtho => this.camera2dzoom;
        public CameraControlMode CameraControlMode { get; set; } = CameraControlMode.Standard;

        public void Update(Map m)
        {
            if (m == null)
            {
                return;
            }

            this.HandleCamera();

            Ray r = Client.Instance.Frontend.Renderer.MapRenderer.RayFromCursor();
            RaycastResut rr = RaycastResut.Raycast(r, m, o => o.MapLayer <= 0);
            this.TerrainRaycastResult = rr.Result ? rr.Hit : null;

            this.DrawingRenderer?.Update(m);
        }

        public void Render(Map m, double time)
        {
            if (m != null)
            {
                this.GroundHitscanResult = TryHitscanGround(this.ClientCamera, out Vector3 cw) ? cw : null;
                this.GridRenderer.Render(time, this.ClientCamera, m);
            }
        }

        public void Create()
        {
            this.GridRenderer = new GridRenderer();
            this.GridRenderer.Create();
            this.FOWRenderer = new FOWRenderer();
            this.FOWRenderer.Create();
            this.FOWRenderer.DeleteFOW();
            this.DrawingRenderer = new DrawingRenderer();
            this.DrawingRenderer.Init();
        }

        private float Get2DMapHeightOrDefault()
        {
            Map m = Client.Instance.CurrentMap;
            return m?.Camera2DHeight * 2 ?? 10;
        }

        public Vector3 GetTerrainCursorOrPointAlongsideView()
        {
            Vector3? v = this.TerrainHit;
            if (v.HasValue)
            {
                return v.Value;
            }

            Ray r = this.RayFromCursor();
            return r.Origin + (r.Direction * 5.0f);
        }

        public void Resize(int w, int h)
        {
            if (!this.IsOrtho)
            {
                this.ClientCamera.Projection = Matrix4x4.CreatePerspectiveFieldOfView(Client.Instance.Settings.FOV * MathF.PI / 180, (float)w / h, 0.01f, 100f);
                this.ClientCamera.RecalculateData(assumedUpAxis: Vector3.UnitZ);
            }
            else
            {
                float zoom = this.camera2dzoom;
                this.ClientCamera.Projection = Matrix4x4.CreateOrthographicOffCenter(-w * 0.5f * zoom, w * 0.5f * zoom, -h * 0.5f * zoom, h * 0.5f * zoom, -this.Get2DMapHeightOrDefault(), this.Get2DMapHeightOrDefault());
                this.ClientCamera.RecalculateData(assumedUpAxis: Vector3.UnitY);
            }
        }

        public void ChangeFOVOrZoom(float to)
        {
            this.FOV = to;
            if (!this.IsOrtho)
            {
                this.ClientCamera.Projection = Matrix4x4.CreatePerspectiveFieldOfView(Client.Instance.Settings.FOV * MathF.PI / 180, (float)Client.Instance.Frontend.Width / Client.Instance.Frontend.Height, 0.01f, 100f);
                this.ClientCamera.RecalculateData(assumedUpAxis: Vector3.UnitZ);
            }
            else
            {
                this.camera2dzoom = to;
                this.Resize(Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
            }
        }

        public void Switch2D(bool b, float zoom = 0.01f)
        {
            Map m = Client.Instance.CurrentMap;
            if (m == null)
            {
                return;
            }

            this.IsOrtho = b;
            int w = Client.Instance.Frontend.Width;
            int h = Client.Instance.Frontend.Height;
            if (b)
            {
                this.camera2dzoom = zoom;
                this.ClientCamera.Projection = Matrix4x4.CreateOrthographicOffCenter(-w * 0.5f * zoom, w * 0.5f * zoom, -h * 0.5f * zoom, h * 0.5f * zoom, -this.Get2DMapHeightOrDefault(), this.Get2DMapHeightOrDefault());
                this.ClientCamera.Direction = -Vector3.UnitZ;
                this.ClientCamera.Position = new Vector3(this.ClientCamera.Position.X, this.ClientCamera.Position.Y, m.Camera2DHeight);
                this.ClientCamera.RecalculateData(assumedUpAxis: Vector3.UnitY);
            }
            else
            {
                if (this.ClientCamera.Direction.Equals(-Vector3.UnitZ))
                {
                    this.ClientCamera.Direction = new Vector3(0, 0.01f, -1f).Normalized();
                }

                this.ClientCamera.Projection = Matrix4x4.CreatePerspectiveFieldOfView(60.0f * MathF.PI / 180, (float)w / h, 0.01f, 100f);
                this.ClientCamera.RecalculateData(assumedUpAxis: Vector3.UnitZ);
            }
        }

        public void Change2DMapHeight(float to)
        {
            Map m = Client.Instance.CurrentMap;
            if (m == null)
            {
                return;
            }

            if (this.IsOrtho && m.Is2D)
            {
                this.Resize(Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
            }
        }

        private bool _cameraBeingMoved;
        private bool _cameraBeingRotated;
        private int _lastMouseX;
        private int _lastMouseY;
        private bool _mmbPressed;
        private Vector3 _pt;
        private float _mouseWheelDY;
        private float camera2dzoom = 0.01f;

        public void ScrollCamera(float dy)
        {
            if (!ImGuiNET.ImGui.GetIO().WantCaptureMouse)
            {
                this._mouseWheelDY += dy;
            }
        }

        public Ray RayFromCursor()
        {
            if (this.IsOrtho)
            {
                float w = Client.Instance.Frontend.Width;
                float mX = (Client.Instance.Frontend.MouseX - (w * 0.5f)) / (w * 0.5f);
                float h = Client.Instance.Frontend.Height;
                float mY = -(Client.Instance.Frontend.MouseY - (h * 0.5f)) / (h * 0.5f);
                float zoom = this.camera2dzoom;
                Vector3 right = this.ClientCamera.Right;
                Vector3 up = this.ClientCamera.Up;
                float rX = (mX * (w / 2) * zoom) + this.ClientCamera.Position.X;
                float rY = (mY * (h / 2) * zoom) + this.ClientCamera.Position.Y;
                Vector3 pos = (right * rX) + (up * rY) + (Vector3.UnitZ * (Client.Instance.CurrentMap?.Camera2DHeight ?? 5));
                Vector3 dir = -Vector3.UnitZ;
                return new Ray(pos, dir);
            }

            return this.ClientCamera.RayFromCursor();
        }

        public bool IsAABoxInFrustrum(AABox box, Vector3 offset) => this.ClientCamera.IsAABoxInFrustrum(box, offset);
        public bool IsMapObjectInFrustum(MapObject mo) => this.ClientCamera.IsSphereInFrustumCached(ref mo.cameraCullerSphere);

        public void HandleKeys(KeyEventData args)
        {
            bool drag = Client.Instance.Frontend.Renderer.SelectionManager.IsDraggingObjects;
            Map cmap = Client.Instance.CurrentMap;
            if (cmap == null)
            {
                return;
            }

            if (!args.Mods.HasFlag(ModifierKeys.Shift) && !ImGuiNET.ImGui.GetIO().WantCaptureKeyboard && !drag && cmap != null)
            {
                Vector2 cameraForward2D = this.IsOrtho || cmap.Is2D ? Vector2.UnitY : this.ClientCamera.Direction.Xy().Normalized();
                if (cameraForward2D.HasAnyNans())
                {
                    cameraForward2D = Vector2.UnitY;
                }

                Vector2 gridForward2D = MathF.Abs(cameraForward2D.X) > MathF.Abs(cameraForward2D.Y) ? Vector2.UnitX * MathF.Sign(cameraForward2D.X) : Vector2.UnitY * MathF.Sign(cameraForward2D.Y);
                Vector2 gridRight2D = gridForward2D.PerpendicularRight();
                Vector3 gridMoveVec = default;
                float rot = 0;
                gridForward2D *= cmap.GridSize;
                gridRight2D *= cmap.GridSize;
                bool arrows = Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode == EditMode.Translate && Client.Instance.Frontend.Renderer.ObjectRenderer.MovementMode == TranslationMode.Arrows;

                float dotZ = MathF.Abs(Vector3.Dot(Vector3.UnitZ, this.ClientCamera.Direction));
                float dotX = MathF.Abs(Vector3.Dot(Vector3.UnitX, this.ClientCamera.Direction));
                float dotY = MathF.Abs(Vector3.Dot(Vector3.UnitY, this.ClientCamera.Direction));

                bool z = dotZ > dotX && dotZ > dotY;
                bool y = dotY > dotX;

                switch (args.Key)
                {
                    case Keys.UpArrow or Keys.Keypad8:
                    {
                        if (!arrows)
                        {
                            gridMoveVec += new Vector3(gridForward2D.X, gridForward2D.Y, 0.0f);
                        }
                        else
                        {
                            gridMoveVec += z ? new Vector3(gridForward2D.X, gridForward2D.Y, 0.0f) : Vector3.UnitZ;
                        }

                        break;
                    }

                    case Keys.DownArrow or Keys.Keypad2:
                    {
                        if (!arrows)
                        {
                            gridMoveVec -= new Vector3(gridForward2D.X, gridForward2D.Y, 0.0f);
                        }
                        else
                        {
                            gridMoveVec -= z ? new Vector3(gridForward2D.X, gridForward2D.Y, 0.0f) : Vector3.UnitZ;
                        }

                        break;
                    }

                    case Keys.LeftArrow or Keys.Keypad4:
                    {
                        gridMoveVec -= new Vector3(gridRight2D.X, gridRight2D.Y, 0.0f);
                        break;
                    }

                    case Keys.RightArrow or Keys.Keypad6:
                    {
                        gridMoveVec += new Vector3(gridRight2D.X, gridRight2D.Y, 0.0f);
                        break;
                    }

                    case Keys.PageUp or Keys.Keypad9:
                    {
                        if (!arrows && !this.IsOrtho)
                        {
                            gridMoveVec += Vector3.UnitZ * cmap.GridSize;
                        }
                        else
                        {
                            gridMoveVec += z ? new Vector3(gridForward2D.X, gridForward2D.Y, 0.0f) : Vector3.UnitZ;
                            gridMoveVec += new Vector3(gridRight2D.X, gridRight2D.Y, 0.0f);
                        }

                        break;
                    }

                    case Keys.PageDown or Keys.Keypad3:
                    {
                        if (!arrows && !this.IsOrtho)
                        {
                            gridMoveVec -= Vector3.UnitZ * cmap.GridSize;
                        }
                        else
                        {
                            gridMoveVec -= z ? new Vector3(gridForward2D.X, gridForward2D.Y, 0.0f) : Vector3.UnitZ;
                            gridMoveVec += new Vector3(gridRight2D.X, gridRight2D.Y, 0.0f);
                        }

                        break;
                    }

                    case Keys.Home or Keys.Keypad7:
                    {
                        if (!arrows)
                        {
                            rot = -MathF.PI / 2f;
                        }
                        else
                        {
                            gridMoveVec += z ? new Vector3(gridForward2D.X, gridForward2D.Y, 0.0f) : Vector3.UnitZ;
                            gridMoveVec -= new Vector3(gridRight2D.X, gridRight2D.Y, 0.0f);
                        }

                        break;
                    }

                    case Keys.End or Keys.Keypad1:
                    {
                        if (!arrows)
                        {
                            rot = MathF.PI / 2f;
                        }
                        else
                        {
                            gridMoveVec -= z ? new Vector3(gridForward2D.X, gridForward2D.Y, 0.0f) : Vector3.UnitZ;
                            gridMoveVec -= new Vector3(gridRight2D.X, gridRight2D.Y, 0.0f);
                        }

                        break;
                    }
                }

                if ((gridMoveVec - Vector3.Zero).Length() > 1e-7)
                {
                    if (Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Space))
                    {
                        gridMoveVec /= 16;
                    }

                    if (Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Count > 0)
                    {
                        bool alt = Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.LeftAlt);
                        PacketChangeObjectModelMatrix pcomm = new PacketChangeObjectModelMatrix()
                        {
                            MovementInducerID = Client.Instance.ID,
                            Type = PacketChangeObjectModelMatrix.ChangeType.Position,
                            MovedObjects = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Select(o => (o.MapID, o.ID, new Vector4(
                                alt ? SnapToGrid(o.Position + gridMoveVec, cmap.GridSize) : o.Position + gridMoveVec, 1.0f
                            ))).ToList()
                        };

                        pcomm.Send();
                    }
                }

                if (MathF.Abs(rot) > 1e-7)
                {
                    if (Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Space))
                    {
                        rot /= 16;
                    }

                    if (Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Count > 0)
                    {
                        PacketChangeObjectModelMatrix pcomm = new PacketChangeObjectModelMatrix()
                        {
                            MovementInducerID = Client.Instance.ID,
                            Type = PacketChangeObjectModelMatrix.ChangeType.Rotation,
                            MovedObjects = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Select(o =>
                            {
                                Quaternion q = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rot) * o.Rotation;
                                return (o.MapID, o.ID, new Vector4(q.X, q.Y, q.Z, q.W));
                            }).ToList()
                        };

                        pcomm.Send();
                    }
                }
            }
        }

        public bool IsCameraMMBDown() => Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Middle) || (Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Left) && this.CameraControlMode != CameraControlMode.Standard);

        public void HandleCamera()
        {
            bool mouseCap = ImGuiNET.ImGui.GetIO().WantCaptureMouse;
            bool spacebarDown = !ImGuiNET.ImGui.GetIO().WantCaptureKeyboard && Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Space);
            bool drag = Client.Instance.Frontend.Renderer.SelectionManager.IsDraggingObjects;
            float sensitivity = Client.Instance.Settings.Sensitivity;
            if (!ImGuiNET.ImGui.GetIO().WantCaptureKeyboard && !drag && Client.Instance.CurrentMap != null)
            {
                Vector2 cameraForward2D = this.IsOrtho ? Vector2.UnitY : this.ClientCamera.Direction.Xy().Normalized();
                if (cameraForward2D.HasAnyNans())
                {
                    cameraForward2D = Vector2.UnitY;
                }

                Vector2 cameraRight2D = cameraForward2D.PerpendicularRight();
                Vector3 keyboardMoveVec = default;

                if (Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.UpArrow) || Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Keypad8))
                {
                    keyboardMoveVec += new Vector3(cameraForward2D.X, cameraForward2D.Y, 0.0f);
                }

                if (Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.DownArrow) || Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Keypad2))
                {
                    keyboardMoveVec -= new Vector3(cameraForward2D.X, cameraForward2D.Y, 0.0f);
                }

                if (Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.LeftArrow) || Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Keypad4))
                {
                    keyboardMoveVec -= new Vector3(cameraRight2D.X, cameraRight2D.Y, 0.0f);
                }

                if (Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.RightArrow) || Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Keypad6))
                {
                    keyboardMoveVec += new Vector3(cameraRight2D.X, cameraRight2D.Y, 0.0f);
                }

                if (Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.PageUp) || Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Keypad9))
                {
                    if (!this.IsOrtho)
                    {
                        keyboardMoveVec += Vector3.UnitZ;
                    }
                    else
                    {
                        float m = spacebarDown ? 0.3f : 1f;
                        this.camera2dzoom /= 1.0f + (m * (1f / 60f));
                        this.Resize(Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
                    }
                }

                if (Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.PageDown) || Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Keypad3))
                {
                    if (!this.IsOrtho)
                    {
                        keyboardMoveVec -= Vector3.UnitZ;
                    }
                    else
                    {
                        float m = spacebarDown ? 0.3f : 1f;
                        this.camera2dzoom *= 1.0f + (m * (1f / 60f));
                        this.Resize(Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
                    }
                }

                if (Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Keypad5))
                {
                    if (!this.IsOrtho)
                    {
                        keyboardMoveVec += this.ClientCamera.Direction;
                    }
                    else
                    {
                        float m = spacebarDown ? 0.3f : 1f;
                        this.camera2dzoom /= 1.0f + (m * (1f / 60f));
                        this.Resize(Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
                    }
                }

                if (Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Keypad0))
                {
                    if (!this.IsOrtho)
                    {
                        keyboardMoveVec -= this.ClientCamera.Direction;
                    }
                    else
                    {
                        float m = spacebarDown ? 0.3f : 1f;
                        this.camera2dzoom *= 1.0f + (m * (1f / 60f));
                        this.Resize(Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
                    }
                }

                if (Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.LeftShift) || Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.RightShift))
                {
                    if (spacebarDown)
                    {
                        keyboardMoveVec /= 3;
                    }

                    keyboardMoveVec *= sensitivity;
                    if (!keyboardMoveVec.Equals(default))
                    {
                        Vector3 newPosVec = this.ClientCamera.Position + (keyboardMoveVec * (1f / 60f) * 3.0f);
                        this.ClientCamera.Position = newPosVec;
                        this.ClientCamera.RecalculateData();
                    }
                }
            }

            if (!drag && !mouseCap && this.IsCameraMMBDown() && !this._mmbPressed)
            {
                this._mmbPressed = true;
                this._cameraBeingMoved = this.IsOrtho || Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.LeftShift) || this.CameraControlMode == CameraControlMode.Move || Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.RightShift);
                this._cameraBeingRotated = !this._cameraBeingMoved;
                if (this._cameraBeingRotated)
                {
                    this._pt = this.ClientCamera.Position + (this.ClientCamera.Direction * 10.0f);
                }
            }

            if (this._cameraBeingMoved)
            {
                Vector3 camUp = this.ClientCamera.Up;
                Vector3 camRight = this.ClientCamera.Right;
                int dx = (int)(Client.Instance.Frontend.MouseX - this._lastMouseX);
                int dy = (int)(Client.Instance.Frontend.MouseY - this._lastMouseY);
                Vector3 camMovement = default;
                if (dx != 0)
                {
                    camMovement -= camRight * dx;
                }

                if (dy != 0)
                {
                    camMovement += camUp * dy;
                }

                if (spacebarDown)
                {
                    camMovement /= 10;
                }

                camMovement *= sensitivity;
                if (this.IsOrtho)
                {
                    camMovement *= this.camera2dzoom * 100f;
                }

                if (!camMovement.Equals(default))
                {
                    // Not a bug: note that the timestep here is fixed for smooth motion regardless of framerate!
                    this.ClientCamera.Position += camMovement * 1/60f * 0.75f; 
                    this.ClientCamera.RecalculateData();
                }
            }

            if (this._cameraBeingRotated)
            {
                Vector3 point2camera = this.ClientCamera.Position - this._pt;
                Vector3 camUp = this.ClientCamera.Up;
                Vector3 camRight = this.ClientCamera.Right;

                int dx = (int)(Client.Instance.Frontend.MouseX - this._lastMouseX);
                int dy = (int)(Client.Instance.Frontend.MouseY - this._lastMouseY);
                float mod = spacebarDown ? 1.5f : 15f;
                mod *= sensitivity;
                Vector3 np2c;
                if (dy != 0)
                {
                    Quaternion q = Quaternion.CreateFromAxisAngle(camRight, -dy * (1f / 60f) * mod * MathF.PI / 180);
                    np2c = Vector4.Transform(new Vector4(point2camera, 1.0f), q).Xyz();
                    float dot = Vector3.Dot(Vector3.UnitZ, -np2c.Normalized());
                    Vector3 oldUp = this.ClientCamera.Up;
                    if (MathF.Abs(dot) < 0.999f)
                    {
                        Vector3 oldPos = this.ClientCamera.Position;
                        Vector3 oldDir = this.ClientCamera.Direction;
                        this.ClientCamera.Position = this._pt + np2c;
                        point2camera = this.ClientCamera.Position - this._pt;
                        this.ClientCamera.Direction = -np2c.Normalized();
                        this.ClientCamera.RecalculateData(assumedUpAxis: Vector3.UnitZ);
                        Vector3 newUp = this.ClientCamera.Up;
                        if (Vector3.Dot(oldUp, newUp) < 0)
                        {
                            this.ClientCamera.Position = oldPos;
                            this.ClientCamera.Direction = oldDir;
                            this.ClientCamera.RecalculateData(assumedUpAxis: Vector3.UnitZ);
                            point2camera = this.ClientCamera.Position - this._pt;
                        }
                    }
                }

                if (dx != 0)
                {
                    float dot = 1.0f - MathF.Abs(Vector3.Dot(Vector3.UnitZ, this.ClientCamera.Direction));
                    if (dot < 0.01f)
                    {
                        mod *= 0.1f;
                    }

                    Quaternion q = Quaternion.CreateFromAxisAngle(camUp, -dx * (1f / 60f) * mod * MathF.PI / 180);
                    np2c = Vector4.Transform(new Vector4(point2camera, 1.0f), q).Xyz();
                    this.ClientCamera.Position = this._pt + np2c;
                    this.ClientCamera.Direction = -np2c.Normalized();
                    this.ClientCamera.RecalculateData(assumedUpAxis: Vector3.UnitZ);
                }
            }

            if (this._mouseWheelDY != 0)
            {
                float mod = spacebarDown ? 0.1f : 1.0f;
                mod *= sensitivity;
                if (!this.IsOrtho)
                {
                    this.ClientCamera.MoveCamera((this.ClientCamera.Direction * this._mouseWheelDY * mod) + this.ClientCamera.Position, true);
                }
                else
                {
                    mod = spacebarDown ? 0.05f : 0.3f;
                    mod *= sensitivity;
                    if (MathF.Sign(this._mouseWheelDY) > 0)
                    {
                        this.camera2dzoom /= 1.0f + mod;
                    }
                    else
                    {
                        this.camera2dzoom *= 1.0f + mod;
                    }

                    this.Resize(Client.Instance.Frontend.Width, Client.Instance.Frontend.Height);
                }

                this._mouseWheelDY = 0;
            }

            if (!mouseCap && !this.IsCameraMMBDown() && this._mmbPressed)
            {
                this._mmbPressed = this._cameraBeingMoved = this._cameraBeingRotated = false;
            }

            this._lastMouseX = (int)Client.Instance.Frontend.MouseX;
            this._lastMouseY = (int)Client.Instance.Frontend.MouseY;
        }

        public static bool TryHitscanGround(Camera cam, out Vector3 cursor2World)
        {
            bool lookingAtGround = true;
            cursor2World = default;
            Ray cursorRay = Client.Instance.Frontend.Renderer.MapRenderer.RayFromCursor();
            float deltaZ = cursorRay.Origin.Z;
            deltaZ /= cursorRay.Direction.Z;
            if (deltaZ >= 0)
            {
                lookingAtGround = false;
            }
            else
            {
                cursor2World = cursorRay.Origin + (cursorRay.Direction * -deltaZ);
            }

            return lookingAtGround;
        }

        public static Vector3 SnapToGrid(Vector3 world, float gridSize, Vector3 isBigObjectAxis = default)
        {
            float halfGrid = gridSize / 2;
            int fX = (int)MathF.Round(world.X / gridSize);
            int fY = (int)MathF.Round(world.Y / gridSize);
            int fZ = (int)MathF.Round(world.Z / gridSize);

            Vector3 ret = new Vector3(
                fX - (isBigObjectAxis.X * halfGrid),
                fY - (isBigObjectAxis.Y * halfGrid),
                Client.Instance.Frontend.Renderer.MapRenderer?.IsOrtho ?? false ? world.Z : fZ - (isBigObjectAxis.Z * halfGrid)
            );

            return ret;
        }
    }

    public enum CameraControlMode
    {
        Standard,
        Move,
        Rotate
    }
}
