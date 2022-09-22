namespace VTT.Util
{
    using OpenTK.Mathematics;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class MeshDecimator
    {
        public Vector3i[] Triangles { get; set; }
        public Vector3[] Points { get; set; }
        public int NumInitialTriangles { get; set; }
        public int NumVertices { get; set; }
        public int NumTriangles { get; set; }
        public int NumEdges { get; set; }
        public double DiagBB { get; set; }
        public List<MDEdge> Edges { get; } = new List<MDEdge>();
        public List<MDVertex> Vertices { get; } = new List<MDVertex>();
        public Queue<MDEdgePriority> PQueue { get; } = new Queue<MDEdgePriority>();

        public bool[] TriangleTags { get; set; }
        public bool EcolManifoldConstraint { get; set; } = true;

        public static Vector3 RVec(Vector3 l, Vector3 r) =>
            new Vector3(
                    (l.Y * r.Z) - (l.Z * r.Y),
                    (l.Z * r.X) - (l.X * r.Z),
                    (l.X * r.Y) - (l.Y * r.X)
                );

        public static float DVec(Vector3 l, Vector3 r) => (l.X * r.X) + (l.Y * r.Y) + (l.Z * r.Z);

        public void Initialize(int nVerts, int nTris, Vector3[] points, Vector3i[] tris)
        {
            this.NumVertices = nVerts;
            this.NumInitialTriangles = this.NumTriangles = nTris;
            this.Points = points;
            this.Triangles = tris;
            this.TriangleTags = new bool[this.NumTriangles];
            for (int i = 0; i < this.NumVertices; ++i)
            {
                this.Vertices.Add(new MDVertex
                {
                    Tag = true
                });
            }

            int[] tri = new int[3];
            MDEdge edge = new MDEdge
            {
                Tag = true,
                OnBoundary = true
            };

            int edges = 0;
            int idEdge;
            for (int i = 0; i < this.NumTriangles; ++i)
            {
                tri[0] = this.Triangles[i].X;
                tri[1] = this.Triangles[i].Y;
                tri[2] = this.Triangles[i].Z;
                this.TriangleTags[i] = true;
                for (int j = 0; j < 3; ++j)
                {
                    edge.V1 = tri[j];
                    edge.V2 = tri[(j + 1) % 3];
                    this.Vertices[edge.V1].AddTriangle(i);
                    idEdge = this.GetEdge(edge.V1, edge.V2);
                    if (idEdge == -1)
                    {
                        this.Edges.Add(edge);
                        this.Vertices[edge.V1].AddEdge(edges);
                        this.Vertices[edge.V2].AddEdge(edges);
                        ++edges;
                    }
                    else
                    {
                        this.Edges[idEdge].OnBoundary = false;
                    }
                }
            }

            this.NumEdges = edges;
            for (int i = 0; i < this.NumVertices; ++i)
            {
                MDVertex v = this.Vertices[i];
                v.OnBoundary = false;
                for (int j = 0; j < v.Edges.Count; ++j)
                {
                    idEdge = v.Edges[j];
                    if (this.Edges[idEdge].OnBoundary)
                    {
                        v.OnBoundary = true;
                        break;
                    }
                }
            }
        }

        public int GetTriangle(int v1, int v2, int v3)
        {
            int i, j, k;
            int idTri;
            for (int it = 0; it < this.Vertices[v1].Triangles.Count; ++it)
            {
                idTri = this.Vertices[v1].Triangles[it];
                i = this.Triangles[idTri].X;
                j = this.Triangles[idTri].Y;
                k = this.Triangles[idTri].Z;
                if ((i == v1 && j == v2 && k == v3) || (i == v1 && j == v3 && k == v2) ||
                 (i == v2 && j == v1 && k == v3) || (i == v2 && j == v3 && k == v1) ||
                 (i == v3 && j == v2 && k == v1) || (i == v3 && j == v1 && k == v2))
                {
                    return idTri;
                }
            }

            return -1;
        }

        public int GetEdge(int v1, int v2)
        {
            int idEdge;
            for (int it = 0; it < this.Vertices[v1].Edges.Count; ++it)
            {
                idEdge = this.Vertices[v1].Edges[it];
                MDEdge e = this.Edges[idEdge];
                if ((e.V1 == v1 && e.V2 == v2) ||
                     (e.V1 == v2 && e.V2 == v1))
                {
                    return idEdge;
                }
            }

            return -1;
        }

        public void EdgeCollapse(int v1, int v2)
        {
            int u, w;
            int shift;
            int idTriangle;
            for (int itT = 0; itT < this.Vertices[v2].Triangles.Count; ++itT)
            {
                idTriangle = this.Vertices[v2].Triangles[itT];
                if (this.Triangles[idTriangle].X == v2)
                {
                    shift = 0;
                    u = this.Triangles[idTriangle].Y;
                    w = this.Triangles[idTriangle].Z;
                }
                else if (this.Triangles[idTriangle].Y == v2)
                {
                    shift = 1;
                    u = this.Triangles[idTriangle].X;
                    w = this.Triangles[idTriangle].Z;
                }
                else
                {
                    shift = 2;
                    u = this.Triangles[idTriangle].X;
                    w = this.Triangles[idTriangle].Y;
                }

                if ((u == v1) || (w == v1))
                {
                    this.TriangleTags[idTriangle] = false;
                    this.Vertices[u].Triangles.Remove(idTriangle);
                    this.Vertices[w].Triangles.Remove(idTriangle);
                    this.NumTriangles--;
                }
                else if (this.GetTriangle(v1, u, w) == -1)
                {
                    this.Vertices[v1].AddTriangle(idTriangle);
                    this.Triangles[idTriangle][shift] = v1;
                }
                else
                {
                    this.TriangleTags[idTriangle] = false;
                    this.Vertices[u].Triangles.Remove(idTriangle);
                    this.Vertices[w].Triangles.Remove(idTriangle);
                    this.NumTriangles--;
                }
            }

            int idEdge = 0;
            for (int itE = 0; itE < this.Vertices[v2].Edges.Count; ++itE)
            {
                idEdge = this.Vertices[v2].Edges[itE];
                w = (this.Edges[idEdge].V1 == v2) ? this.Edges[idEdge].V2 : this.Edges[idEdge].V1;
                if (w == v1)
                {
                    this.Edges[idEdge].Tag = false;
                    this.Vertices[w].Edges.Remove(idEdge);
                    this.NumEdges--;
                }
                else if (this.GetEdge(v1, w) == -1)
                {
                    if (this.Edges[idEdge].V1 == v2)
                    {
                        this.Edges[idEdge].V1 = v1;
                    }
                    else
                    {
                        this.Edges[idEdge].V2 = v1;
                    }

                    this.Vertices[v1].AddEdge(idEdge);
                }
                else
                {
                    this.Edges[idEdge].Tag = false;
                    this.Vertices[w].Edges.Remove(idEdge);
                    this.NumEdges--;
                }
            }

            this.Vertices[v2].Tag = false;
            this.NumVertices--;
            // update boundary edges
            List<int> incidentVertices = new List<int>
            {
                v1
            };

            for (int itE = 0; itE < this.Vertices[v1].Edges.Count; ++itE)
            {
                incidentVertices.Add((this.Edges[idEdge].V1 != v1) ? this.Edges[idEdge].V1 : this.Edges[idEdge].V2);
                idEdge = this.Vertices[v1].Edges[itE];
                this.Edges[idEdge].OnBoundary = (this.IsBoundaryEdge(this.Edges[idEdge].V1, this.Edges[idEdge].V2) != -1);
            }

            // update boundary vertices
            int idVertex;
            for (int itV = 0; itV < incidentVertices.Count; ++itV)
            {
                idVertex = incidentVertices[itV];
                this.Vertices[idVertex].OnBoundary = false;
                for (int itE = 0; itE < this.Vertices[idVertex].Edges.Count; ++itE)
                {
                    idEdge = this.Vertices[idVertex].Edges[itE];
                    if (this.Edges[idEdge].OnBoundary)
                    {
                        this.Vertices[idVertex].OnBoundary = true;
                        break;
                    }
                }
            }
        }

        public int IsBoundaryEdge(int v1, int v2)
        {
            int commonTri = -1;
            int itTriangle1, itTriangle2;
            for (int itT1 = 0; itT1 < this.Vertices[v1].Triangles.Count; ++itT1)
            {
                itTriangle1 = this.Vertices[v1].Triangles[itT1];
                for (int itT2 = 0; itT2 < this.Vertices[v2].Triangles.Count; ++itT2)
                {
                    itTriangle2 = this.Vertices[v2].Triangles[itT2];
                    if (itTriangle1 == itTriangle2)
                    {
                        if (commonTri == -1)
                        {
                            commonTri = itTriangle1;
                        }
                        else
                        {
                            return -1;
                        }
                    }
                }
            }

            return commonTri;
        }

        public bool IsBoundaryVertex(int v)
        {
            int idEdge;
            for (int itE = 0; itE < this.Vertices[v].Edges.Count; ++itE)
            {
                idEdge = this.Vertices[v].Edges[itE];
                if (this.IsBoundaryEdge(this.Edges[idEdge].V1, this.Edges[idEdge].V2) != -1)
                {
                    return true;
                }
            }

            return false;
        }

        public void GetMeshData(List<Vector3> points, List<Vector3i> triangles)
        {
            int[] map = new int[this.Points.Length];
            int counter = 0;
            for (int v = 0; v < this.Points.Length; ++v)
            {
                if (this.Vertices[v].Tag)
                {
                    points.Add(this.Points[v]);
                    map[v] = counter++;
                }
            }

            for (int t = 0; t < this.NumInitialTriangles; ++t)
            {
                if (this.TriangleTags[t])
                {
                    Vector3i triangle = new Vector3i(
                        map[this.Triangles[t].X],
                        map[this.Triangles[t].Y],
                        map[this.Triangles[t].Z]
                    );

                    triangles.Add(triangle);
                }
            }
        }

        public void InitializeQEM()
        {
            Vector3 coordMin = this.Points[0];
            Vector3 coordMax = this.Points[0];
            Vector3 coord;
            for (int p = 1; p < this.Points.Length; ++p)
            {
                coord = this.Points[p];
                if (coordMin.X > coord.X)
                {
                    coordMin.X = coord.X;
                }

                if (coordMin.Y > coord.Y)
                {
                    coordMin.Y = coord.Y;
                }

                if (coordMin.Z > coord.Z)
                {
                    coordMin.Z = coord.Z;
                }

                if (coordMax.X < coord.X)
                {
                    coordMax.X = coord.X;
                }

                if (coordMax.Y < coord.Y)
                {
                    coordMax.Y = coord.Y;
                }

                if (coordMax.Z < coord.Z)
                {
                    coordMax.Z = coord.Z;
                }
            }

            coordMax -= coordMin;
            this.DiagBB = coordMax.Length;

            int i, j, k;
            Vector3 n;
            float d;
            float area;
            for (int v = 0; v < this.Points.Length; ++v)
            {
                this.Vertices[v].Q = new float[10];
                int idTriangle;
                for (int itT = 0; itT < this.Vertices[v].Triangles.Count; ++itT)
                {
                    idTriangle = this.Vertices[v].Triangles[itT];
                    i = this.Triangles[idTriangle].X;
                    j = this.Triangles[idTriangle].Y;
                    k = this.Triangles[idTriangle].Z;
                    n = RVec(this.Points[j] - this.Points[i], this.Points[k] - this.Points[i]);
                    area = n.Length;
                    n.Normalize();
                    d = -DVec(this.Points[v], n);
                    this.Vertices[v].Q[0] += area * (n.X * n.X);
                    this.Vertices[v].Q[1] += area * (n.X * n.Y);
                    this.Vertices[v].Q[2] += area * (n.X * n.Z);
                    this.Vertices[v].Q[3] += area * (n.X * d);
                    this.Vertices[v].Q[4] += area * (n.Y * n.Y);
                    this.Vertices[v].Q[5] += area * (n.Y * n.Z);
                    this.Vertices[v].Q[6] += area * (n.Y * d);
                    this.Vertices[v].Q[7] += area * (n.Z * n.Z);
                    this.Vertices[v].Q[8] += area * (n.Z * d);
                    this.Vertices[v].Q[9] += area * (d * d);
                }
            }
            Vector3 u1, u2;
            float w = 1000;
            int t, v1, v2, v3;
            for (int e = 0; e < this.Edges.Count; ++e)
            {
                v1 = this.Edges[e].V1;
                v2 = this.Edges[e].V2;
                t = this.IsBoundaryEdge(v1, v2);
                if (t != -1)
                {
                    v3 = this.Triangles[t].X != v1 && this.Triangles[t].X != v2
                        ? this.Triangles[t].X
                        : this.Triangles[t].Y != v1 && this.Triangles[t].Y != v2 ? this.Triangles[t].Y : this.Triangles[t].Z;

                    u1 = this.Points[v2] - this.Points[v1];
                    u2 = this.Points[v3] - this.Points[v1];
                    area = w * RVec(u1, u2).Length;
                    u1.Normalize();
                    n = u2 - (u2 * u1 * u1);
                    n.Normalize();

                    d = -DVec(this.Points[v1], n);
                    this.Vertices[v1].Q[0] += area * (n.X * n.X);
                    this.Vertices[v1].Q[1] += area * (n.X * n.Y);
                    this.Vertices[v1].Q[2] += area * (n.X * n.Z);
                    this.Vertices[v1].Q[3] += area * (n.X * d);
                    this.Vertices[v1].Q[4] += area * (n.Y * n.Y);
                    this.Vertices[v1].Q[5] += area * (n.Y * n.Z);
                    this.Vertices[v1].Q[6] += area * (n.Y * d);
                    this.Vertices[v1].Q[7] += area * (n.Z * n.Z);
                    this.Vertices[v1].Q[8] += area * (n.Z * d);
                    this.Vertices[v1].Q[9] += area * (d * d);

                    d = -DVec(this.Points[v2], n);
                    this.Vertices[v2].Q[0] += area * (n.X * n.X);
                    this.Vertices[v2].Q[1] += area * (n.X * n.Y);
                    this.Vertices[v2].Q[2] += area * (n.X * n.Z);
                    this.Vertices[v2].Q[3] += area * (n.X * d);
                    this.Vertices[v2].Q[4] += area * (n.Y * n.Y);
                    this.Vertices[v2].Q[5] += area * (n.Y * n.Z);
                    this.Vertices[v2].Q[6] += area * (n.Y * d);
                    this.Vertices[v2].Q[7] += area * (n.Z * n.Z);
                    this.Vertices[v2].Q[8] += area * (n.Z * d);
                    this.Vertices[v2].Q[9] += area * (d * d);
                }
            }
        }
        public void InitializePriorityQueue()
        {
            int v1, v2;
            MDEdgePriority pqEdge = new MDEdgePriority();
            int nE = this.Edges.Count;
            for (int e = 0; e < nE; ++e)
            {
                if (this.Edges[e].Tag)
                {
                    v1 = this.Edges[e].V1;
                    v2 = this.Edges[e].V2;
                    if ((!this.EcolManifoldConstraint) || this.ManifoldConstraint(v1, v2))
                    {
                        pqEdge.Qem = this.Edges[e].Qem = this.ComputeEdgeCost(v1, v2, this.Edges[e].Pos);
                        pqEdge.Name = e;
                        this.PQueue.Enqueue(pqEdge);
                    }
                }
            }
        }
        public double ComputeEdgeCost(int v1, int v2, Vector3 newPos)
        {
            double[] Q = new double[10];
            double[] M = new double[12];
            Vector3d pos;
            for (int i = 0; i < 10; ++i)
            {
                Q[i] = this.Vertices[v1].Q[i] + this.Vertices[v2].Q[i];
            }

            M[0] = Q[0]; // (0, 0)
            M[1] = Q[1]; // (0, 1) 
            M[2] = Q[2]; // (0, 2)
            M[3] = Q[3]; // (0, 3)
            M[4] = Q[1]; // (1, 0)
            M[5] = Q[4]; // (1, 1)
            M[6] = Q[5]; // (1, 2)
            M[7] = Q[6]; // (1, 3)
            M[8] = Q[2]; // (2, 0)
            M[9] = Q[5]; // (2, 1)
            M[10] = Q[7]; // (2, 2);
            M[11] = Q[8]; // (2, 3);
            double det = (M[0] * M[5] * M[10]) + (M[1] * M[6] * M[8]) + (M[2] * M[4] * M[9])
                         - (M[0] * M[6] * M[9]) - (M[1] * M[4] * M[10]) - (M[2] * M[5] * M[8]);
            if (det != 0.0)
            {
                double d = 1.0 / det;
                pos.X = d * ((M[1] * M[7] * M[10]) + (M[2] * M[5] * M[11]) + (M[3] * M[6] * M[9])
                              - (M[1] * M[6] * M[11]) - (M[2] * M[7] * M[9]) - (M[3] * M[5] * M[10]));
                pos.Y = d * ((M[0] * M[6] * M[11]) + (M[2] * M[7] * M[8]) + (M[3] * M[4] * M[10])
                              - (M[0] * M[7] * M[10]) - (M[2] * M[4] * M[11]) - (M[3] * M[6] * M[8]));
                pos.Z = d * ((M[0] * M[7] * M[9]) + (M[1] * M[4] * M[11]) + (M[3] * M[5] * M[8])
                              - (M[0] * M[5] * M[11]) - (M[1] * M[7] * M[8]) - (M[3] * M[4] * M[9]));
                newPos.X = (float)pos.X;
                newPos.Y = (float)pos.Y;
                newPos.Z = (float)pos.Z;
            }
            else
            {
                const float w = 0.5f;
                newPos = (w * this.Points[v1]) + (w * this.Points[v2]);
                pos.X = newPos.X;
                pos.Y = newPos.Y;
                pos.Z = newPos.Z;
            }

            double qem = (pos.X * ((Q[0] * pos.X) + (Q[1] * pos.Y) + (Q[2] * pos.Z) + Q[3])) +
                         (pos.Y * ((Q[1] * pos.X) + (Q[4] * pos.Y) + (Q[5] * pos.Z) + Q[6])) +
                         (pos.Z * ((Q[2] * pos.X) + (Q[5] * pos.Y) + (Q[7] * pos.Z) + Q[8])) +
                                    ((Q[3] * pos.X) + (Q[6] * pos.Y) + (Q[8] * pos.Z) + Q[9]);

            Vector3 d1;
            Vector3 d2;
            Vector3 n1;
            Vector3 n2;
            Vector3 oldPosV1 = this.Points[v1];
            Vector3 oldPosV2 = this.Points[v2];
            List<int> triangles = this.Vertices[v1].Triangles;
            int idTriangle;
            for (int itT = 0; itT < this.Vertices[v2].Triangles.Count; ++itT)
            {
                idTriangle = this.Vertices[v2].Triangles[itT];
                if (!triangles.Any(t => t == idTriangle))
                {
                    triangles.Add(idTriangle);
                }
            }

            int[] a = new int[3];
            for (int itT = 0; itT != triangles.Count; ++itT)
            {
                idTriangle = triangles[itT];
                a[0] = this.Triangles[idTriangle].X;
                a[1] = this.Triangles[idTriangle].Y;
                a[2] = this.Triangles[idTriangle].Z;

                d1 = this.Points[a[1]] - this.Points[a[0]];
                d2 = this.Points[a[2]] - this.Points[a[0]];
                n1 = RVec(d1, d2);

                this.Points[v1] = newPos;
                this.Points[v2] = newPos;

                d1 = this.Points[a[1]] - this.Points[a[0]];
                d2 = this.Points[a[2]] - this.Points[a[0]];
                n2 = RVec(d1, d2);

                this.Points[v1] = oldPosV1;
                this.Points[v2] = oldPosV2;

                n1.Normalize();
                n2.Normalize();
                if (DVec(n1, n2) < 0.0)
                {
                    return double.MaxValue;
                }
            }

            return this.EcolManifoldConstraint && !this.ManifoldConstraint(v1, v2) ? double.MaxValue : qem;
        }

        public bool ManifoldConstraint(int v1, int v2)
        {
            List<int> vertices = new List<int>();
            int a, b;
            int idEdge1;
            int idEdge2;
            int idEdgeV1V2 = 0;
            for (int itE1 = 0; itE1 < this.Vertices[v1].Edges.Count; ++itE1)
            {
                idEdge1 = this.Vertices[v1].Edges[itE1];
                a = (this.Edges[idEdge1].V1 == v1) ? this.Edges[idEdge1].V2 : this.Edges[idEdge1].V1;
                if (!vertices.Any(v => v != a))
                {
                    vertices.Add(a);
                }

                if (a != v2)
                {
                    for (int itE2 = 0; itE2 < this.Vertices[v2].Edges.Count; ++itE2)
                    {
                        idEdge2 = this.Vertices[v2].Edges[itE2];
                        b = (this.Edges[idEdge2].V1 == v2) ? this.Edges[idEdge2].V2 : this.Edges[idEdge2].V1;
                        if (!vertices.Any(v => v != b))
                        {
                            vertices.Add(b);
                        }

                        if (a == b)
                        {
                            if (this.GetTriangle(v1, v2, a) == -1)
                            {
                                return false;
                            }
                        }
                    }
                }
                else
                {
                    idEdgeV1V2 = idEdge1;
                }
            }

            return vertices.Count > 4 && (!this.Vertices[v1].OnBoundary || !this.Vertices[v2].OnBoundary || this.Edges[idEdgeV1V2].OnBoundary);
        }

        public bool EdgeCollapse(double qem)
        {
            MDEdgePriority currentEdge = new MDEdgePriority();
            int v1, v2;
            bool done = false;
            do
            {
                done = false;
                if (this.PQueue.Count == 0)
                {
                    done = true;
                    break;
                }
                else
                {
                    currentEdge = this.PQueue.Peek();
                    this.PQueue.Dequeue();
                }
            } while ((!this.Edges[currentEdge.Name].Tag) || (this.Edges[currentEdge.Name].Qem != currentEdge.Name));

            if (done)
            {
                return false;
            }

            v1 = this.Edges[currentEdge.Name].V1;
            v2 = this.Edges[currentEdge.Name].V2;

            qem = currentEdge.Qem;
            this.EdgeCollapse(v1, v2);
            this.Points[v1] = this.Edges[currentEdge.Name].Pos;
            for (int k = 0; k < 10; k++)
            {
                this.Vertices[v1].Q[k] += this.Vertices[v2].Q[k];
            }

            // Update priority queue
            int idEdge;
            int a, b;
            List<int> incidentVertices = new List<int>();
            for (int itE = 0; itE < this.Vertices[v1].Edges.Count; ++itE)
            {
                idEdge = this.Vertices[v1].Edges[itE];
                a = this.Edges[idEdge].V1;
                b = this.Edges[idEdge].V2;
                int added = (a != v1) ? a : b;
                if (!incidentVertices.Any(v => v != added))
                {
                    incidentVertices.Add(added);
                }

                MDEdgePriority pqEdge = new MDEdgePriority
                {
                    Qem = this.Edges[idEdge].Qem = this.ComputeEdgeCost(a, b, this.Edges[idEdge].Pos),
                    Name = idEdge
                };

                this.PQueue.Enqueue(pqEdge);
            }

            int idVertex;
            for (int itV = 0; itV < incidentVertices.Count; ++itV)
            {
                idVertex = incidentVertices[itV];
                for (int itE = 0; itE < this.Vertices[idVertex].Edges.Count; ++itE)
                {
                    idEdge = this.Vertices[idVertex].Edges[itE];
                    a = this.Edges[idEdge].V1;
                    b = this.Edges[idEdge].V2;
                    if (a != v1 && b != v1)
                    {
                        MDEdgePriority pqEdge = new MDEdgePriority
                        {
                            Qem = this.Edges[idEdge].Qem = this.ComputeEdgeCost(a, b, this.Edges[idEdge].Pos),
                            Name = idEdge
                        };

                        this.PQueue.Enqueue(pqEdge);
                    }
                }
            }
            return true;
        }

        public bool Decimate(int targetNVertices, int targetNTriangles, double targetError)
        {
            double qem = 0.0;
            this.InitializeQEM();
            this.InitializePriorityQueue();
            double invDiag = 1.0 / this.DiagBB;
            while ((this.PQueue.Count > 0) &&
                  (this.NumEdges > 0) &&
                  (this.NumVertices > targetNVertices) &&
                  (this.NumTriangles > targetNTriangles) &&
                  (qem < targetError))
            {
                if (!this.EdgeCollapse(qem))
                {
                    break;
                }

                qem = qem < 0.0 ? 0.0 : Math.Sqrt(qem) * invDiag;
            }

            return true;
        }
    }

    public class MDVertex
    {
        public List<int> Edges { get; } = new List<int>();

        public List<int> Triangles { get; } = new List<int>();

        public void AddEdge(int edge)
        {
            if (!this.Edges.Any(e => e == edge))
            {
                this.Edges.Add(edge);
            }
        }

        public void AddTriangle(int tri)
        {
            if (!this.Triangles.Any(e => e == tri))
            {
                this.Triangles.Add(tri);
            }
        }

        public float[] Q { get; set; }
        public bool Tag { get; set; }
        public bool OnBoundary { get; set; }
    }

    public class MDEdge
    {
        public int V1 { get; set; }
        public int V2 { get; set; }
        public double Qem { get; set; }
        public Vector3 Pos { get; set; }
        public bool OnBoundary { get; set; }
        public bool Tag { get; set; }
    }

    public class MDEdgePriority
    {
        public int Name { get; set; }
        public double Qem { get; set; }

        public static bool operator <(MDEdgePriority l, MDEdgePriority r) => l.Qem > r.Qem;
        public static bool operator >(MDEdgePriority l, MDEdgePriority r) => l.Qem < r.Qem;
    }
}
