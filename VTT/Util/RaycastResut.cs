﻿namespace VTT.Util
{
    using OpenTK.Mathematics;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using VTT.Asset.Glb;
    using VTT.Control;
    using VTT.Network;

    public unsafe class RaycastResut
    {
        public Vector3 Hit { get; set; }
        public bool Result { get; set; }
        public MapObject ObjectHit { get; set; }
        public MapObject[] AllObjectsHit { get; set; }
        public Vector3[] AllHit { get; set; }
        public int HitIndex { get; set; }

        public static implicit operator bool(RaycastResut self) => self.Result;

        public static RaycastResut Raycast(Ray r, Map env, Predicate<MapObject> selector = null)
        {
            IEnumerable<MapObject> mEnv()
            {
                lock (env.Lock)
                {
                    foreach (MapObject mo in env.Objects)
                    {
                        yield return mo;
                    }
                }

                yield break;
            };

            return Raycast(r, mEnv(), selector);
        }

        private static readonly List<(MapObject, Asset.Asset)> hitsBroad = new List<(MapObject, Asset.Asset)>();
        private static readonly ConcurrentBag<(MapObject, Vector3)> hitResults = new ConcurrentBag<(MapObject, Vector3)>();
        private static readonly MatrixStack ms = new MatrixStack() { Reversed = true };
        private static readonly List<Vector3> hitPoints = new List<Vector3>();

        public static unsafe RaycastResut Raycast(Ray r, IEnumerable<MapObject> env, Predicate<MapObject> selector = null)
        {
            selector ??= m => true;
            hitsBroad.Clear();
            foreach (MapObject mo in env) // Broad phase
            {
                if (selector(mo))
                {
                    BBBox box = new BBBox(mo.ClientRaycastBox.Start, mo.ClientRaycastBox.End, mo.Rotation);
                    box = box.Scale(mo.Scale); // TODO fix BBBox rotation!

                    Vector3? vec = box.Intersects(r, mo.Position);
                    if (vec.HasValue)
                    {
                        Guid aID = mo.AssetID;
                        if (!Guid.Empty.Equals(aID) && Client.Instance.AssetManager.Assets.ContainsKey(aID))
                        {
                            hitsBroad.Add((mo, Client.Instance.AssetManager.Assets[aID]));
                        }
                    }
                }
            }

            hitResults.Clear();
            Vector4 vOri4 = new Vector4(r.Origin, 1.0f);
            Vector4 vDir4 = new Vector4(r.Direction, 1.0f);
            int nMaxVertsForMultithreading = Client.Instance.Settings.RaycastMultithreading switch
            {
                ClientSettings.RaycastMultithreadingType.Always => 0,
                ClientSettings.RaycastMultithreadingType.Eager => 24576,
                ClientSettings.RaycastMultithreadingType.Cautious => ushort.MaxValue * 3,
                ClientSettings.RaycastMultithreadingType.Never => int.MaxValue,
                _ => 0,
            };

            foreach ((MapObject, Asset.Asset) e in hitsBroad)
            {
                MapObject mo = e.Item1;
                Asset.Asset a = e.Item2;
                if (a.Model != null && a.Model.GLMdl != null)
                {
                    GlbScene s = a.Model.GLMdl;
                    Matrix4 oMat = mo.ClientCachedModelMatrix;
                    ms.Push(oMat);
                    GlbObjectType sType = s.SimplifiedRaycastMesh != null ? GlbObjectType.RaycastMesh : GlbObjectType.Mesh;
                    foreach (GlbMesh mesh in s.RootObjects.SelectMany(o => IterateGlbModel(ms, o, sType)))
                    {
                        Matrix4 omat = ms.Current;
                        Matrix4 mat = ms.Current.Inverted();
                        System.Numerics.Vector3 nOri = (vOri4 * mat).Xyz.SystemVector();
                        System.Numerics.Vector3 nDir = (vDir4 * mat.ClearTranslation()).Xyz.SystemVector();
                        nDir = System.Numerics.Vector3.Normalize(nDir);
                        int l = mesh.simplifiedTriangles.Length;
                        fixed (System.Numerics.Vector3* ptr = &mesh.simplifiedTriangles[0])
                        {
                            if (l < nMaxVertsForMultithreading)
                            {
                                for (int i = 0; i < l; i += 3)
                                {
                                    IterateTriangles(ptr, omat, nOri, nDir, i, hitPoints);
                                }
                            }
                            else
                            {
                                try
                                {
                                    iterStoredFixedPtr = ptr;
                                    iterStoredMat = omat;
                                    iterStoredRayOrigin = nOri;
                                    iterStoredRayDirection = nDir;
                                    Parallel.For(0, mesh.simplifiedTriangles.Length / 3, ParallelIterationDelegate);
                                }
                                finally
                                {
                                    iterStoredFixedPtr = null; // Remove ptr ref in case
                                }
                            }
                        }
                    }

                    ms.Pop();
                    if (hitPoints.Count > 0)
                    {
                        Vector3 actualHit = default;
                        float cD = float.MaxValue;
                        foreach (Vector3 hit in hitPoints)
                        {
                            float hD = (r.Origin - hit).Length;
                            if (hD < cD)
                            {
                                cD = hD;
                                actualHit = hit;
                            }
                        }

                        hitResults.Add((e.Item1, actualHit));
                    }

                    hitPoints.Clear();
                }
            }

            if (!hitResults.IsEmpty)
            {
                Vector3 actualHit = default;
                MapObject objectHit = null;
                int indexHit = 0;
                float cD = float.MaxValue;
                Vector3[] hits = new Vector3[hitResults.Count];
                MapObject[] objectsHit = new MapObject[hitResults.Count];
                int i = 0;
                foreach ((MapObject, Vector3) hit in hitResults)
                {
                    float hD = (r.Origin - hit.Item2).Length;
                    if (hD < cD)
                    {
                        cD = hD;
                        objectHit = hit.Item1;
                        actualHit = hit.Item2;
                        indexHit = i;
                    }

                    hits[i] = hit.Item2;
                    objectsHit[i] = hit.Item1;
                    ++i;
                }

                return new RaycastResut() { AllHit = hits, AllObjectsHit = objectsHit, Hit = actualHit, HitIndex = indexHit, ObjectHit = objectHit, Result = true };
            }

            return new RaycastResut() { AllHit = Array.Empty<Vector3>(), AllObjectsHit = Array.Empty<MapObject>(), Hit = default, ObjectHit = null, HitIndex = 0, Result = false };
        }

        private static System.Numerics.Vector3* iterStoredFixedPtr;
        private static Matrix4 iterStoredMat;
        private static System.Numerics.Vector3 iterStoredRayOrigin;
        private static System.Numerics.Vector3 iterStoredRayDirection;
        private static unsafe void ParallelIterationDelegate(int i) => IterateTriangles(iterStoredFixedPtr, iterStoredMat, iterStoredRayOrigin, iterStoredRayDirection, i * 3, hitPoints);

        private static Vector3 TransformFull(Vector3 vec, Matrix4 mat)
        {
            return new Vector3(
                (vec.X * mat.Row0.X) + (vec.Y * mat.Row1.X) + (vec.Z * mat.Row2.X) + (1 * mat.Row3.X),
                (vec.X * mat.Row0.Y) + (vec.Y * mat.Row1.Y) + (vec.Z * mat.Row2.Y) + (1 * mat.Row3.Y),
                (vec.X * mat.Row0.Z) + (vec.Y * mat.Row1.Z) + (vec.Z * mat.Row2.Z) + (1 * mat.Row3.Z));
        }

        public static unsafe void IterateTriangles(System.Numerics.Vector3* arrayPtr, Matrix4 mat, System.Numerics.Vector3 rOrigin, System.Numerics.Vector3 rDirection, int index, List<Vector3> hitPoints)
        {
            System.Numerics.Vector3 t0 = arrayPtr[index + 0];
            System.Numerics.Vector3 t1 = arrayPtr[index + 1];
            System.Numerics.Vector3 t2 = arrayPtr[index + 2];

            System.Numerics.Vector3 t10 = t1 - t0;
            System.Numerics.Vector3 t20 = t2 - t0;

            System.Numerics.Vector3 tNormal = System.Numerics.Vector3.Cross(t10, t20);
            float d = System.Numerics.Vector3.Dot(tNormal, t0);
            float nd = System.Numerics.Vector3.Dot(tNormal, rDirection);
            if (MathF.Abs(nd) > float.Epsilon)
            {
                System.Numerics.Vector3 hit = rOrigin + (rDirection * ((d - System.Numerics.Vector3.Dot(tNormal, rOrigin)) / nd));

                System.Numerics.Vector3 v0 = t20;
                System.Numerics.Vector3 v1 = t10;
                System.Numerics.Vector3 v2 = hit - t0;

                float dot00 = System.Numerics.Vector3.Dot(v0, v0);
                float dot01 = System.Numerics.Vector3.Dot(v0, v1);
                float dot02 = System.Numerics.Vector3.Dot(v0, v2);
                float dot11 = System.Numerics.Vector3.Dot(v1, v1);
                float dot12 = System.Numerics.Vector3.Dot(v1, v2);

                float invDenom = 1 / ((dot00 * dot11) - (dot01 * dot01));
                float u = ((dot11 * dot02) - (dot01 * dot12)) * invDenom;
                float v = ((dot00 * dot12) - (dot01 * dot02)) * invDenom;
                if ((u >= 0) && (v >= 0) && (u + v < 1))
                {
                    Vector4 hit4 = new Vector4(hit.X, hit.Y, hit.Z, 1.0f) * mat;
                    hitPoints.Add(hit4.Xyz / hit4.W);
                }
            }
        }

        private static IEnumerable<GlbMesh> IterateGlbModel(MatrixStack stack, GlbObject o, GlbObjectType typeSeeked)
        {
            stack.Push(o.CachedMatrix);
            if (o.Type == typeSeeked)
            {
                foreach (GlbMesh m in o.Meshes)
                {
                    yield return m;
                }
            }

            foreach (GlbObject c in o.Children)
            {
                foreach (GlbMesh m in IterateGlbModel(stack, c, typeSeeked))
                {
                    yield return m;
                }
            }

            stack.Pop();

            yield break;
        }
    }
}
