namespace VTT.Asset.Glb
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using VTT.Util;

    public class BoundingVolumeHierarchy
    {
        public UnsafeResizeableArray<int> TriangleIndices { get; private set; }
        public Node RootNode { get; set; }

        internal UnsafeResizeableArray<Vector3> _trianglesRef;
        internal UnsafeResizeableArray<Vector3> _centroids;

        public class Node
        {
            public AABox Bounds { get; set; }

            public Node ChildA { get; set; }
            public Node ChildB { get; set; }

            public int TriangleSliceStart { get; set; }
            public int TriangleSliceLength { get; set; }

            public bool IsLeafNode => this.TriangleSliceLength > 0;

            public void CalculateBounds(BoundingVolumeHierarchy bvh)
            {
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                for (int first = this.TriangleSliceStart, i = 0; i < this.TriangleSliceLength; ++i)
                {
                    int leafTriIdx = bvh.TriangleIndices[first + i] * 3;
                    Vector3 a = bvh._trianglesRef[leafTriIdx];
                    Vector3 b = bvh._trianglesRef[leafTriIdx + 1];
                    Vector3 c = bvh._trianglesRef[leafTriIdx + 2];
                    min = Vector3.Min(min, a);
                    min = Vector3.Min(min, b);
                    min = Vector3.Min(min, c);
                    max = Vector3.Max(max, a);
                    max = Vector3.Max(max, b);
                    max = Vector3.Max(max, c);
                }

                this.Bounds = new AABox(min, max);
            }

            public void RecursivelySubdivide(BoundingVolumeHierarchy bvh)
            {
                // Could in theory use a faster *(((float*)&vec) + idx) operation but behaviour may be undefined for
                // custom impls of System.Numerics such as Mono
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static float GetVectorElement(Vector3 vec, int idx) => idx switch
                {
                    0 => vec.X,
                    1 => vec.Y,
                    2 => vec.Z,
                    _ => vec.X
                };

                if (this.TriangleSliceLength <= 2)
                {
                    return;
                }

                Vector3 extent = this.Bounds.End - this.Bounds.Start;
                int axis = 0;
                if (extent.Y > extent.X)
                {
                    axis = 1;
                }

                if (extent.Z > (axis == 0 ? extent.X : extent.Y))
                {
                    axis = 2;
                }

                float splitPos = GetVectorElement(this.Bounds.Start, axis) + GetVectorElement(extent, axis) * 0.5f;
                int i = this.TriangleSliceStart;
                int j = i + this.TriangleSliceLength - 1;
                while (i <= j)
                {
                    if (GetVectorElement(bvh._centroids[bvh.TriangleIndices[i]], axis) < splitPos)
                    {
                        i++;
                    }
                    else
                    {
                        (bvh.TriangleIndices[i], bvh.TriangleIndices[j]) = (bvh.TriangleIndices[j], bvh.TriangleIndices[i]);
                        j--;
                    }
                }

                int leftCount = i - this.TriangleSliceStart;
                if (leftCount == 0 || leftCount == this.TriangleSliceLength)
                {
                    return;
                }

                this.ChildA = new Node();
                this.ChildB = new Node();
                this.ChildA.TriangleSliceStart = this.TriangleSliceStart;
                this.ChildA.TriangleSliceLength = leftCount;
                this.ChildB.TriangleSliceStart = i;
                this.ChildB.TriangleSliceLength = this.TriangleSliceLength - leftCount;
                this.TriangleSliceLength = 0;
                this.ChildA.CalculateBounds(bvh);
                this.ChildB.CalculateBounds(bvh);

                this.ChildA.RecursivelySubdivide(bvh);
                this.ChildB.RecursivelySubdivide(bvh);
            }

            private static bool TestAABBRayIntersectFast(Ray r, AABox box)
            {
                Vector3 invRayDir = Vector3.One / r.Direction;
                float tx1 = (box.Start.X - r.Origin.X) * invRayDir.X, tx2 = (box.End.X - r.Origin.X) * invRayDir.X;
                float tmin = MathF.Min(tx1, tx2), tmax = MathF.Max(tx1, tx2);
                float ty1 = (box.Start.Y - r.Origin.Y) * invRayDir.Y, ty2 = (box.End.Y - r.Origin.Y) * invRayDir.Y;
                tmin = MathF.Max(tmin, MathF.Min(ty1, ty2));
                tmax = MathF.Min(tmax, MathF.Max(ty1, ty2));
                float tz1 = (box.Start.Z - r.Origin.Z) * invRayDir.Z, tz2 = (box.End.Z - r.Origin.Z) * invRayDir.Z;
                tmin = MathF.Max(tmin, MathF.Min(tz1, tz2));
                tmax = MathF.Min(tmax, MathF.Max(tz1, tz2));
                return tmax >= tmin && tmax > 0;
            }

            public unsafe void Intersect(BoundingVolumeHierarchy bvh, ref TriangleIterationState tis, Vector3* arrayPtr)
            {
                Ray r = tis.r;
                Vector3? intersectionResult = this.Bounds.Intersects(r);
                if (intersectionResult.HasValue)
                {
                    float dist = (r.Origin - intersectionResult.Value).Length();
                    if (dist <= tis.smallestDistance)
                    {
                        if (this.IsLeafNode)
                        {
                            for (int i = 0; i < this.TriangleSliceLength; ++i)
                            {
                                int index = bvh.TriangleIndices[this.TriangleSliceStart + i] * 3;
                                Vector3 hitVec = new Vector3();
                                if (RaycastResut.IterateTriangles(arrayPtr, tis.mat, r.Origin, r.Direction, index, tis.hitPoints, ref hitVec))
                                {
                                    dist = (r.Origin - hitVec).Length();
                                    tis.smallestDistance = MathF.Min(dist, tis.smallestDistance);
                                }
                            }
                        }
                        else
                        {
                            this.ChildA.Intersect(bvh, ref tis, arrayPtr);
                            this.ChildB.Intersect(bvh, ref tis, arrayPtr);
                        }
                    }
                }
            }
        }

        public void Build(UnsafeResizeableArray<Vector3> triangles)
        {
            const float oneThird = 1f / 3f;
            UnsafeResizeableArray<Vector3> centroids = new UnsafeResizeableArray<Vector3>(triangles.Length / 3);
            this.TriangleIndices = new UnsafeResizeableArray<int>(triangles.Length / 3);
            int j = 0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = triangles[i];
                Vector3 b = triangles[i + 1];
                Vector3 c = triangles[i + 2];
                Vector3 center = (a + b + c) * oneThird;
                centroids.Add(center);
                this.TriangleIndices.Add(j++);
            }

            this._trianglesRef = triangles;
            this._centroids = centroids;

            Node root = new Node() { TriangleSliceStart = 0, TriangleSliceLength = triangles.Length / 3 };
            root.CalculateBounds(this);
            root.RecursivelySubdivide(this);
            this.RootNode = root;

            centroids.Free();
            this._centroids = null;
            this._trianglesRef = null;
        }

        // Called by RaycastResult.Raycast 
        // arrayPtr is our unsafe triangle array - the same passed into the build method
        // for performance reasons instead of a ray struct ray origin and direction are passed around on the stack
        // hit points is our result list reference
        public unsafe void IntersectTriangles(Vector3* arrayPtr, Matrix4x4 mat, Vector3 rOrigin, Vector3 rDirection, List<Vector3> hitPoints)
        {
            TriangleIterationState state = new TriangleIterationState(new Ray(rOrigin, rDirection), mat, hitPoints, float.MaxValue);
            this.RootNode.Intersect(this, ref state, arrayPtr);
        }

        public void Free()
        {
            this.TriangleIndices.Free();
        }

        public struct TriangleIterationState
        {
            public Ray r;
            public Matrix4x4 mat;
            public List<Vector3> hitPoints;
            public float smallestDistance;

            public TriangleIterationState(Ray r, Matrix4x4 mat, List<Vector3> hitPoints, float smallestDistance)
            {
                this.r = r;
                this.mat = mat;
                this.hitPoints = hitPoints;
                this.smallestDistance = smallestDistance;
            }
        }
    }
}
