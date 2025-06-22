namespace VTT.Render
{
    using ImGuiNET;
    using System.Numerics;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using VTT.Control;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;
    using Plane = Util.Plane;
    using VTT.GLFW;

    public class SelectionManager
    {
        private bool _lmbDown;
        private int _initialLmbX;
        private int _lastLmbX;
        private int _initialLmbY;
        private int _lastLmbY;
        private bool _isBoxSelect;
        private bool _blockSelection;
        private Vector3 _rayInitialHit;
        private Vector3 _axisLockVector;
        private Vector3? _movementIntersectionValue;
        private MovementGizmoControlMode _moveMode;
        private float _initialAngle;
        private bool _deleteDown;

        public Vector3? HalfRenderVector { get; set; }
        public bool IsDraggingObjects => this._blockSelection;

        public List<MapObject> SelectedObjects { get; } = new List<MapObject>();
        public List<MapObject> BoxSelectCandidates { get; } = new List<MapObject>();

        public Vector3 ObjectMovementInitialHitLocation => this._rayInitialHit;
        public Vector3? ObjectMovementCurrentHitLocation => this._movementIntersectionValue;
        public List<Vector3> ObjectMovementPath { get; } = new List<Vector3>();

        public void Update()
        {
            Map cMap = Client.Instance.CurrentMap;
            if (cMap == null)
            {
                return;
            }

            if (!ImGui.GetIO().WantCaptureKeyboard && !this._deleteDown && Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Delete))
            {
                bool isAdmin = Client.Instance.IsAdmin;
                Guid cId = Client.Instance.ID;
                List<(Guid, Guid)> o2delete = (isAdmin ? this.SelectedObjects : this.SelectedObjects.Where(x => x.CanEdit(cId))).Select(o => (o.MapID, o.ID)).ToList();
                if (o2delete.Count > 0)
                {
                    PacketDeleteMapObject pdmo = new PacketDeleteMapObject() { DeletedObjects = o2delete, IsServer = false, Session = Client.Instance.SessionID };
                    pdmo.Send();
                }

                this._deleteDown = true;
            }

            for (int i = this.SelectedObjects.Count - 1; i >= 0; i--)
            {
                MapObject mo = this.SelectedObjects[i];
                if (mo.IsRemoved)
                {
                    this.SelectedObjects.Remove(mo);
                    continue;
                }
            }

            for (int i = this.BoxSelectCandidates.Count - 1; i >= 0; i--)
            {
                MapObject mo = this.BoxSelectCandidates[i];
                if (mo.IsRemoved)
                {
                    this.BoxSelectCandidates.Remove(mo);
                    continue;
                }
            }

            bool imGuiWantsMouse = ImGui.GetIO().WantCaptureMouse;
            if (Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode is not (EditMode.FOW or EditMode.Measure or EditMode.Draw or EditMode.FX or EditMode.Shadows2D) && Client.Instance.Frontend.Renderer.MapRenderer.CameraControlMode == CameraControlMode.Standard)
            {
                if (!imGuiWantsMouse && !this._lmbDown && Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Left))
                {
                    this._lmbDown = true;
                    this._isBoxSelect = false;
                    this._initialLmbX = (int)Client.Instance.Frontend.MouseX;
                    this._initialLmbY = (int)Client.Instance.Frontend.MouseY;

                    bool is2d = Client.Instance.Frontend.Renderer.MapRenderer.IsOrtho;
                    float zoomortho = Client.Instance.Frontend.Renderer.MapRenderer.ZoomOrtho;

                    EditMode mode = Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode;
                    if (mode != EditMode.Select && this.SelectedObjects.Count > 0)
                    {
                        Vector3 min = this.SelectedObjects[0].Position;
                        Vector3 max = this.SelectedObjects[0].Position;
                        for (int i = 1; i < this.SelectedObjects.Count; i++)
                        {
                            MapObject mo = this.SelectedObjects[i];
                            min = Vector3.Min(min, mo.Position);
                            max = Vector3.Max(max, mo.Position);
                        }

                        Vector3 half = (max - min) / 2;
                        half = min + half;
                        Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
                        Ray r = Client.Instance.Frontend.Renderer.MapRenderer.RayFromCursor();
                        if (mode is EditMode.Translate or EditMode.Scale) // Translate and scale have the same selection BBs and mouse->world rules
                        {
                            if (mode == EditMode.Translate && Client.Instance.Frontend.Renderer.ObjectRenderer.MovementMode == TranslationMode.Arrows)
                            {
                                float dotZ = MathF.Abs(Vector3.Dot(Vector3.UnitZ, cam.Direction));
                                float dotX = MathF.Abs(Vector3.Dot(Vector3.UnitX, cam.Direction));
                                float dotY = MathF.Abs(Vector3.Dot(Vector3.UnitY, cam.Direction));

                                bool z = dotZ > dotX && dotZ > dotY;
                                bool y = dotY > dotX;

                                Vector3 boxVector =
                                    z ? new Vector3(0.25f, 0.5f, 0.05f) :
                                    y ? new Vector3(0.5f, 0.05f, 0.25f) :
                                        new Vector3(0.05f, 0.5f, 0.25f);

                                Vector4 offsetVector =
                                    z ? new Vector4(0, -0.75f, 0, 1.0f) :
                                    y ? new Vector4(-0.75f, 0, 0, 1.0f) :
                                        new Vector4(0, 0, -0.75f, 1.0f);

                                Vector3 axis = z ? Vector3.UnitZ : y ? Vector3.UnitY : Vector3.UnitX;
                                for (int i = 0; i < 8; ++i)
                                {
                                    Quaternion q = Quaternion.CreateFromAxisAngle(axis, MathF.PI * (i * 0.25f));
                                    Vector4 offset4 = Vector4.Transform(offsetVector, q);
                                    Vector3 offset = offset4.Xyz() / offset4.W;
                                    BBBox box = new BBBox(-boxVector, boxVector, q).Scale(0.5f);
                                    Vector3? intersection = box.Intersects(r, half + offset);
                                    if (intersection.HasValue)
                                    {
                                        this._blockSelection = true;
                                        this._moveMode = MovementGizmoControlMode.Blocking;

                                        offset.X = MathF.Abs(offset.X) <= 1e-7 ? 0 : offset.X;
                                        offset.Y = MathF.Abs(offset.Y) <= 1e-7 ? 0 : offset.Y;
                                        offset.Z = MathF.Abs(offset.Z) <= 1e-7 ? 0 : offset.Z;

                                        float mX = 1 * MathF.Sign(offset.X);
                                        float mY = 1 * MathF.Sign(offset.Y);
                                        float mZ = 1 * MathF.Sign(offset.Z);
                                        foreach (MapObject mo in this.SelectedObjects)
                                        {
                                            mo.Position += new Vector3(mX, mY, mZ);
                                        }

                                        List<(Guid, Guid, Vector4)> changes = this.SelectedObjects.Select(o => (o.MapID, o.ID, new Vector4(o.Position, 1.0f))).ToList();
                                        PacketChangeObjectModelMatrix pmo = new PacketChangeObjectModelMatrix() { IsServer = false, Session = Client.Instance.SessionID, MovedObjects = changes, MovementInducerID = Client.Instance.ID, Type = PacketChangeObjectModelMatrix.ChangeType.Position };
                                        pmo.Send();

                                        break;
                                    }
                                }
                            }
                            else
                            {
                                Matrix4x4 viewProj = cam.ViewProj;
                                Vector4 posScreen = Vector4.Transform(new Vector4(half, 1.0f), viewProj);
                                float vScale = 0.2f * posScreen.W;
                                if (is2d)
                                {
                                    vScale = 150.0f * zoomortho;
                                }

                                AABox zArrow = new AABox(-0.077927f, -0.077927f, -0.5f, 0.077927f, 0.077927f, 0.5f).Offset(new Vector3(0, 0, 0.5f)).Scale(vScale).Offset(half);
                                AABox xArrow = new AABox(-0.5f, -0.077927f, -0.077927f, 0.5f, 0.077927f, 0.077927f).Offset(new Vector3(0.5f, 0, 0)).Scale(vScale).Offset(half);

                                AABox yArrow = new AABox(-0.077927f, -0.5f, -0.077927f, 0.077927f, 0.5f, 0.077927f).Offset(new Vector3(0, 0.5f, 0)).Scale(vScale).Offset(half);

                                AABox zBox = new AABox(-0.125000f, -0.125000f, is2d ? -0.125f : -0.012500f, 0.125000f, 0.125000f, is2d ? 0.125f : 0.012500f).Offset(new Vector3(is2d ? 0 : 0.5f, is2d ? 0 : 0.5f, 0)).Scale(vScale).Offset(half);
                                AABox xBox = new AABox(-0.012500f, -0.125000f, -0.125000f, 0.012500f, 0.125000f, 0.125000f).Offset(new Vector3(0f, 0.5f, 0.5f)).Scale(vScale).Offset(half);
                                AABox yBox = new AABox(-0.125000f, -0.012500f, -0.125000f, 0.125000f, 0.012500f, 0.125000f).Offset(new Vector3(0.5f, 0f, 0.5f)).Scale(vScale).Offset(half);
                                AABox cBox = new AABox(-0.15f, -0.15f, -0.15f, 0.15f, 0.15f, 0.15f).Scale(vScale).Offset(half);

                                Plane sPlane = new Plane(-cam.Direction, 1.0f);

                                Vector3? intersectionZ = is2d ? null : zArrow.Intersects(r);
                                Vector3? intersectionX = xArrow.Intersects(r);
                                Vector3? intersectionY = yArrow.Intersects(r);
                                Vector3? intersectionBoxZ = zBox.Intersects(r);
                                Vector3? intersectionBoxX = is2d ? null : xBox.Intersects(r);
                                Vector3? intersectionBoxY = is2d ? null : yBox.Intersects(r);
                                Vector3? intersectionCenter = is2d ? null : cBox.Intersects(r);
                                Vector3? intersectionPlane = is2d ? null : sPlane.Intersect(r, half);

                                if (intersectionPlane.HasValue)
                                {
                                    if (mode != EditMode.Scale)
                                    {
                                        intersectionPlane = null;
                                    }
                                    else
                                    {
                                        intersectionCenter = null;
                                        float d = (intersectionPlane.Value - half).Length();
                                        float dMax = 1.228f * vScale;
                                        float dMin = 1.168f * vScale;
                                        if (d > dMax || d < dMin)
                                        {
                                            intersectionPlane = null;
                                        }
                                    }
                                }

                                float dZ = intersectionZ.HasValue ? (cam.Position - intersectionZ.Value).Length() : float.PositiveInfinity;
                                float dX = intersectionX.HasValue ? (cam.Position - intersectionX.Value).Length() : float.PositiveInfinity;
                                float dY = intersectionY.HasValue ? (cam.Position - intersectionY.Value).Length() : float.PositiveInfinity;

                                float bZ = intersectionBoxZ.HasValue ? (cam.Position - intersectionBoxZ.Value).Length() : float.PositiveInfinity;
                                float bX = intersectionBoxX.HasValue ? (cam.Position - intersectionBoxX.Value).Length() : float.PositiveInfinity;
                                float bY = intersectionBoxY.HasValue ? (cam.Position - intersectionBoxY.Value).Length() : float.PositiveInfinity;

                                float iC = intersectionCenter.HasValue ? (cam.Position - intersectionCenter.Value).Length() : float.PositiveInfinity;
                                float iP = intersectionPlane.HasValue ? (cam.Position - intersectionPlane.Value).Length() : float.PositiveInfinity;

                                if (intersectionZ.HasValue || intersectionX.HasValue || intersectionY.HasValue || intersectionBoxZ.HasValue || intersectionBoxX.HasValue || intersectionBoxY.HasValue || intersectionCenter.HasValue || intersectionPlane.HasValue)
                                {
                                    bool z = dZ <= dX && dZ <= dY && dZ <= bZ && dZ <= bX && dZ <= bY && dZ <= iC;
                                    bool x = dX <= dZ && dX <= dY && dX <= bZ && dX <= bX && dX <= bY && dX <= iC;
                                    bool y = dY <= dZ && dY <= dX && dY <= bZ && dY <= bX && dY <= bY && dY <= iC;
                                    bool bz = bZ <= dX && bZ <= dY && bZ <= dZ && bZ <= bX && bZ <= bY && bZ <= iC;
                                    bool bx = bX <= dZ && bX <= dY && bX <= dZ && bX <= bZ && bX <= bY && bX <= iC;
                                    bool by = bY <= dX && bY <= dZ && bY <= dZ && bY <= bX && bY <= bZ && bY <= iC;
                                    bool ic = iC <= dX && iC <= dZ && iC <= dZ && iC <= bX && iC <= bZ && iC <= bY;
                                    bool ip = !(z || x || y || bz || bz || bx || by || ic) && intersectionPlane.HasValue;

                                    this._rayInitialHit =
                                        ip ? intersectionPlane.Value :
                                        z ? intersectionZ.Value :
                                        x ? intersectionX.Value :
                                        y ? intersectionY.Value :
                                        bz ? intersectionBoxZ.Value :
                                        bx ? intersectionBoxX.Value :
                                        by ? intersectionBoxY.Value : intersectionCenter.Value;

                                    this._blockSelection = true;
                                    this._axisLockVector =
                                        z ? Vector3.UnitZ :
                                        x ? Vector3.UnitX :
                                        y ? Vector3.UnitY :
                                        bz ? new Vector3(1, 1, 0) :
                                        bx ? new Vector3(0, 1, 1) :
                                        by ? new Vector3(1, 0, 1) : Vector3.One;

                                    this._moveMode = z || x || y ? MovementGizmoControlMode.SingleAxis : bz || bx || by ? MovementGizmoControlMode.Plane : MovementGizmoControlMode.CameraViewPlane;
                                    if (mode == EditMode.Translate)
                                    {
                                        foreach (MapObject mo in this.SelectedObjects)
                                        {
                                            mo.ClientDragMoveAccumulatedPosition = mo.Position;
                                            mo.ClientDragMoveResetInitialPosition = mo.Position;
                                        }

                                        if (Client.Instance.Frontend.Renderer.ObjectRenderer.MovementMode == TranslationMode.Path)
                                        {
                                            this.ObjectMovementPath.Clear();
                                            this.ObjectMovementPath.Add(this.SelectedObjects[0].Position);
                                        }
                                    }
                                    else
                                    {
                                        foreach (MapObject mo in this.SelectedObjects)
                                        {
                                            mo.ClientDragMoveAccumulatedPosition = mo.Scale;
                                            mo.ClientDragMoveResetInitialPosition = mo.Scale;
                                        }
                                    }
                                }
                            }
                        }

                        if (mode == EditMode.Rotate)
                        {
                            Matrix4x4 viewProj = cam.ViewProj;
                            Vector4 posScreen = Vector4.Transform(new Vector4(half, 1.0f), viewProj);
                            float vScale = 0.4f * posScreen.W;
                            float radius = 0.197f * posScreen.W;
                            float radiusMin = 0.185f * posScreen.W;
                            float sRMax = 0.65f;
                            float sRMin = 0.58f;
                            if (is2d)
                            {
                                radius = 150f * zoomortho;
                                vScale = 150f * zoomortho;
                                sRMin = 0;
                                sRMax = 1f;
                            }

                            AABox zBox = new AABox(-0.490000f, -0.490000f, -0.01f, 0.490000f, 0.490000f, 0.01f).Scale(vScale).Offset(half);
                            AABox xBox = new AABox(-0.01f, -0.490000f, -0.490000f, 0.01f, 0.490000f, 0.490000f).Scale(vScale).Offset(half);
                            AABox yBox = new AABox(-0.490000f, -0.01f, -0.490000f, 0.490000f, 0.01f, 0.490000f).Scale(vScale).Offset(half);

                            Vector3 a = Vector3.Cross(Vector3.UnitZ, -cam.Direction);
                            Quaternion q = is2d ? Quaternion.Identity : new Quaternion(a, 1 + Vector3.Dot(Vector3.UnitZ, -cam.Direction));
                            BBBox p = new BBBox(new Vector3(-0.490000f, -0.490000f, -0.01f), new Vector3(0.490000f, 0.490000f, 0.01f), q).Scale(new Vector3(2.0f * vScale));

                            Vector3? pIntersection = p.Intersects(r, half);

                            Vector3? sIntersection = r.IntersectsSphere(half, radius);

                            if (sIntersection.HasValue)
                            {
                                Vector3? zIntersection = is2d ? null : TorusIntersection(r, half, Vector3.UnitZ, radiusMin, radius);
                                Vector3? xIntersection = is2d ? null : TorusIntersection(r, half, Vector3.UnitX, radiusMin, radius);
                                Vector3? yIntersection = is2d ? null : TorusIntersection(r, half, Vector3.UnitY, radiusMin, radius);

                                /*
                                Matrix4 zMat = Matrix4.CreateScale(vScale) * Matrix4.CreateTranslation(half);
                                Matrix4 yMat = Matrix4.CreateScale(vScale) * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(90)) * Matrix4.CreateTranslation(half);
                                Matrix4 xMat = Matrix4.CreateScale(vScale) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(90)) * Matrix4.CreateTranslation(half);

                                Vector3? zIntersection = TorusIntersects(r, zMat, zBox);
                                Vector3? xIntersection = TorusIntersects(r, xMat, xBox);
                                Vector3? yIntersection = TorusIntersects(r, yMat, yBox);
                                */

                                float dZ = zIntersection.HasValue ? (cam.Position - zIntersection.Value).Length() : float.PositiveInfinity;
                                float dX = xIntersection.HasValue ? (cam.Position - xIntersection.Value).Length() : float.PositiveInfinity;
                                float dY = yIntersection.HasValue ? (cam.Position - yIntersection.Value).Length() : float.PositiveInfinity;

                                bool rZ = zIntersection.HasValue;
                                bool rX = xIntersection.HasValue;
                                bool rY = yIntersection.HasValue;

                                dZ = rZ ? dZ : float.PositiveInfinity;
                                dX = rX ? dX : float.PositiveInfinity;
                                dY = rY ? dY : float.PositiveInfinity;

                                bool z = dZ < dX && dZ < dY && rZ;
                                bool x = dX < dZ && dX < dY && rX;
                                bool y = dY < dX && dY < dZ && rY;

                                if (x || y || z)
                                {
                                    Vector3 intersection = x ? xIntersection.Value : y ? yIntersection.Value : zIntersection.Value;
                                    this._axisLockVector =
                                        z ? MathF.Sign(cam.Direction.Z) * -Vector3.UnitZ :
                                        x ? MathF.Sign(cam.Direction.X) * -Vector3.UnitX :
                                        MathF.Sign(cam.Direction.Y) * -Vector3.UnitY;

                                    this._rayInitialHit = intersection;
                                    Vector2 screenIntersection = cam.ToScreenspace(intersection).Xy();
                                    Vector2 screenHalf = cam.ToScreenspace(half).Xy();
                                    Vector2 screenDelta = screenIntersection - screenHalf;
                                    float dot = -screenDelta.Y;
                                    float det = -screenDelta.X;
                                    this._initialAngle = MathF.Atan2(det, dot);
                                    this.HalfRenderVector = half;
                                    this._moveMode = MovementGizmoControlMode.SingleAxis;
                                    foreach (MapObject mo in this.SelectedObjects)
                                    {
                                        mo.ClientDragMoveResetInitialPosition = mo.Position;
                                        mo.ClientDragRotateInitialRotation = mo.Rotation;
                                    }

                                    this._blockSelection = true;
                                }
                            }

                            if (!this._blockSelection) // Test plane
                            {
                                if (pIntersection.HasValue && (pIntersection.Value - half).Length() <= sRMax * vScale && (pIntersection.Value - half).Length() >= sRMin * vScale)
                                {
                                    Vector3 intersection = pIntersection.Value;
                                    this._axisLockVector = -cam.Direction;
                                    this._rayInitialHit = intersection;
                                    Vector2 screenIntersection = cam.ToScreenspace(intersection).Xy();
                                    Vector2 screenHalf = cam.ToScreenspace(half).Xy();
                                    Vector2 screenDelta = screenIntersection - screenHalf;
                                    float dot = -screenDelta.Y;
                                    float det = -screenDelta.X;
                                    this._initialAngle = MathF.Atan2(det, dot);
                                    this.HalfRenderVector = half;
                                    this._moveMode = MovementGizmoControlMode.SingleAxis;
                                    foreach (MapObject mo in this.SelectedObjects)
                                    {
                                        mo.ClientDragMoveResetInitialPosition = mo.Position;
                                        mo.ClientDragRotateInitialRotation = mo.Rotation;
                                    }

                                    this._blockSelection = true;
                                }
                            }
                        }
                    }

                    if (Client.Instance.Frontend.GameHandle.IsAnyAltDown())
                    {
                        if (!this._blockSelection)
                        {
                            this._blockSelection = true;
                            this._moveMode = MovementGizmoControlMode.Blocking;
                            Client.Instance.Frontend.Renderer.PingRenderer.BeginPingUI(Client.Instance.Frontend.GameHandle.IsAnyControlDown());
                        }
                    }
                }

                if (!this._blockSelection && !this._isBoxSelect && (this._initialLmbX != this._lastLmbX || this._initialLmbY != this._lastLmbY))
                {
                    this._isBoxSelect = true;
                }

                if (this._moveMode != MovementGizmoControlMode.Blocking)
                {
                    this.HandleMovementGizmo();
                    this.HandleRotationGizmo(cMap);
                }

                this.BoxSelectCandidates.Clear();
                if (this._lmbDown && this._isBoxSelect)
                {
                    int minX = Math.Min(this._initialLmbX, (int)Client.Instance.Frontend.MouseX);
                    int maxX = Math.Max(this._initialLmbX, (int)Client.Instance.Frontend.MouseX);
                    int minY = Math.Min(this._initialLmbY, (int)Client.Instance.Frontend.MouseY);
                    int maxY = Math.Max(this._initialLmbY, (int)Client.Instance.Frontend.MouseY);
                    RectangleF rect = new RectangleF(minX, minY, maxX - minX, maxY - minY);
                    foreach (MapObject mo in cMap.IterateObjects(Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer))
                    {
                        if (!mo.ClientRenderedThisFrame)
                        {
                            continue;
                        }

                        if (Client.Instance.IsAdmin || mo.CanEdit(Client.Instance.ID) || Client.Instance.IsObserver)
                        {
                            BBBox box = mo.ClientRaycastOOBB;
                            Vector3 moPos = mo.Position;
                            if (TriRectTester.Intersects(in rect, in box, in moPos, Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera))
                            {
                                this.BoxSelectCandidates.Add(mo);
                            }
                        }
                    }
                }

                if (this._lmbDown && !Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Left))
                {
                    this._lmbDown = false;
                    if (!this._blockSelection)
                    {
                        this.ProcessSelection();
                    }
                    else
                    {
                        if (this._moveMode != MovementGizmoControlMode.Blocking)
                        {
                            EditMode em = Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode;
                            if (em != EditMode.Select)
                            {
                                if (em == EditMode.Translate && Client.Instance.Frontend.Renderer.ObjectRenderer.MovementMode == TranslationMode.Path)
                                {
                                    List<Vector3> pathCpy = new List<Vector3>(this.ObjectMovementPath);
                                    if (this.SelectedObjects.Count > 0)
                                    {
                                        pathCpy.Add(this.SelectedObjects[0].Position);
                                    }
                                    else // Should never happen but just in case
                                    {
                                        pathCpy.Add(Client.Instance.Frontend.Renderer.MapRenderer.GetTerrainCursorOrPointAlongsideView());
                                    }

                                    new PacketObjectPathMovement() { MapID = cMap.ID, ObjectIDs = this.SelectedObjects.Select(o => o.ID).ToList(), Path = pathCpy }.Send(); // Copy the path bc it will be cleared this frame before the packet gets serialized
                                }
                                else
                                {
                                    List<(Guid, Guid, Vector4)> changes = this.SelectedObjects.Select(o => (o.MapID, o.ID, em == EditMode.Translate ? new Vector4(o.Position, 1.0f) : em == EditMode.Scale ? new Vector4(o.Scale, 1.0f) : new Vector4(o.Rotation.X, o.Rotation.Y, o.Rotation.Z, o.Rotation.W))).ToList();
                                    PacketChangeObjectModelMatrix pmo = new PacketChangeObjectModelMatrix() { IsServer = false, Session = Client.Instance.SessionID, MovedObjects = changes, MovementInducerID = Client.Instance.ID, Type = (PacketChangeObjectModelMatrix.ChangeType)((int)em - 1) };
                                    pmo.Send();
                                }
                            }

                            if (em == EditMode.Rotate && this.SelectedObjects.Count > 1)
                            {
                                List<(Guid, Guid, Vector4)> changes = this.SelectedObjects.Select(o => (o.MapID, o.ID, new Vector4(o.Position, 1.0f))).ToList();
                                PacketChangeObjectModelMatrix pmo = new PacketChangeObjectModelMatrix() { IsServer = false, Session = Client.Instance.SessionID, MovedObjects = changes, MovementInducerID = Client.Instance.ID, Type = PacketChangeObjectModelMatrix.ChangeType.Position };
                                pmo.Send();
                            }
                        }
                        else
                        {
                            this._moveMode = MovementGizmoControlMode.SingleAxis;
                            Client.Instance.Frontend.Renderer.PingRenderer.EndPingUI();
                        }
                    }

                    this.HalfRenderVector = null;
                    this._blockSelection = false;
                }
            }
            else
            {
                this.SelectedObjects.Clear();
                this.BoxSelectCandidates.Clear();
            }

            if (Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode == EditMode.FX && !imGuiWantsMouse)
            {
                if (!this._fxHandlerLmbDown && Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Left))
                {
                    this._fxHandlerLmbDown = true;
                    if (!Client.Instance.Frontend.Renderer.GuiRenderer.FXToEmitParticleSystemID.IsEmpty())
                    {
                        Vector3 cursorLoc = Client.Instance.Frontend.Renderer.MapRenderer.GetTerrainCursorOrPointAlongsideView();
                        new PacketAddFXParticle() { Location = cursorLoc, NumParticles = Client.Instance.Frontend.Renderer.GuiRenderer.FXNumToEmit, SystemID = Client.Instance.Frontend.Renderer.GuiRenderer.FXToEmitParticleSystemID }.Send();
                    }
                }

                if (this._fxHandlerLmbDown && !Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Left))
                {
                    this._fxHandlerLmbDown = false;
                }
            }

            if (this._deleteDown && !Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Delete))
            {
                this._deleteDown = false;
            }

            this._lastLmbX = (int)Client.Instance.Frontend.MouseX;
            this._lastLmbY = (int)Client.Instance.Frontend.MouseY;
        }

        private bool _fxHandlerLmbDown;
        private static Vector3? TorusIntersection(Ray r, Vector3 center, Vector3 torusAxis, float rMin, float rMax)
        {
            (Vector3, Vector3)? hits = r.IntersectsSphereBoth(center, rMax);
            if (hits.HasValue)
            {
                float halfRad = rMin + ((rMax - rMin) / 2f);
                Vector3 inverseAxis = Vector3.One - torusAxis;
                Vector3 proj1 = (hits.Value.Item1 * inverseAxis) + (center * torusAxis);
                Vector3 proj2 = (hits.Value.Item2 * inverseAxis) + (center * torusAxis);

                Vector3 s1c = center + ((proj1 - center).Normalized() * halfRad);
                Vector3 s2c = center + ((proj2 - center).Normalized() * halfRad);

                Vector3? h1 = r.IntersectsSphere(s1c, (rMax - rMin));
                Vector3? h2 = r.IntersectsSphere(s2c, (rMax - rMin));
                if (h1.HasValue && !h2.HasValue)
                {
                    return h1.Value;
                }

                if (h2.HasValue && !h1.HasValue)
                {
                    return h2.Value;
                }

                if (h1.HasValue && h2.HasValue)
                {
                    return (r.Origin - h1.Value).Length() < (r.Origin - h2.Value).Length() ? h1.Value : h2.Value;
                }
            }

            return null;
        }

        private void HandleRotationGizmo(Map m)
        {
            if (this._blockSelection && this._lmbDown && Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode == EditMode.Rotate && this.SelectedObjects.Count > 0)
            {
                Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
                Vector3? cw = Client.Instance.Frontend.Renderer.MapRenderer.GroundHitscanResult;
                Vector2 pCurrent;
                bool shift = Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.LeftShift) || Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.RightShift) || Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.Space);
                bool alt = Client.Instance.Frontend.GameHandle.IsKeyDown(Keys.LeftAlt);
                pCurrent = cw != null && !shift
                    ? cam.ToScreenspace(alt ? MapRenderer.SnapToGrid(m.GridType, cw.Value, m.GridSize) : cw.Value).Xy()
                    : new Vector2(Client.Instance.Frontend.MouseX, Client.Instance.Frontend.MouseY);

                Vector3 min = this.SelectedObjects[0].Position;
                Vector3 max = this.SelectedObjects[0].Position;
                for (int i = 1; i < this.SelectedObjects.Count; i++)
                {
                    MapObject mo = this.SelectedObjects[i];
                    min = Vector3.Min(min, mo.Position);
                    max = Vector3.Max(max, mo.Position);
                }

                Vector2 screenHalf = cam.ToScreenspace(this.HalfRenderVector.Value).Xy();
                Vector2 screenDelta = pCurrent - screenHalf;
                float dot = -screenDelta.Y;
                float det = -screenDelta.X;
                float angleDelta = MathF.Atan2(det, dot) - this._initialAngle;
                Quaternion q = Quaternion.CreateFromAxisAngle(this._axisLockVector, angleDelta);
                if (MathF.Abs(q.W) <= float.Epsilon || float.IsNaN(q.W) || float.IsInfinity(q.W))
                {
                    return;
                }

                foreach (MapObject mo in this.SelectedObjects)
                {
                    mo.Rotation = q * mo.ClientDragRotateInitialRotation;
                }

                if (this.SelectedObjects.Count > 1)
                {
                    foreach (MapObject mo in this.SelectedObjects)
                    {
                        Vector3 o2h = mo.ClientDragMoveResetInitialPosition - this.HalfRenderVector.Value;
                        o2h = Vector4.Transform(new Vector4(o2h, 1.0f), q).Xyz();
                        mo.Position = this.HalfRenderVector.Value + o2h;
                        float msx = MathF.Abs(mo.Scale.X) % (m.GridSize * 2);
                        float msy = MathF.Abs(mo.Scale.Y) % (m.GridSize * 2);
                        float msz = MathF.Abs(mo.Scale.Z) % (m.GridSize * 2);
                        Vector3 bigScale = new Vector3(
                            msx - 0.075f <= 0 || (m.GridSize * 2) - msx <= 0.075f ? 1 : 0,
                            msy - 0.075f <= 0 || (m.GridSize * 2) - msy <= 0.075f ? 1 : 0,
                            msz - 0.075f <= 0 || (m.GridSize * 2) - msz <= 0.075f ? 1 : 0
                        );

                        if (alt)
                        {
                            mo.Position = MapRenderer.SnapToGrid(m.GridType, mo.Position, m.GridSize, bigScale);
                        }
                    }
                }
            }
        }

        private void HandleMovementGizmo()
        {
            if (this.SelectedObjects.Count > 0 && this._blockSelection && this._lmbDown && (Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode == EditMode.Translate || Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode == EditMode.Scale) && Client.Instance.Frontend.Renderer.MapRenderer.CameraControlMode == CameraControlMode.Standard)
            {
                if (this._moveMode == MovementGizmoControlMode.Blocking)
                {
                    return;
                }

                Map map = Client.Instance.CurrentMap;
                if (map == null)
                {
                    return;
                }

                Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
                bool alt = Client.Instance.Frontend.GameHandle.IsAnyAltDown();
                bool scale = Client.Instance.Frontend.Renderer.ObjectRenderer.EditMode == EditMode.Scale;
                Vector3 min = this.SelectedObjects[0].Position;
                Vector3 max = this.SelectedObjects[0].Position;
                for (int i = 1; i < this.SelectedObjects.Count; i++)
                {
                    MapObject mo = this.SelectedObjects[i];
                    min = Vector3.Min(min, mo.Position);
                    max = Vector3.Max(max, mo.Position);
                }

                Vector3 half = (max - min) / 2;
                if (this._moveMode is MovementGizmoControlMode.SingleAxis or MovementGizmoControlMode.Plane)
                {
                    if (Client.Instance.Frontend.MouseX - this._lastLmbX != 0 || Client.Instance.Frontend.MouseY - this._lastLmbY != 0)
                    {
                        #region Old
                        /*Vector3 p0 = this._rayInitialHit;
                        float t = 0;
                        float dt = 1;
                        Vector3 q = new Vector3(Game.Instance.MouseX, Game.Instance.MouseY, 0.0f);
                        float l0;
                        int i = 0;
                        for (l0 = -1.0f; MathF.Abs(dt) > 1e-5f; t += dt)
                        {
                            Vector3 p = p0 + (t * this._axisLockVector);
                            p = cam.ToScreenspace(p);
                            p = p - q;
                            p.Z = 0;
                            float len = p.Length;
                            if (l0 < -0.5f) l0 = len;
                            if (len > l0) dt = -0.1f * dt;
                            l0 = len;
                            ++i;
                            if (i > 127)
                            {
                                break;
                            }
                        }

                        if (i <= 127)
                        {
                            if (!alt)
                            {
                                foreach (MapObject mo in this.SelectedObjects)
                                {
                                    if (scale)
                                    {
                                        mo.Scale += this._axisLockVector * t;
                                        mo.ClientDragMoveAccumulatedPosition = mo.Scale;
                                    }
                                    else
                                    {
                                        mo.Position += this._axisLockVector * t;
                                        mo.ClientDragMoveAccumulatedPosition = mo.Position;
                                    }
                                }
                            }
                            else
                            {
                                foreach (MapObject mo in this.SelectedObjects)
                                {
                                    if (scale)
                                    {
                                        mo.ClientDragMoveAccumulatedPosition += this._axisLockVector * t;
                                        mo.Scale = MapRenderer.SnapToGrid(mo.ClientDragMoveAccumulatedPosition, Client.Instance.CurrentMap.GridSize);
                                    }
                                    else
                                    {
                                        mo.ClientDragMoveAccumulatedPosition += this._axisLockVector * t;
                                        mo.Position = MapRenderer.SnapToGrid(mo.ClientDragMoveAccumulatedPosition, Client.Instance.CurrentMap.GridSize);
                                    }
                                }
                            }

                            this._rayInitialHit += this._axisLockVector * t;
                        }*/
                        #endregion
                        Ray r = Client.Instance.Frontend.Renderer.MapRenderer.RayFromCursor();
                        Vector3 planeNormal = this._moveMode == 0 ? ((-cam.Direction) * (Vector3.One - this._axisLockVector)).Normalized() : (Vector3.One - this._axisLockVector).Normalized();
                        Plane p = new Plane(planeNormal, 1);
                        Vector3? intersection = this._movementIntersectionValue = p.Intersect(r, this._rayInitialHit);
                        if (intersection.HasValue)
                        {
                            Vector3 iMajorAxis = intersection.Value * this._axisLockVector;
                            Vector3 delta = iMajorAxis - (this._rayInitialHit * this._axisLockVector);
                            foreach (MapObject mo in this.SelectedObjects)
                            {
                                if (scale)
                                {
                                    mo.Scale = mo.ClientDragMoveResetInitialPosition + delta;
                                    if (alt)
                                    {
                                        mo.Scale = MapRenderer.SnapToGrid(map.GridType, mo.Scale, map.GridSize);
                                    }
                                }
                                else
                                {
                                    float msx = MathF.Abs(mo.Scale.X) % (map.GridSize * 2);
                                    float msy = MathF.Abs(mo.Scale.Y) % (map.GridSize * 2);
                                    float msz = MathF.Abs(mo.Scale.Z) % (map.GridSize * 2);
                                    Vector3 bigScale = new Vector3(
                                        msx - 0.075f <= 0 || (map.GridSize * 2) - msx <= 0.075f ? 1 : 0,
                                        msy - 0.075f <= 0 || (map.GridSize * 2) - msy <= 0.075f ? 1 : 0,
                                        msz - 0.075f <= 0 || (map.GridSize * 2) - msz <= 0.075f ? 1 : 0
                                    );

                                    mo.Position = alt ? MapRenderer.SnapToGrid(map.GridType, mo.ClientDragMoveResetInitialPosition + delta, map.GridSize, bigScale) : mo.ClientDragMoveResetInitialPosition + delta;
                                }
                            }
                        }
                    }
                }
                else
                {
                    #region Old
                    /*
                    if (this._moveMode == 1)
                    {
                        Vector3 inverse = Vector3.One - this._axisLockVector;
                        float sign = MathF.Sign(this._rayInitialHit.X * inverse.X) + MathF.Sign(this._rayInitialHit.Y * inverse.Y) + MathF.Sign(this._rayInitialHit.Z * inverse.Z);
                        Plane p = new Plane(-sign * inverse, (this._rayInitialHit * inverse).Length);
                        Ray r = cam.RayFromCursor();
                        if (Game.Instance.MouseX - this._lastLmbX != 0 || Game.Instance.MouseY - this._lastLmbY != 0)
                        {
                            Vector3? v = null;
                            if (v.HasValue)
                            {
                                if (!alt)
                                {
                                    foreach (MapObject mo in this.SelectedObjects)
                                    {
                                        if (scale)
                                        {
                                            mo.Scale += (v.Value - this._rayInitialHit) * this._axisLockVector;
                                            mo.ClientDragMoveAccumulatedPosition = mo.Scale;
                                        }
                                        else
                                        {
                                            mo.Position += (v.Value - this._rayInitialHit) * this._axisLockVector;
                                            mo.ClientDragMoveAccumulatedPosition = mo.Position;
                                        }
                                    }
                                }
                                else
                                {
                                    foreach (MapObject mo in this.SelectedObjects)
                                    {
                                        if (scale)
                                        {
                                            mo.ClientDragMoveAccumulatedPosition += (v.Value - this._rayInitialHit) * this._axisLockVector;
                                            mo.Scale = MapRenderer.SnapToGrid(mo.ClientDragMoveAccumulatedPosition, Client.Instance.CurrentMap.GridSize);
                                        }
                                        else
                                        {
                                            mo.ClientDragMoveAccumulatedPosition += (v.Value - this._rayInitialHit) * this._axisLockVector;
                                            mo.Position = MapRenderer.SnapToGrid(mo.ClientDragMoveAccumulatedPosition, Client.Instance.CurrentMap.GridSize);
                                        }
                                    }
                                }

                                this._rayInitialHit = v.Value;
                            }
                        }
                    }
                    else
                    {
                        Ray r = cam.RayFromCursor();
                        Plane p = new Plane(-cam.Direction, this._rayInitialHit.Length);
                        Vector3? intersection = p.Intersect(r, this._rayInitialHit);
                        Console.WriteLine(intersection);

                        if (intersection.HasValue && (Game.Instance.MouseX - this._lastLmbX != 0 || Game.Instance.MouseY - this._lastLmbY != 0))
                        {
                            if (!alt)
                            {
                                foreach (MapObject mo in this.SelectedObjects)
                                {
                                    if (scale)
                                    {
                                        mo.Scale = mo.ClientDragMoveResetInitialPosition + intersection.Value - this._rayInitialHit;
                                        //mo.ClientDragMoveAccumulatedPosition = mo.Scale;
                                    }
                                    else
                                    {
                                        mo.Position = intersection.Value;
                                    }
                                }
                            }
                            else
                            {
                                foreach (MapObject mo in this.SelectedObjects)
                                {
                                    if (scale)
                                    {
                                        //mo.ClientDragMoveAccumulatedPosition += (v - this._rayInitialHit) * this._axisLockVector;
                                        //mo.Scale = MapRenderer.SnapToGrid(mo.ClientDragMoveAccumulatedPosition, Client.Instance.CurrentMap.GridSize);
                                    }
                                    else
                                    {
                                        //mo.ClientDragMoveAccumulatedPosition += (v - this._rayInitialHit) * this._axisLockVector;
                                        mo.Position = MapRenderer.SnapToGrid(intersection.Value, Client.Instance.CurrentMap.GridSize);
                                    }
                                }
                            }

                            // this._rayInitialHit = v;
                        }
                    }*/
                    #endregion
                    Ray r = Client.Instance.Frontend.Renderer.MapRenderer.RayFromCursor();
                    Plane p = new Plane(-cam.Direction, this._rayInitialHit.Length());
                    Vector3? intersection = this._movementIntersectionValue = p.Intersect(r, this._rayInitialHit);

                    if (intersection.HasValue && (Client.Instance.Frontend.MouseX - this._lastLmbX != 0 || Client.Instance.Frontend.MouseY - this._lastLmbY != 0))
                    {
                        foreach (MapObject mo in this.SelectedObjects)
                        {
                            if (scale)
                            {
                                float lI = (half - this._rayInitialHit).Length();
                                float lC = (half - intersection.Value).Length();
                                mo.Scale = mo.ClientDragMoveResetInitialPosition * (lC / lI);
                            }
                            else
                            {
                                mo.Position = alt ? MapRenderer.SnapToGrid(map.GridType, intersection.Value, map.GridSize) : intersection.Value;
                            }
                        }
                    }
                }
            }
        }

        public void ProcessSelection()
        {
            bool add = Client.Instance.Frontend.GameHandle.IsAnyShiftDown();
            bool remove = Client.Instance.Frontend.GameHandle.IsAnyControlDown();
            if (!this._isBoxSelect) // Single click
            {
                MapObject mouseOver = Client.Instance.Frontend.Renderer.ObjectRenderer.ObjectMouseOver;
                if (mouseOver == null)
                {
                    this.SelectedObjects.Clear();
                }
                else
                {
                    if (remove && this.SelectedObjects.Contains(mouseOver))
                    {
                        this.SelectedObjects.Remove(mouseOver);
                        return;
                    }

                    if (add && !this.SelectedObjects.Contains(mouseOver) && (Client.Instance.IsAdmin || mouseOver.CanEdit(Client.Instance.ID)))
                    {
                        this.SelectedObjects.Add(mouseOver);
                        return;
                    }

                    this.SelectedObjects.Clear();
                    if (Client.Instance.IsAdmin || mouseOver.CanEdit(Client.Instance.ID))
                    {
                        this.SelectedObjects.Add(mouseOver);
                    }
                }
            }
            else
            {
                this.BoxSelectCandidates.Clear();
                int minX = Math.Min(this._initialLmbX, (int)Client.Instance.Frontend.MouseX);
                int maxX = Math.Max(this._initialLmbX, (int)Client.Instance.Frontend.MouseX);
                int minY = Math.Min(this._initialLmbY, (int)Client.Instance.Frontend.MouseY);
                int maxY = Math.Max(this._initialLmbY, (int)Client.Instance.Frontend.MouseY);
                RectangleF rect = new RectangleF(minX, minY, maxX - minX, maxY - minY);
                Map map = Client.Instance.CurrentMap;
                if (map != null)
                {
                    foreach (MapObject mo in map.IterateObjects(Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer))
                    {
                        if (!mo.ClientRenderedThisFrame)
                        {
                            continue;
                        }

                        BBBox box = mo.ClientRaycastOOBB;
                        Vector3 moPos = mo.Position;
                        if (TriRectTester.Intersects(in rect, in box, in moPos, Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera))
                        {
                            this.BoxSelectCandidates.Add(mo);
                        }
                    }
                }

                if (!remove && !add)
                {
                    this.SelectedObjects.Clear();
                }

                foreach (MapObject mo in this.BoxSelectCandidates)
                {
                    if (remove)
                    {
                        if (this.SelectedObjects.Contains(mo))
                        {
                            this.SelectedObjects.Remove(mo);
                        }

                        continue;
                    }

                    if (add)
                    {
                        if (!this.SelectedObjects.Contains(mo) && (Client.Instance.IsAdmin || mo.CanEdit(Client.Instance.ID)))
                        {
                            this.SelectedObjects.Add(mo);
                        }

                        continue;
                    }

                    if (Client.Instance.IsAdmin || mo.CanEdit(Client.Instance.ID))
                    {
                        this.SelectedObjects.Add(mo);
                    }
                }

                this.BoxSelectCandidates.Clear();
            }
        }

        private bool _rmbLastFrame;
        public void Render(Map m, double delta)
        {
            if (m == null)
            {
                return;
            }

            bool rmb = Client.Instance.Frontend.GameHandle.IsMouseButtonDown(MouseButton.Right);
            if (!this._rmbLastFrame && rmb)
            {
                if (Client.Instance.Frontend.Renderer?.ObjectRenderer?.EditMode == EditMode.Translate && Client.Instance.Frontend.Renderer?.ObjectRenderer?.MovementMode == TranslationMode.Path && this.IsDraggingObjects)
                {
                    if (this.SelectedObjects.Count > 0)
                    {
                        MapObject mo = this.SelectedObjects[0];
                        this.ObjectMovementPath.Add(mo.Position);
                    }
                }

                this._rmbLastFrame = true;
            }

            if (!rmb && this._rmbLastFrame)
            {
                this._rmbLastFrame = false;
            }
        }

        public void RenderGui(double delta)
        {
            if (this._isBoxSelect && this._lmbDown)
            {
                int minX = Math.Min(this._initialLmbX, (int)Client.Instance.Frontend.MouseX);
                int maxX = Math.Max(this._initialLmbX, (int)Client.Instance.Frontend.MouseX);
                int minY = Math.Min(this._initialLmbY, (int)Client.Instance.Frontend.MouseY);
                int maxY = Math.Max(this._initialLmbY, (int)Client.Instance.Frontend.MouseY);
                ImGuiWindowFlags window_flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove;
                ImGui.SetNextWindowBgAlpha(0.2f);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
                ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(0, 0));
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0, 0));
                ImGui.PushStyleColor(ImGuiCol.Border, ((Vector4)Color.RoyalBlue));
                ImGui.SetNextWindowPos(new Vector2(minX, minY));
                ImGui.SetNextWindowSize(new Vector2(maxX - minX, maxY - minY));
                ImGui.SetNextWindowSizeConstraints(Vector2.Zero, new Vector2(float.PositiveInfinity, float.PositiveInfinity));
                ImGui.Begin("SelectBox", window_flags);
                ImGui.End();
                ImGui.PopStyleVar();
                ImGui.PopStyleVar();
                ImGui.PopStyleVar();
                ImGui.PopStyleVar();
                ImGui.PopStyleVar();
                ImGui.PopStyleColor();
            }
        }

        private enum MovementGizmoControlMode
        {
            SingleAxis = 0,
            Plane = 1,
            CameraViewPlane = 2,
            Blocking = 4
        }
    }
}
