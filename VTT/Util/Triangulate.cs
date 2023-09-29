namespace VTT.Util
{
    using OpenTK.Mathematics;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // https://github.com/SebLague/Ear-Clipping-Triangulation
    public static class Triangulate
    {
        private class Polygon
        {
            public readonly Vector2[] points;

            public Polygon(Vector2[] points)
            {
                this.points = new Vector2[points.Length];


                // add hull points, ensuring they wind in counterclockwise order
                bool reverseHullPointsOrder = !PointsAreCounterClockwise(points);
                for (int i = 0; i < points.Length; i++)
                {
                    this.points[i] = points[reverseHullPointsOrder ? points.Length - 1 - i : i];
                }
            }

            private bool PointsAreCounterClockwise(Vector2[] testPoints)
            {
                float signedArea = 0;
                for (int i = 0; i < testPoints.Length; i++)
                {
                    int nextIndex = (i + 1) % testPoints.Length;
                    signedArea += (testPoints[nextIndex].X - testPoints[i].X) * (testPoints[nextIndex].Y + testPoints[i].Y);
                }

                return signedArea < 0;
            }
        }

        private class PolyVertex
        {
            public readonly Vector2 position;
            public readonly int index;
            public bool isConvex;

            public PolyVertex(Vector2 position, int index, bool isConvex)
            {
                this.position = position;
                this.index = index;
                this.isConvex = isConvex;
            }
        }

        private static int SideOfLine(Vector2 a, Vector2 b, Vector2 c) => MathF.Sign(((c.X - a.X) * (-b.Y + a.Y)) + ((c.Y - a.Y) * (b.X - a.X)));
        private static bool IsConvex(Vector2 v0, Vector2 v1, Vector2 v2) => SideOfLine(v0, v2, v1) == -1;
        private static bool PointInTriangle(Vector2 a, Vector2 b, Vector2 c, Vector2 p)
        {
            float area = 0.5f * ((-b.Y * c.X) + (a.Y * (-b.X + c.X)) + (a.X * (b.Y - c.Y)) + (b.X * c.Y));
            float s = 1 / (2 * area) * ((a.Y * c.X) - (a.X * c.Y) + ((c.Y - a.Y) * p.X) + ((a.X - c.X) * p.Y));
            float t = 1 / (2 * area) * ((a.X * b.Y) - (a.Y * b.X) + ((a.Y - b.Y) * p.X) + ((b.X - a.X) * p.Y));
            return s >= 0 && t >= 0 && (s + t) <= 1;

        }

        public static void Process(IEnumerable<Vector2> vIn, List<Vector2> vOut, out bool s)
        {
            Polygon poly = new Polygon(vIn.ToArray());
            int[] tris = GenTriangles(poly);
            if (tris != null)
            {
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector2 a = poly.points[tris[i + 0]];
                    Vector2 b = poly.points[tris[i + 1]];
                    Vector2 c = poly.points[tris[i + 2]];
                    vOut.Add(a);
                    vOut.Add(b);
                    vOut.Add(c);
                }

                s = true;
            }
            else
            {
                // Panic
                List<Vector2> ins = vIn.ToList();
                bool b = false;
                for (int i = 0; i < ins.Count - 2; ++i)
                {
                    Vector2 c = ins[i];
                    Vector2 n = ins[i + 1];
                    Vector2 p = ins[i + 2];
                    if (!b)
                    {
                        vOut.Add(c);
                        vOut.Add(n);
                        vOut.Add(p);
                        b = true;
                    }
                    else
                    {
                        vOut.Add(n);
                        vOut.Add(c);
                        vOut.Add(p);
                        b = true;
                    }
                }

                s = false;
            }
        }

        private static LinkedList<PolyVertex> GenerateVertexList(Polygon polygon)
        {
            LinkedList<PolyVertex> vertexList = new LinkedList<PolyVertex>();
            LinkedListNode<PolyVertex> currentNode = null;

            // Add all hull points to the linked list
            for (int i = 0; i < polygon.points.Length; i++)
            {
                int prevPointIndex = (i - 1 + polygon.points.Length) % polygon.points.Length;
                int nextPointIndex = (i + 1) % polygon.points.Length;

                bool vertexIsConvex = IsConvex(polygon.points[prevPointIndex], polygon.points[i], polygon.points[nextPointIndex]);
                PolyVertex currentHullVertex = new PolyVertex(polygon.points[i], i, vertexIsConvex);
                currentNode = currentNode == null ? vertexList.AddFirst(currentHullVertex) : vertexList.AddAfter(currentNode, currentHullVertex);
            }

            return vertexList;
        }

        private static bool TriangleContainsVertex(LinkedList<PolyVertex> vertsInClippedPolygon, PolyVertex v0, PolyVertex v1, PolyVertex v2)
        {
            LinkedListNode<PolyVertex> vertexNode = vertsInClippedPolygon.First;
            for (int i = 0; i < vertsInClippedPolygon.Count; i++)
            {
                if (!vertexNode.Value.isConvex) // convex verts will never be inside triangle
                {
                    PolyVertex vertexToCheck = vertexNode.Value;
                    if (vertexToCheck.index != v0.index && vertexToCheck.index != v1.index && vertexToCheck.index != v2.index) // dont check verts that make up triangle
                    {
                        if (PointInTriangle(v0.position, v1.position, v2.position, vertexToCheck.position))
                        {
                            return true;
                        }
                    }
                }
                vertexNode = vertexNode.Next;
            }

            return false;
        }

        private static int[] GenTriangles(Polygon poly)
        {
            int[] tris = new int[(poly.points.Length - 2) * 3];
            int triIndex = 0;
            LinkedList<PolyVertex> vertsInClippedPolygon = GenerateVertexList(poly);
            while (vertsInClippedPolygon.Count >= 3)
            {
                bool hasRemovedEarThisIteration = false;
                LinkedListNode<PolyVertex> vertexNode = vertsInClippedPolygon.First;
                for (int i = 0; i < vertsInClippedPolygon.Count; i++)
                {
                    LinkedListNode<PolyVertex> prevPolyVertexNode = vertexNode.Previous ?? vertsInClippedPolygon.Last;
                    LinkedListNode<PolyVertex> nextPolyVertexNode = vertexNode.Next ?? vertsInClippedPolygon.First;

                    if (vertexNode.Value.isConvex)
                    {
                        if (!TriangleContainsVertex(vertsInClippedPolygon, prevPolyVertexNode.Value, vertexNode.Value, nextPolyVertexNode.Value))
                        {
                            // check if removal of ear makes prev/next vertex convex (if was previously reflex)
                            if (!prevPolyVertexNode.Value.isConvex)
                            {
                                LinkedListNode<PolyVertex> prevOfPrev = prevPolyVertexNode.Previous ?? vertsInClippedPolygon.Last;

                                prevPolyVertexNode.Value.isConvex = IsConvex(prevOfPrev.Value.position, prevPolyVertexNode.Value.position, nextPolyVertexNode.Value.position);
                            }
                            if (!nextPolyVertexNode.Value.isConvex)
                            {
                                LinkedListNode<PolyVertex> nextOfNext = nextPolyVertexNode.Next ?? vertsInClippedPolygon.First;
                                nextPolyVertexNode.Value.isConvex = IsConvex(prevPolyVertexNode.Value.position, nextPolyVertexNode.Value.position, nextOfNext.Value.position);
                            }

                            // add triangle to tri array
                            tris[(triIndex * 3) + 2] = prevPolyVertexNode.Value.index;
                            tris[(triIndex * 3) + 1] = vertexNode.Value.index;
                            tris[triIndex * 3] = nextPolyVertexNode.Value.index;
                            triIndex++;

                            hasRemovedEarThisIteration = true;
                            vertsInClippedPolygon.Remove(vertexNode);
                            break;
                        }
                    }


                    vertexNode = nextPolyVertexNode;
                }

                if (!hasRemovedEarThisIteration)
                {
                    return null;
                }
            }

            return tris;
        }
    }
}