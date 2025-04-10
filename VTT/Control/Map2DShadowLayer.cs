namespace VTT.Control
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Render;
    using VTT.Util;

    public class Map2DShadowLayer : ISerializable
    {
        private readonly Dictionary<Guid, Shadow2DBox> _boxes = new Dictionary<Guid, Shadow2DBox>();
        private readonly List<Shadow2DBox> _allBoxes = new List<Shadow2DBox>();
        private bool _needsBVHChange;
        private readonly object _lock = new object();
        public Shadow2DBVH BVH { get; set; } = new Shadow2DBVH();

        public void CloneFrom(Map2DShadowLayer other)
        {
            lock (this._lock)
            {
                lock (other._lock)
                {
                    this._boxes.Clear();
                    this._allBoxes.Clear();
                    foreach (KeyValuePair<Guid, Shadow2DBox> kv in other._boxes)
                    {
                        this._allBoxes.Add(this._boxes[kv.Key] = kv.Value.FullClone(false));
                    }

                    this._needsBVHChange = true;
                }
            }
        }

        public IEnumerable<Shadow2DBox> EnumerateBoxes()
        {
            lock (this._lock)
            {
                foreach (Shadow2DBox box in this._allBoxes)
                {
                    yield return box;
                }

                yield break;
            }
        }

        public void RemoveBox(Guid id, bool notifyChange = true)
        {
            lock (this._lock)
            {
                if (this._boxes.TryGetValue(id, out Shadow2DBox box))
                {
                    this._boxes.Remove(id);
                    this._allBoxes.Remove(box);
                    this._needsBVHChange = notifyChange;
                }
            }
        }

        public bool TryGetBox(Guid id, out Shadow2DBox box)
        {
            lock (this._lock)
            {
                return this._boxes.TryGetValue(id, out box);
            }
        }

        public void NotifyOfAnyChange()
        {
            lock (this._lock)
            {
                this._needsBVHChange = true;
            }
        }

        public void AddBox(Shadow2DBox box, bool notifyChange = true)
        {
            lock (this._lock)
            {
                if (this._boxes.TryAdd(box.BoxID, box))
                {
                    this._allBoxes.Add(box);
                    this._needsBVHChange = notifyChange && box.BoxType != Shadow2DBox.ShadowBoxType.Sunlight;
                }
            }
        }

        public void Free()
        {
            lock (this._lock)
            {
                this.BVH.Free();
            }
        }

        public void Update(Map m)
        {
            lock (this._lock)
            {
                if (this._needsBVHChange)
                {
                    this._needsBVHChange = false;
                    this.BVH.Free();
                    this.BVH.Build(this._allBoxes);
                }
            }
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            lock (this._lock)
            {
                ret.SetArray("boxes", this._allBoxes.ToArray(), (n, c, v) => c.SetMap(n, v.Serialize()));
            }

            return ret;
        }

        public void Deserialize(DataElement e)
        {
            lock (this._lock)
            {
                Shadow2DBox[] bx = e.GetArray("boxes", (n, c) =>
                {
                    Shadow2DBox box = new Shadow2DBox();
                    box.Deserialize(c.GetMap(n));
                    return box;
                }, Array.Empty<Shadow2DBox>());

                this._allBoxes.Clear();
                this._boxes.Clear();
                foreach (Shadow2DBox box in bx)
                {
                    this._boxes[box.BoxID] = box;
                    this._allBoxes.Add(box);
                }

                this._needsBVHChange = true;
            }
        }
    }

    public class Shadow2DBVH
    {
        [StructLayout(LayoutKind.Explicit, Pack = 0, Size = 16)]
        public readonly struct ShadowAABB2D
        {
            [FieldOffset(0)]
            public readonly Vector2 start;

            [FieldOffset(8)]
            public readonly Vector2 end;

            public readonly Vector2 Center => this.start + ((this.end - this.start) * 0.5f);
            public readonly float SurfaceArea
            {
                get
                {
                    Vector2 size = this.end - this.start;
                    return size.X * size.Y;
                }
            }

            public ShadowAABB2D(Vector2 start, Vector2 end)
            {
                this.start = start;
                this.end = end;
            }
        }

        [StructLayout(LayoutKind.Explicit, Pack = 0, Size = 32)]
        public readonly struct ShadowOBB2D
        {
            [FieldOffset(0)]
            public readonly Vector2 start;

            [FieldOffset(8)]
            public readonly Vector2 end;

            [FieldOffset(16)]
            public readonly float rotationRadians;

            [FieldOffset(20)]
            public readonly Vector2 rotatedExtent;

            [FieldOffset(28)]
            public readonly float padding;

            public readonly Vector2 Center => this.start + ((this.end - this.start) * 0.5f);
            public readonly Vector2 RotatedMin => this.Center - (this.rotatedExtent / 2);
            public readonly Vector2 RotatedMax => this.Center + (this.rotatedExtent / 2);
            public readonly Vector2 BVHMin => this.IsRotated ? this.RotatedMin : this.start;
            public readonly Vector2 BVHMax => this.IsRotated ? this.RotatedMax : this.end;
            public readonly bool IsRotated => this.rotationRadians != 0;
            public readonly float SurfaceArea
            {
                get
                {
                    Vector2 size = this.end - this.start;
                    return size.X * size.Y;
                }
            }

            public ShadowOBB2D(Vector2 start, Vector2 end, float rotationRadians)
            {
                this.start = start;
                this.end = end;
                this.rotationRadians = rotationRadians;

                Vector2 center = start + ((end - start) * 0.5f);
                Vector2 a = start - center;
                Vector2 b = end - center;
                Vector2 c = new Vector2(end.X, start.Y) - center;
                Vector2 d = new Vector2(start.X, end.Y) - center;
                a = RotateVector(a, rotationRadians);
                b = RotateVector(b, rotationRadians);
                c = RotateVector(c, rotationRadians);
                d = RotateVector(d, rotationRadians);
                Vector2 min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
                Vector2 max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
                this.rotatedExtent = Vector2.Abs(max - min);
                this.padding = 0;
            }

            private static Vector2 RotateVector(Vector2 vec, float rad)
            {
                float cos = MathF.Cos(rad);
                float sin = MathF.Sin(rad);
                return new Vector2(
                    (vec.X * cos) - (vec.Y * sin),
                    (vec.X * sin) + (vec.Y * cos)
                );
            }
        }

        [StructLayout(LayoutKind.Explicit, Pack = 0, Size = 32)]
        public struct Node // 32 bytes per struct
        {
            [FieldOffset(0)]
            public ShadowAABB2D bounds; // 16

            [FieldOffset(16)]
            public int leftFirst; // 4

            [FieldOffset(20)]
            public int primitiveCount; // 4

            [FieldOffset(24)]
            public int paddingA; // 4

            [FieldOffset(28)]
            public int paddingB; // 4

            public readonly bool IsLeaf => this.primitiveCount > 0;
        }

        public bool WasUploaded { get; private set; } = true;
        public bool HasAnyBoxes => (this.primitives?.Length ?? 0) > 0;
        private UnsafeArray<ShadowOBB2D> primitives;
        private UnsafeArray<Node> nodes;

        private unsafe void UpdateNodeBounds(Node* node)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < node->primitiveCount; ++i)
            {
                ShadowOBB2D bound = this.primitives[node->leftFirst + i];
                min = Vector2.Min(min, bound.BVHMin);
                max = Vector2.Max(max, bound.BVHMax);
            }

            node->bounds = new ShadowAABB2D(min, max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe float GetComponent(Vector2 vec, int i) => i == 0 ? vec.X : vec.Y;

        private unsafe float CalculateNodeCost(Node* n) => n->primitiveCount * n->bounds.SurfaceArea;

        private unsafe float FindBestSplitPlane(Node* n, ref int axis, ref float splitPos)
        {
            float bestCost = float.MaxValue;
            for (int a = 0; a < 2; ++a)
            {
                float boundsMin = GetComponent(n->bounds.start, a);
                float boundsMax = GetComponent(n->bounds.end, a);
                if (boundsMin == boundsMax)
                {
                    continue;
                }

                float scale = (boundsMax - boundsMin) / 8;
                for (int k = 1; k < 8; ++k)
                {
                    float candidatePos = boundsMin + (k * scale);
                    float cost = this.EvaluateSAH(n, a, candidatePos);
                    if (cost < bestCost)
                    {
                        splitPos = candidatePos;
                        axis = a;
                        bestCost = cost;
                    }
                }
            }

            return bestCost;
        }

        private unsafe void Subdivide(Node* node, ref int nodesUsed)
        {
            float splitPosition = 0;
            int axis = 0;

            float splitCost = this.FindBestSplitPlane(node, ref axis, ref splitPosition);
            if (splitCost >= this.CalculateNodeCost(node))
            {
                return;
            }

            int i = node->leftFirst;
            int j = i + node->primitiveCount - 1;
            while (i < j)
            {
                if (GetComponent(this.primitives[i].Center, axis) < splitPosition)
                {
                    ++i;
                }
                else
                {
                    (this.primitives[j], this.primitives[i]) = (this.primitives[i], this.primitives[j]);
                    --j;
                }
            }

            int leftCount = i - node->leftFirst;
            if (leftCount == 0 || leftCount == node->primitiveCount)
            {
                return;
            }

            int leftChildIndex = nodesUsed++;
            int rightChildIndex = nodesUsed++;
            Node* lc = this.nodes.GetPointer(leftChildIndex);
            Node* rc = this.nodes.GetPointer(rightChildIndex);
            lc->leftFirst = node->leftFirst;
            lc->primitiveCount = leftCount;
            rc->leftFirst = i;
            rc->primitiveCount = node->primitiveCount - leftCount;
            node->leftFirst = leftChildIndex;
            node->primitiveCount = 0;
            this.UpdateNodeBounds(lc);
            this.UpdateNodeBounds(rc);
            this.Subdivide(lc, ref nodesUsed);
            this.Subdivide(rc, ref nodesUsed);
        }

        private unsafe float EvaluateSAH(Node* node, int axis, float candidatePos)
        {
            Vector2 minLeft = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 maxLeft = new Vector2(float.MinValue, float.MinValue);
            Vector2 minRight = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 maxRight = new Vector2(float.MinValue, float.MinValue);

            int leftCount = 0;
            int rightCount = 0;
            for (int i = 0; i < node->primitiveCount; ++i)
            {
                ShadowOBB2D box = this.primitives[node->leftFirst + i];
                if (GetComponent(box.Center, axis) < candidatePos)
                {
                    ++leftCount;
                    minLeft = Vector2.Min(minLeft, box.BVHMin);
                    maxLeft = Vector2.Max(maxLeft, box.BVHMax);
                }
                else
                {
                    ++rightCount;
                    minRight = Vector2.Min(minRight, box.BVHMin);
                    maxRight = Vector2.Max(maxRight, box.BVHMax);
                }
            }

            ShadowAABB2D leftBox = new ShadowAABB2D(minLeft, maxLeft);
            ShadowAABB2D rightBox = new ShadowAABB2D(minRight, maxRight);
            float cost = (leftCount * leftBox.SurfaceArea) + (rightCount * rightBox.SurfaceArea);
            return cost > 0 ? cost : float.MaxValue;
        }

        public unsafe void Build(List<Shadow2DBox> boxes)
        {
            if (boxes == null || boxes.Count == 0)
            {
                this.WasUploaded = false;
                return;
            }

            int inactiveOrSuns = boxes.Count(x => x.IsActive && x.BoxType == Shadow2DBox.ShadowBoxType.Blocker);
            this.primitives = new UnsafeArray<ShadowOBB2D>(inactiveOrSuns);
            for (int i = 0, j = 0; i < boxes.Count; ++i)
            {
                Shadow2DBox box = boxes[i];
                if (box.IsActive && box.BoxType == Shadow2DBox.ShadowBoxType.Blocker)
                {
                    this.primitives[j++] = new ShadowOBB2D(box.Start, box.End, box.Rotation);
                }
            }

            int nAmt = (primitives.Length * 2) - 1;
            UnsafeArray<Node> nodes = new UnsafeArray<Node>(nAmt);
            this.nodes = nodes;
            for (int i = 0; i < nAmt; ++i)
            {
                nodes[i] = new Node();
            }

            int rootIndex = 0;
            int nodesUsed = 1;

            Node* root = nodes.GetPointer(rootIndex);
            root->leftFirst = 0;
            root->primitiveCount = this.primitives.Length;
            this.UpdateNodeBounds(root);
            this.Subdivide(root, ref nodesUsed);

            UnsafeArray<Node> actuallyUsedNodes = new UnsafeArray<Node>(sizeof(Node) * nodesUsed);
            Buffer.MemoryCopy(nodes.GetPointer(), actuallyUsedNodes.GetPointer(), sizeof(Node) * nodesUsed, sizeof(Node) * nodesUsed);
            this.nodes = actuallyUsedNodes;
            nodes.Free();
            this.WasUploaded = false;
        }

        public unsafe void Upload(Shadow2DRenderer renderer)
        {
            if (renderer.BoxesBufferTexture != null)
            {
                renderer.BoxesDataBuffer.Dispose();
                renderer.BoxesBufferTexture?.Dispose();
                renderer.BVHNodesDataBuffer.Dispose();
                renderer.BVHNodesBufferTexture?.Dispose();
            }

            renderer.BoxesDataBuffer = new GPUBuffer(BufferTarget.Texture);
            renderer.BVHNodesDataBuffer = new GPUBuffer(BufferTarget.Texture);
            renderer.BoxesBufferTexture = new Texture(TextureTarget.Buffer);
            renderer.BVHNodesBufferTexture = new Texture(TextureTarget.Buffer);

            if (this.HasAnyBoxes)
            {
                renderer.BoxesDataBuffer.Bind();
                renderer.BoxesDataBuffer.SetData((IntPtr)this.primitives.GetPointer(), this.primitives.Length * sizeof(ShadowOBB2D));
                renderer.BVHNodesDataBuffer.Bind();
                renderer.BVHNodesDataBuffer.SetData((IntPtr)this.nodes.GetPointer(), this.nodes.Length * sizeof(Node));
            }
            else
            {
                renderer.BoxesDataBuffer.Bind();
                renderer.BoxesDataBuffer.SetData(IntPtr.Zero, 1);
                renderer.BVHNodesDataBuffer.Bind();
                renderer.BVHNodesDataBuffer.SetData(IntPtr.Zero, 1);
            }

            renderer.BoxesBufferTexture.Bind();
            GL.TexBuffer(SizedInternalFormat.RgbaFloat, renderer.BoxesDataBuffer);
            renderer.BVHNodesBufferTexture.Bind();
            GL.TexBuffer(SizedInternalFormat.RgbaFloat, renderer.BVHNodesDataBuffer);

            this.WasUploaded = true;
        }

        public void Free()
        {
            this.primitives?.Free();
            this.nodes?.Free();
            this.primitives = null;
            this.nodes = null;
        }
    }

    public class Shadow2DBox : ISerializable
    {
        public Guid BoxID { get; set; }
        public Vector2 Start { get; set; }
        public Vector2 End { get; set; }
        public float Rotation { get; set; }
        public bool IsActive { get; set; }
        public bool IsMouseOver { get; set; }
        public ShadowBoxType BoxType { get; set; }

        public Vector2 Center => this.Start + ((this.End - this.Start) * 0.5f);
        public float Area
        {
            get
            {
                Vector2 extent = this.End - this.Start;
                return MathF.Abs(extent.X) * MathF.Abs(extent.Y);
            }
        }

        public bool Contains(Vector2 point)
        {
            Vector2 center = this.Center;
            Vector2 arel = point - center;
            Vector2 np = arel.Rotate(this.Rotation + MathF.PI) + center;
            return np.X >= this.Start.X && np.Y >= this.Start.Y && np.X <= this.End.X && np.Y <= this.End.Y;
        }

        public void Deserialize(DataElement e)
        {
            this.BoxID = e.GetGuid("id");
            this.Start = e.GetVec2("s");
            this.End = e.GetVec2("e");
            this.Rotation = e.GetSingle("r");
            this.IsActive = e.GetBool("a");
            this.BoxType = e.GetEnum<ShadowBoxType>("t");
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("id", this.BoxID);
            ret.SetVec2("s", this.Start);
            ret.SetVec2("e", this.End);
            ret.SetSingle("r", this.Rotation);
            ret.SetBool("a", this.IsActive);
            ret.SetEnum("t", this.BoxType);
            return ret;
        }

        public Shadow2DBox FullClone(bool cloneID = true)
        {
            return new Shadow2DBox()
            {
                BoxID = cloneID ? this.BoxID : Guid.NewGuid(),
                Start = this.Start,
                End = this.End,
                Rotation = this.Rotation,
                IsActive = this.IsActive,
                BoxType = this.BoxType,
            };
        }

        public enum ShadowBoxType
        {
            Blocker,
            Sunlight
        }
    }
}
