namespace VTT.Util
{
    using OpenTK.Mathematics;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public static class PolygonDecomposer
    {
        private static Vector2 LineIntersection(Vector2[] l1, Vector2[] l2, float precision)
        {
            Vector2 i = new Vector2(0, 0); // point
            float a1, b1, c1, a2, b2, c2, det; // scalars
            a1 = l1[1].Y - l1[0].Y;
            b1 = l1[0].X - l1[1].X;
            c1 = (a1 * l1[0].X) + (b1 * l1[0].Y);
            a2 = l2[1].Y - l2[0].Y;
            b2 = l2[0].X - l2[1].X;
            c2 = (a2 * l2[0].X) + (b2 * l2[0].Y);
            det = (a1 * b2) - (a2 * b1);
            if (!FloatEquals(det, 0, precision))
            { // lines are not parallel
                i = new Vector2(((b2 * c1) - (b1 * c2)) / det, ((a1 * c2) - (a2 * c1)) / det);
            }

            return i;
        }

        private static bool LineSegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            var da = q2.X - q1.X;
            var db = q2.Y - q1.Y;

            // segments are parallel
            if (((da * dy) - (db * dx)) == 0)
            {
                return false;
            }

            var s = ((dx * (q1[1] - p1[1])) + (dy * (p1[0] - q1[0]))) / ((da * dy) - (db * dx));
            var t = ((da * (p1[1] - q1[1])) + (db * (q1[0] - p1[0]))) / ((db * dx) - (da * dy));

            return (s >= 0 && s <= 1 && t >= 0 && t <= 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleArea(Vector2 a, Vector2 b, Vector2 c) => (((b.X - a.X) * (c.Y - a.Y)) - ((c.X - a.X) * (b.Y - a.Y)));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLeft(Vector2 a, Vector2 b, Vector2 c) => TriangleArea(a, b, c) > 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLeftOn(Vector2 a, Vector2 b, Vector2 c) => TriangleArea(a, b, c) >= 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsRight(Vector2 a, Vector2 b, Vector2 c) => TriangleArea(a, b, c) < 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsRightOn(Vector2 a, Vector2 b, Vector2 c) => TriangleArea(a, b, c) <= 0;

        private static Vector2 tmpPoint1;
        private static Vector2 tmpPoint2;
        private static bool Collinear(Vector2 a, Vector2 b, Vector2 c, float thresholdAngle)
        {
            if (FloatEquals(thresholdAngle, 0, float.Epsilon))
            {
                return TriangleArea(a, b, c) == 0;
            }
            else
            {
                Vector2 ab = tmpPoint1 = new Vector2(b.X - a.X, b.Y - a.Y);
                Vector2 bc = tmpPoint2 = new Vector2(c.X - b.X, c.Y - b.Y);
                float dot = (ab.X * bc.X) + (ab.Y * bc.Y);
                float magA = MathF.Sqrt((ab.X * ab.X) + (ab.Y * ab.Y));
                float magB = MathF.Sqrt((bc.X * bc.X) + (bc.Y * bc.Y));
                float angle = MathF.Acos(dot / (magA * magB));
                return angle < thresholdAngle;
            }
        }

        public static bool MakeCCW(ref Vector2[] polygon)
        {
            int br = 0;
            Vector2[] v = polygon;

            // find bottom right point
            for (int i = 1; i < polygon.Length; ++i)
            {
                if (v[i].Y < v[br].Y || (v[i].Y == v[br].Y && v[i].X > v[br].X))
                {
                    br = i;
                }
            }

            // reverse poly if clockwise
            if (!IsLeft(PolygonAt(polygon, br - 1), PolygonAt(polygon, br), PolygonAt(polygon, br + 1)))
            {
                Array.Reverse(polygon);
                return true;
            }
            else
            {
                return false;
            }
        }

        private static Vector2 PolygonAt(Vector2[] polygon, int i) => polygon[i < 0 ? (i % polygon.Length) + polygon.Length : i % polygon.Length];
        private static Vector2 PolygonAt(List<Vector2> polygon, int i) => polygon[i < 0 ? (i % polygon.Count) + polygon.Count : i % polygon.Count];

        private static bool IsReflex(Vector2[] polygon, int i) => IsRight(PolygonAt(polygon, i - 1), PolygonAt(polygon, i), PolygonAt(polygon, i + 1));

        private static Vector2[] tmpLine1 = new Vector2[2];
        private static Vector2[] tmpLine2 = new Vector2[2];
        private static bool CanSee(Vector2[] polygon, int a, int b)
        {
            Vector2 p;
            float dist;
            Vector2[] l1 = tmpLine1;
            Vector2[] l2 = tmpLine2;

            if (IsLeftOn(PolygonAt(polygon, a + 1), PolygonAt(polygon, a), PolygonAt(polygon, b)) && IsRightOn(PolygonAt(polygon, a - 1), PolygonAt(polygon, a), PolygonAt(polygon, b)))
            {
                return false;
            }

            dist = Vector2.DistanceSquared(PolygonAt(polygon, a), PolygonAt(polygon, b));
            for (int i = 0; i != polygon.Length; ++i)
            { // for each edge
                if ((i + 1) % polygon.Length == a || i == a)
                { // ignore incident edges
                    continue;
                }
                if (IsLeftOn(PolygonAt(polygon, a), PolygonAt(polygon, b), PolygonAt(polygon, i + 1)) && IsRightOn(PolygonAt(polygon, a), PolygonAt(polygon, b), PolygonAt(polygon, i)))
                { // if diag intersects an edge
                    l1[0] = PolygonAt(polygon, a);
                    l1[1] = PolygonAt(polygon, b);
                    l2[0] = PolygonAt(polygon, i);
                    l2[1] = PolygonAt(polygon, i + 1);
                    p = LineIntersection(l1, l2, float.Epsilon);
                    if (Vector2.DistanceSquared(PolygonAt(polygon, a), p) < dist)
                    { // if edge is blocking visibility to b
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool CanSee2(Vector2[] polygon, int a, int b)
        {
            // for each edge
            for (int i = 0; i != polygon.Length; ++i)
            {
                // ignore incident edges
                if (i == a || i == b || (i + 1) % polygon.Length == a || (i + 1) % polygon.Length == b)
                {
                    continue;
                }

                if (LineSegmentsIntersect(PolygonAt(polygon, a), PolygonAt(polygon, b), PolygonAt(polygon, i), PolygonAt(polygon, i + 1)))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<Vector2> p = new List<Vector2>();
        private static Vector2[] CopyPolygon(Vector2[] polygon, int i, int j)
        {
            p.Clear();
            if (i < j)
            {
                // Insert all vertices from i to j
                for (int k = i; k <= j; k++)
                {
                    p.Add(polygon[k]);
                }

            }
            else
            {
                // Insert vertices 0 to j
                for (int k = 0; k <= j; k++)
                {
                    p.Add(polygon[k]);
                }

                // Insert vertices i to end
                for (int k = i; k < polygon.Length; k++)
                {
                    p.Add(polygon[k]);
                }
            }

            return p.ToArray();
        }

        private static List<Vector2[]> GetCutEdges(Vector2[] polygon)
        {
            List<Vector2[]> min = new List<Vector2[]>();
            List<Vector2[]> tmp1;
            List<Vector2[]> tmp2;
            int nDiags = int.MaxValue;

            for (int i = 0; i < polygon.Length; ++i)
            {
                if (IsReflex(polygon, i))
                {
                    for (int j = 0; j < polygon.Length; ++j)
                    {
                        if (CanSee(polygon, i, j))
                        {
                            tmp1 = GetCutEdges(CopyPolygon(polygon, i, j));
                            tmp2 = GetCutEdges(CopyPolygon(polygon, j, i));

                            for (int k = 0; k < tmp2.Count; k++)
                            {
                                tmp1.Add(tmp2[k]);
                            }

                            if (tmp1.Count < nDiags)
                            {
                                min = new List<Vector2[]>(tmp1);
                                nDiags = tmp1.Count;
                                min.Add(new Vector2[] { PolygonAt(polygon, i), PolygonAt(polygon, j) });
                            }
                        }
                    }
                }
            }

            return min;
        }

        public static Vector2[][] Decompose(Vector2[] polygon)
        {
            var edges = GetCutEdges(polygon);
            if (edges.Count > 0)
            {
                return Slice(polygon, edges.ToArray());
            }
            else
            {
                return new Vector2[][] { polygon };
            }
        }

        private static int PolygonIndexOf(Vector2[] polygon, Vector2 point) => Array.FindIndex(polygon, v => Vector2.Equals(v, point));

        private static Vector2[][] Slice(Vector2[] polygon, Vector2[][] cutEdges)
        {
            if (cutEdges.Length == 0)
            {
                return new Vector2[][] { polygon };
            }

            if (cutEdges.Length > 1)
            {
                List<Vector2[]> polys = new List<Vector2[]>();
                polys.Add(polygon);

                for (int i = 0; i < cutEdges.Length; i++)
                {
                    Vector2[] cutEdge = cutEdges[i];
                    for (int j = 0; j < polys.Count; j++)
                    {
                        Vector2[] poly = polys[j];
                        Vector2[][] result = Slice(poly, new Vector2[][] { cutEdge });
                        if (result != null)
                        {
                            // Found poly! Cut and quit
                            polys.RemoveAt(j);
                            polys.Add(result[0]);
                            polys.Add(result[1]);
                            break;
                        }
                    }
                }

                return polys.ToArray();
            } 
            else
            {

                // Was given one edge
                Vector2[] cutEdge = cutEdges[0];
                int i = PolygonIndexOf(polygon, cutEdge[0]);
                int j = PolygonIndexOf(polygon, cutEdge[1]);
                return i != -1 && j != -1 ? (new Vector2[][]{ CopyPolygon(polygon, i, j), CopyPolygon(polygon, j, i) }) : null;
            }
        }

        public static bool IsSimple(Vector2[] polygon)
        {
            int i;
            // Check
            for (i = 0; i < polygon.Length - 1; i++)
            {
                for (int j = 0; j < i - 1; j++)
                {
                    if (LineSegmentsIntersect(polygon[i], polygon[i + 1], polygon[j], polygon[j + 1]))
                    {
                        return false;
                    }
                }
            }

            // Check the segment between the last and the first point to all others
            for (i = 1; i < polygon.Length - 2; i++)
            {
                if (LineSegmentsIntersect(polygon[0], polygon[polygon.Length - 1], polygon[i], polygon[i + 1]))
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector2 GetIntersectionPoint(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2, float delta = float.Epsilon)
        {
            var a1 = p2.Y - p1.Y;
            var b1 = p1.X - p2.X;
            var c1 = (a1 * p1.X) + (b1 * p1.Y);
            var a2 = q2.Y - q1.Y;
            var b2 = q1.X - q2.X;
            var c2 = (a2 * q1.X) + (b2 * q1.Y);
            var det = (a1 * b2) - (a2 * b1);
            return !FloatEquals(det, 0, delta) ? new Vector2(((b2 * c1) - (b1 * c2)) / det, ((a1 * c2) - (a2 * c1)) / det) : Vector2.Zero;
        }

        private static void AppendRange(List<Vector2[]> where, List<Vector2[]> what, int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                where.Add(what[i]);
            }
        }

        private static void AppendRange<T>(List<T> where, T[] what, int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                where.Add(what[i]);
            }
        }

        private static void AppendRange(ref Vector2[][] where, List<Vector2[]> what, int from, int to)
        {
            int amt = to - from;
            int d = where.Length;
            Array.Resize(ref where, where.Length + amt);
            for (int i = from; i < to; i++)
            {
                where[d++] = what[i];
            }
        }

        public static Vector2[][] QuickDecompose(Vector2[] polygon, List<Vector2[]> result = null, List<Vector2> reflexVertices = null, List<Vector2> steinerPoints = null, float delta = 25.0f, int maxlevel = 100, int level = 0)
        {
            result ??= new List<Vector2[]>();
            reflexVertices ??= new List<Vector2>();
            steinerPoints ??= new List<Vector2>();

            Vector2 upperInt = Vector2.Zero;
            Vector2 lowerInt = Vector2.Zero;
            Vector2 p = Vector2.Zero; // Points

            float upperDist = 0, lowerDist = 0, d = 0, closestDist = 0;
            int upperIndex = 0, lowerIndex = 0, closestIndex = 0; // Integers
            List<Vector2> lowerPoly = new List<Vector2>();
            List<Vector2> upperPoly = new List<Vector2>(); // polygons

            Vector2[] poly = polygon;
            Vector2[] v = polygon;

            if (v.Length < 3)
            {
                return result.ToArray();
            }

            level++;
            if (level > maxlevel)
            {
                return result.ToArray();
            }

            for (var i = 0; i < polygon.Length; ++i)
            {
                if (IsReflex(poly, i))
                {
                    reflexVertices.Add(poly[i]);
                    upperDist = lowerDist = float.MaxValue;
                    for (var j = 0; j < polygon.Length; ++j)
                    {
                        if (IsLeft(PolygonAt(poly, i - 1), PolygonAt(poly, i), PolygonAt(poly, j)) && IsRightOn(PolygonAt(poly, i - 1), PolygonAt(poly, i), PolygonAt(poly, j - 1)))
                        { // if line intersects with an edge
                            p = GetIntersectionPoint(PolygonAt(poly, i - 1), PolygonAt(poly, i), PolygonAt(poly, j), PolygonAt(poly, j - 1)); // find the point of intersection
                            if (IsRight(PolygonAt(poly, i + 1), PolygonAt(poly, i), p))
                            { // make sure it's inside the poly
                                d = Vector2.DistanceSquared(poly[i], p);
                                if (d < lowerDist)
                                { // keep only the closest intersection
                                    lowerDist = d;
                                    lowerInt = p;
                                    lowerIndex = j;
                                }
                            }
                        }
                        if (IsLeft(PolygonAt(poly, i + 1), PolygonAt(poly, i), PolygonAt(poly, j + 1)) && IsRightOn(PolygonAt(poly, i + 1), PolygonAt(poly, i), PolygonAt(poly, j)))
                        {
                            p = GetIntersectionPoint(PolygonAt(poly, i + 1), PolygonAt(poly, i), PolygonAt(poly, j), PolygonAt(poly, j + 1));
                            if (IsLeft(PolygonAt(poly, i - 1), PolygonAt(poly, i), p))
                            {
                                d = Vector2.DistanceSquared(poly[i], p);
                                if (d < upperDist)
                                {
                                    upperDist = d;
                                    upperInt = p;
                                    upperIndex = j;
                                }
                            }
                        }
                    }

                    // if there are no vertices to connect to, choose a point in the middle
                    if (lowerIndex == (upperIndex + 1) % polygon.Length)
                    {
                        //console.log("Case 1: Vertex("+i+"), lowerIndex("+lowerIndex+"), upperIndex("+upperIndex+"), poly.size("+polygon.length+")");
                        p[0] = (lowerInt[0] + upperInt[0]) / 2;
                        p[1] = (lowerInt[1] + upperInt[1]) / 2;
                        steinerPoints.Add(p);

                        if (i < upperIndex)
                        {
                            //lowerPoly.insert(lowerPoly.end(), poly.begin() + i, poly.begin() + upperIndex + 1);
                            AppendRange(lowerPoly, poly, i, upperIndex + 1);
                            lowerPoly.Add(p);
                            upperPoly.Add(p);
                            if (lowerIndex != 0)
                            {
                                //upperPoly.insert(upperPoly.end(), poly.begin() + lowerIndex, poly.end());
                                AppendRange(upperPoly, poly, lowerIndex, poly.Length);
                            }
                            //upperPoly.insert(upperPoly.end(), poly.begin(), poly.begin() + i + 1);
                            AppendRange(upperPoly, poly, 0, i + 1);
                        }
                        else
                        {
                            if (i != 0)
                            {
                                //lowerPoly.insert(lowerPoly.end(), poly.begin() + i, poly.end());
                                AppendRange(lowerPoly, poly, i, poly.Length);
                            }
                            //lowerPoly.insert(lowerPoly.end(), poly.begin(), poly.begin() + upperIndex + 1);
                            AppendRange(lowerPoly, poly, 0, upperIndex + 1);
                            lowerPoly.Add(p);
                            upperPoly.Add(p);
                            //upperPoly.insert(upperPoly.end(), poly.begin() + lowerIndex, poly.begin() + i + 1);
                            AppendRange(upperPoly, poly, lowerIndex, i + 1);
                        }
                    }
                    else
                    {
                        // connect to the closest point within the triangle
                        //console.log("Case 2: Vertex("+i+"), closestIndex("+closestIndex+"), poly.size("+polygon.length+")\n");

                        if (lowerIndex > upperIndex)
                        {
                            upperIndex += polygon.Length;
                        }

                        closestDist = float.MaxValue;

                        if (upperIndex < lowerIndex)
                        {
                            return result.ToArray();
                        }

                        for (var j = lowerIndex; j <= upperIndex; ++j)
                        {
                            if (
                                IsLeftOn(PolygonAt(poly, i - 1), PolygonAt(poly, i), PolygonAt(poly, j)) &&
                                IsRightOn(PolygonAt(poly, i + 1), PolygonAt(poly, i), PolygonAt(poly, j))
                            )
                            {
                                d = Vector2.DistanceSquared(PolygonAt(poly, i), PolygonAt(poly, j));
                                if (d < closestDist && CanSee2(poly, i, j))
                                {
                                    closestDist = d;
                                    closestIndex = j % polygon.Length;
                                }
                            }
                        }

                        if (i < closestIndex)
                        {
                            AppendRange(lowerPoly, poly, i, closestIndex + 1);
                            if (closestIndex != 0)
                            {
                                AppendRange(upperPoly, poly, closestIndex, v.Length);
                            }

                            AppendRange(upperPoly, poly, 0, i + 1);
                        }
                        else
                        {
                            if (i != 0)
                            {
                                AppendRange(lowerPoly, poly, i, v.Length);
                            }

                            AppendRange(lowerPoly, poly, 0, closestIndex + 1);
                            AppendRange(upperPoly, poly, closestIndex, i + 1);
                        }
                    }

                    // solve smallest poly first
                    if (lowerPoly.Count < upperPoly.Count)
                    {
                        QuickDecompose(lowerPoly.ToArray(), result, reflexVertices, steinerPoints, delta, maxlevel, level);
                        QuickDecompose(upperPoly.ToArray(), result, reflexVertices, steinerPoints, delta, maxlevel, level);
                    }
                    else
                    {
                        QuickDecompose(upperPoly.ToArray(), result, reflexVertices, steinerPoints, delta, maxlevel, level);
                        QuickDecompose(lowerPoly.ToArray(), result, reflexVertices, steinerPoints, delta, maxlevel, level);
                    }

                    return result.ToArray();
                }
            }

            result.Add(polygon);
            return result.ToArray();
        }

        public static int RemoveCollinearPoints(ref Vector2[] polygon, float precision)
        {
            var num = 0;
            List<Vector2> poly = new List<Vector2>(polygon);
            for (var i = poly.Count - 1; poly.Count > 3 && i >= 0; --i)
            {
                if (Collinear(PolygonAt(poly, i - 1), PolygonAt(poly, i), PolygonAt(poly, i + 1), precision))
                {
                    // Remove the middle point
                    poly.RemoveAt(i % poly.Count);
                    num++;
                }
            }

            polygon = poly.ToArray();
            return num;
        }

        /**
         * Remove duplicate points in the polygon.
         * @method removeDuplicatePoints
         * @param  {Number} [precision] The threshold to use when determining whether two points are the same. Use zero for best precision.
         */
        public static void RemoveDuplicatePoints(ref Vector2[] polygon, float precision)
        {
            List<Vector2> poly = new List<Vector2>(polygon);
            for (var i = poly.Count - 1; i >= 1; --i)
            {
                var pi = poly[i];
                for (var j = i - 1; j >= 0; --j)
                {
                    if (PointsEquals(pi, poly[j], precision))
                    {
                        poly.RemoveAt(i);
                        continue;
                    }
                }
            }

            polygon = poly.ToArray();
        }

        public static bool FloatEquals(float a, float b, float precision) => MathF.Abs(a - b) <= precision;
        public static bool PointsEquals(Vector2 a, Vector2 b, float precision) => FloatEquals(a.X, b.X, precision) && FloatEquals(a.Y, b.Y, precision);
    }
}
