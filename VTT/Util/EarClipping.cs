namespace VTT.Util
{
    using OpenTK.Mathematics;
    using System.Collections.Generic;

    // https://www.habrador.com/tutorials/math/10-triangulation/
    public static class EarClipping
    {
        public static void Triangulate(IEnumerable<Vector2> polygon, bool cw, out List<Vector2> vertices, out List<uint> indices)
        {
            vertices = new List<Vector2>();
            indices = new List<uint>();
            List<TriangulationVertex> polygonVertices = new List<TriangulationVertex>();

            TriangulationVertex first = null;
            TriangulationVertex last = null;
            foreach (Vector2 vertex in polygon)
            {
                TriangulationVertex vert = new() { IsConvex = false, IsReflex = false, Pos = vertex };
                if (first == null)
                {
                    first = vert;
                }
                else
                {
                    vert.Prev = last;
                    last.Next = vert;
                }

                last = vert;
                polygonVertices.Add(vert);
            }

            last.Next = first;
            first.Prev = last;

            List<Triangle> tris = new List<Triangle>();
            foreach (TriangulationVertex tv in polygonVertices)
            {
                FlagConvexOrReflex(tv);
            }

            List<TriangulationVertex> ears = new List<TriangulationVertex>();
            foreach (TriangulationVertex tv in polygonVertices)
            {
                FlagEar(tv, polygonVertices, ears);
            }

            while (true)
            {
                if (polygonVertices.Count == 3)
                {
                    //The final triangle
                    tris.Add(new Triangle(polygonVertices[0].Pos, polygonVertices[0].Prev.Pos, polygonVertices[0].Next.Pos));
                    break;
                }

                //Make a triangle of the first ear
                TriangulationVertex earVertex = ears[0];

                TriangulationVertex earVertexPrev = earVertex.Prev;
                TriangulationVertex earVertexNext = earVertex.Next;

                Triangle newTriangle = new Triangle(earVertex.Pos, earVertexPrev.Pos, earVertexNext.Pos);
                tris.Add(newTriangle);
                ears.Remove(earVertex);

                polygonVertices.Remove(earVertex);

                //Update the previous vertex and next vertex
                earVertexPrev.Next = earVertexNext;
                earVertexNext.Prev = earVertexPrev;

                //...see if we have found a new ear by investigating the two vertices that was part of the ear
                FlagConvexOrReflex(earVertexPrev);
                FlagConvexOrReflex(earVertexNext);

                ears.Remove(earVertexPrev);
                ears.Remove(earVertexNext);

                FlagEar(earVertexPrev, polygonVertices, ears);
                FlagEar(earVertexNext, polygonVertices, ears);
            }

            for (int i = 0; i < tris.Count; ++i)
            {
                Triangle tri = tris[i];
                uint idx1 = (uint)FindIndex(tri.Pos1, vertices);
                uint idx2 = (uint)FindIndex(tri.Pos2, vertices);
                uint idx3 = (uint)FindIndex(tri.Pos3, vertices);
                if (cw)
                {
                    indices.Add(idx1);
                    indices.Add(idx2);
                    indices.Add(idx3);
                }
                else
                {
                    indices.Add(idx3);
                    indices.Add(idx2);
                    indices.Add(idx1);
                }
            }
        }

        private static int FindIndex(Vector2 pos, List<Vector2> verts)
        {
            for (int i = 0; i < verts.Count; i++)
            {
                Vector2 vec = verts[i];
                if ((pos - vec).Length <= float.Epsilon * 2)
                {
                    return i;
                }
            }

            verts.Add(pos);
            return verts.Count - 1;
        }

        private static void FlagConvexOrReflex(TriangulationVertex vert)
        {
            vert.IsReflex = false;
            vert.IsConvex = false;

            Vector2 a = vert.Prev.Pos;
            Vector2 b = vert.Pos;
            Vector2 c = vert.Next.Pos;
            if (IsTriangleOrientedClockwise(a, b, c))
            {
                vert.IsReflex = true;
            }
            else
            {
                vert.IsConvex = true;
            }
        }

        // https://www.habrador.com/tutorials/math/9-useful-algorithms/
        private static bool IsTriangleOrientedClockwise(Vector2 p1, Vector2 p2, Vector2 p3) => ((p1.X * p2.Y) + (p3.X * p1.Y) + (p2.X * p3.Y) - (p1.X * p3.Y) - (p3.X * p2.Y) - (p2.X * p1.Y)) <= 0f;
        private static bool IsPointInTriangle(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p)
        {
            float denominator = ((p2.Y - p3.Y) * (p1.X - p3.X)) + ((p3.X - p2.X) * (p1.Y - p3.Y));
            float a = (((p2.Y - p3.Y) * (p.X - p3.X)) + ((p3.X - p2.X) * (p.Y - p3.Y))) / denominator;
            float b = (((p3.Y - p1.Y) * (p.X - p3.X)) + ((p1.X - p3.X) * (p.Y - p3.Y))) / denominator;
            float c = 1 - a - b;
            return a > 0f && a < 1f && b > 0f && b < 1f && c > 0f && c < 1f;
        }


        private static void FlagEar(TriangulationVertex vertex, List<TriangulationVertex> allVerts, List<TriangulationVertex> ears)
        {
            if (vertex.IsReflex)
            {
                return;
            }

            Vector2 a = vertex.Prev.Pos;
            Vector2 b = vertex.Pos;
            Vector2 c = vertex.Next.Pos;
            bool hasPointInside = false;
            for (int i = 0; i < allVerts.Count; i++)
            {
                if (allVerts[i].IsReflex)
                {
                    Vector2 p = allVerts[i].Pos;
                    if (IsPointInTriangle(a, b, c, p))
                    {
                        hasPointInside = true;
                        break;
                    }
                }
            }

            if (!hasPointInside)
            {
                ears.Add(vertex);
            }
        }

        public struct Triangle
        {
            public Vector2 Pos1 { get; set; }
            public Vector2 Pos2 { get; set; }
            public Vector2 Pos3 { get; set; }

            public Triangle(Vector2 pos1, Vector2 pos2, Vector2 pos3)
            {
                this.Pos1 = pos1;
                this.Pos2 = pos2;
                this.Pos3 = pos3;
            }
        }
    }

    public class TriangulationVertex
    {
        public Vector2 Pos { get; set; }

        public TriangulationVertex Prev { get; set; }
        public TriangulationVertex Next { get; set; }

        public bool IsReflex { get; set; }
        public bool IsConvex { get; set; }
    }
}
