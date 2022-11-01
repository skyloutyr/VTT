namespace VTT.Render
{
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using System;
    using VTT.Util;

    public class TriRectTester
    {
        [Flags]
        private enum Region
        {
            TopLeft = 1,
            Top = 2,
            TopRight = 4,
            Left = 8,
            Inside = 16,
            Right = 32,
            BottomLeft = 64,
            Bottom = 128,
            BottomRight = 256
        }

        static TriRectTester()
        {
            Region[] rs = Enum.GetValues<Region>();
            foreach (Region r1 in rs)
            {
                foreach (Region r2 in rs)
                {
                    foreach (Region r3 in rs)
                    {
                        answers[(int)(r1 | r2 | r3)] = DoRegionsIntersects_Lookup(r1, r2, r3);
                    }
                }
            }
        }

        private static Region GetPointRegion(in RectangleF box, in Vector2 point)
        {
            return 
                point.X < box.Left ? 
                    point.Y < box.Top ? Region.TopLeft : 
                    point.Y > box.Bottom ? Region.BottomLeft : 
                    Region.Left 
                : point.X > box.Right ? 
                    point.Y < box.Top ? Region.TopRight : 
                    point.Y > box.Bottom ? Region.BottomRight : 
                    Region.Right : 
                point.Y < box.Top ? 
                    Region.Top : 
                point.Y > box.Bottom ? 
                    Region.Bottom : 
                    Region.Inside;
        }

        private static readonly int[] answers = new int[512];

        private static bool Do2RegionsIntersect(Region r1, Region r2)
        {
            return (((r1 | r2) & Region.Inside) != 0) ||
                ((r1 | r2) == (Region.Left | Region.Right)) ||
                ((r1 | r2) == (Region.Top | Region.Bottom));
        }

        private static int DoRegionsIntersects_Lookup(Region r1, Region r2, Region r3)
        {
            Region r23 = r2 | r3;
            switch (r1)
            {
                case Region.TopLeft:
                {
                    if (r23 is (Region.Bottom | Region.Right) or
                        (Region.Bottom | Region.TopRight) or
                        (Region.Right | Region.BottomLeft))
                    {
                        return 1;
                    }
                    else if ((r23 & (Region.TopLeft | Region.Left | Region.BottomLeft)) == r23 ||
                               (r23 & (Region.TopLeft | Region.Top | Region.TopRight)) == r23)
                    {
                        return -1;
                    }

                    goto case Region.Top;
                }
                case Region.Top:
                {
                    if (r23 is (Region.Left | Region.BottomRight) or
                        (Region.Right | Region.BottomLeft))
                    {
                        return 1;
                    }
                    else if ((r23 & (Region.TopLeft | Region.Top | Region.TopRight)) == r23)
                    {
                        return -1;
                    }

                    goto case Region.TopRight;
                }
                case Region.TopRight:
                {
                    if (r23 is (Region.Bottom | Region.Left) or
                        (Region.Bottom | Region.TopLeft) or
                        (Region.Left | Region.BottomRight))
                    {
                        return 1;
                    }
                    else if ((r23 & (Region.TopRight | Region.Right | Region.BottomRight)) == r23 ||
                               (r23 & (Region.TopRight | Region.Top | Region.TopLeft)) == r23)
                    {
                        return -1;
                    }

                    goto case Region.Left;
                }
                case Region.Left:
                {
                    if (r23 is (Region.Top | Region.BottomRight) or
                        (Region.Bottom | Region.TopRight))
                    {
                        return 1;
                    }
                    else if ((r23 & (Region.TopLeft | Region.Left | Region.BottomLeft)) == r23)
                    {
                        return -1;
                    }

                    goto case Region.Right;
                }
                case Region.Right:
                {
                    if (r23 is (Region.Top | Region.BottomLeft) or
                        (Region.Bottom | Region.TopLeft))
                    {
                        return 1;
                    }
                    else if ((r23 & (Region.TopRight | Region.Right | Region.BottomRight)) == r23)
                    {
                        return -1;
                    }

                    goto case Region.BottomLeft;
                }
                case Region.BottomLeft:
                {
                    if (r23 is (Region.Top | Region.Right) or
                        (Region.Top | Region.BottomRight) or
                        (Region.Right | Region.TopLeft))
                    {
                        return 1;
                    }
                    else if ((r23 & (Region.BottomLeft | Region.Left | Region.TopLeft)) == r23 ||
                               (r23 & (Region.BottomLeft | Region.Bottom | Region.BottomRight)) == r23)
                    {
                        return -1;
                    }

                    goto case Region.Bottom;
                }
                case Region.Bottom:
                {
                    if (r23 is (Region.Left | Region.TopRight) or
                        (Region.Right | Region.TopLeft))
                    {
                        return 1;
                    }
                    else if ((r23 & (Region.BottomLeft | Region.Bottom | Region.BottomRight)) == r23)
                    {
                        return -1;
                    }

                    goto case Region.BottomRight;
                }
                case Region.BottomRight:
                {
                    if (r23 is (Region.Top | Region.Left) or
                        (Region.Top | Region.BottomLeft) or
                        (Region.Left | Region.TopRight))
                    {
                        return 1;
                    }
                    else if ((r23 & (Region.BottomRight | Region.Right | Region.TopRight)) == r23 ||
                               (r23 & (Region.BottomRight | Region.Bottom | Region.BottomLeft)) == r23)
                    {
                        return -1;
                    }

                    goto default;
                }
                default:
                {
                    return 0;
                }
            }
        }
        private static int DoRegionsIntersect(Region r1, Region r2, Region r3) => ((answers[(int)(r1 | r2 | r3) >> 2] >> (((int)(r1 | r2 | r3) & 3) << 1)) & 3) - 1;

        private static bool SegmentIntersects(in RectangleF rect, in Vector2 p1, in Vector2 p2, Region r1, Region r2) 
        {
            // Skip if intersection is impossible
            Region r12 = r1 | r2;
            if ((r12 & (Region.TopLeft | Region.Top | Region.TopRight)) == r12 ||
                (r12 & (Region.BottomLeft | Region.Bottom | Region.BottomRight)) == r12 ||
                (r12 & (Region.TopLeft | Region.Left | Region.BottomLeft)) == r12 ||
                (r12 & (Region.TopRight | Region.Right | Region.BottomRight)) == r12) {
                return false;
            }
            float dx = p2.X - p1.X;
                float dy = p2.Y - p1.Y;
            if (MathF.Abs(dx) < float.Epsilon || MathF.Abs(dy) < float.Epsilon) 
            {
                // Vertical or horizontal line (or zero-sized vector)
                // If there were intersection we would have already picked it up
                return false;
            }
            float t = (rect.Left - p1.X) / dx;
            if (t is >= 0.0f and <= 1.0f) 
            {
                return true;
            }

            t = (rect.Right - p1.X) / dx;
            if (t is >= 0.0f and <= 1.0f) 
            {
                return true;
            }

            t = (rect.Top - p1.Y) / dy;
            if (t is >= 0.0f and <= 1.0f) 
            {
                return true;
            }

            t = (rect.Bottom - p1.Y) / dy;
            return t is >= 0.0f and <= 1.0f;
        }

        public static bool Intersects(in RectangleF rect, in Vector2 tp0, in Vector2 tp1, in Vector2 tp2) 
        {
            // Find plane regions for each point
            Region r1 = GetPointRegion(in rect, in tp0);
            Region r2 = GetPointRegion(in rect, in tp1);
            Region r3 = GetPointRegion(in rect, in tp2);

            if (Do2RegionsIntersect(r1, r2) || Do2RegionsIntersect(r1, r3) || Do2RegionsIntersect(r2, r3))
            {
                return true;
            }

            // Check if the three regions imply or forbid intersection
            return DoRegionsIntersect(r1, r2, r3) switch
            {
                1 => true,
                -1 => false,
                // Check segment intersections
                _ => SegmentIntersects(in rect, in tp0, in tp1, r1, r2) || SegmentIntersects(in rect, in tp0, in tp2, r1, r3) || SegmentIntersects(in rect, in tp1, in tp2, r2, r3),
            };
        }

        public static bool Contains(in Vector2 pt, in Vector2 tp0, in Vector2 tp1, in Vector2 tp2)
        {
            static float sign(Vector2 p1, Vector2 p2, Vector2 p3) => ((p1.X - p3.X) * (p2.Y - p3.Y)) - ((p2.X - p3.X) * (p1.Y - p3.Y));

            float d1, d2, d3;
            bool has_neg, has_pos;

            d1 = sign(pt, tp0, tp1);
            d2 = sign(pt, tp1, tp2);
            d3 = sign(pt, tp2, tp0);

            has_neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            has_pos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(has_neg && has_pos);
        }

        private static readonly BoxSide[] bsArray = new BoxSide[6];
        public static bool Intersects(in RectangleF rect, in BBBox box, in Vector3 bboxOffset, Camera caster)
        {
            Vector4 s = new Vector4(box.Start);
            Vector4 e = new Vector4(box.End);

            Matrix4 matMod = Matrix4.CreateFromQuaternion(box.Rotation) * Matrix4.CreateTranslation(bboxOffset);

            Vector3 p0 = caster.ToScreenspace((new Vector4(s.X, s.Y, s.Z, 1.0f) * matMod).Xyz); // -X, -Y, -Z
            Vector3 p1 = caster.ToScreenspace((new Vector4(e.X, s.Y, s.Z, 1.0f) * matMod).Xyz); // +X, -Y, -Z
            Vector3 p2 = caster.ToScreenspace((new Vector4(s.X, e.Y, s.Z, 1.0f) * matMod).Xyz); // -X, +Y, -Z
            Vector3 p3 = caster.ToScreenspace((new Vector4(e.X, e.Y, s.Z, 1.0f) * matMod).Xyz); // +X, +Y, -Z
            Vector3 p4 = caster.ToScreenspace((new Vector4(s.X, s.Y, e.Z, 1.0f) * matMod).Xyz); // -X, -Y, +Z
            Vector3 p5 = caster.ToScreenspace((new Vector4(e.X, s.Y, e.Z, 1.0f) * matMod).Xyz); // +X, -Y, +Z
            Vector3 p6 = caster.ToScreenspace((new Vector4(s.X, e.Y, e.Z, 1.0f) * matMod).Xyz); // -X, +Y, +Z
            Vector3 p7 = caster.ToScreenspace((new Vector4(e.X, e.Y, e.Z, 1.0f) * matMod).Xyz); // +X, +Y, +Z

            Vector3 max = VMax(p0, p1, p2, p3, p4, p5, p6, p7);
            Vector3 min = VMin(p0, p1, p2, p3, p4, p5, p6, p7);

            if (min.Z <= 0)
            {
                return false; // Outside of camera's near plane
            }

            if (max.Z >= 100.0f)
            {
                return false; // Outside of camera's far plane
            }

            RectangleF boxRect = new RectangleF(min.X, min.Y, max.X - min.X, max.Y - min.Y);
            if (boxRect.IntersectsWith(rect) || boxRect.Contains(rect) || rect.Contains(boxRect)) // Broad phase
            {
                bsArray[0] = new BoxSide(p0, p2, p4, p6); // -X
                bsArray[1] = new BoxSide(p0, p1, p4, p5); // -Y
                bsArray[2] = new BoxSide(p0, p1, p2, p3); // -Z
                bsArray[3] = new BoxSide(p1, p3, p5, p7); // +X
                bsArray[4] = new BoxSide(p2, p3, p6, p7); // +Y
                bsArray[5] = new BoxSide(p4, p5, p6, p7); // +Z

                Vector2 t0, t1, t2;
                for (int i = 0; i < bsArray.Length; i++)
                {
                    BoxSide side = bsArray[i];
                    t0 = side.p0;
                    t1 = side.p1;
                    t2 = side.p2;

                    if (Intersects(in rect, in t0, in t1, in t2))
                    {
                        return true;
                    }

                    if (
                        Contains(new Vector2(rect.Left, rect.Top), t0, t1, t2) || 
                        Contains(new Vector2(rect.Right, rect.Top), t0, t1, t2) || 
                        Contains(new Vector2(rect.Left, rect.Bottom), t0, t1, t2) || 
                        Contains(new Vector2(rect.Right, rect.Bottom), t0, t1, t2)
                    )
                    {
                        return true;
                    }

                    t0 = side.p0;
                    t1 = side.p2;
                    t2 = side.p3;
                    if (Intersects(in rect, in t0, in t1, in t2))
                    {
                        return true;
                    }

                    if (
                        Contains(new Vector2(rect.Left, rect.Top), t0, t1, t2) ||
                        Contains(new Vector2(rect.Right, rect.Top), t0, t1, t2) ||
                        Contains(new Vector2(rect.Left, rect.Bottom), t0, t1, t2) ||
                        Contains(new Vector2(rect.Right, rect.Bottom), t0, t1, t2)
                    )
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Vector3 VMax(in Vector3 v0, in Vector3 v1, in Vector3 v2, in Vector3 v3, in Vector3 v4, in Vector3 v5, in Vector3 v6, in Vector3 v7)
        {
            Vector3 r = Vector3.ComponentMax(v0, v1);
            r = Vector3.ComponentMax(r, v2);
            r = Vector3.ComponentMax(r, v3);
            r = Vector3.ComponentMax(r, v4);
            r = Vector3.ComponentMax(r, v5);
            r = Vector3.ComponentMax(r, v6);
            r = Vector3.ComponentMax(r, v7);
            return r;
        }

        private static Vector3 VMin(in Vector3 v0, in Vector3 v1, in Vector3 v2, in Vector3 v3, in Vector3 v4, in Vector3 v5, in Vector3 v6, in Vector3 v7)
        {
            Vector3 r = Vector3.ComponentMin(v0, v1);
            r = Vector3.ComponentMin(r, v2);
            r = Vector3.ComponentMin(r, v3);
            r = Vector3.ComponentMin(r, v4);
            r = Vector3.ComponentMin(r, v5);
            r = Vector3.ComponentMin(r, v6);
            r = Vector3.ComponentMin(r, v7);
            return r;
        }

        private readonly struct BoxSide
        {
            public readonly Vector2 p0;
            public readonly Vector2 p1;
            public readonly Vector2 p2;
            public readonly Vector2 p3;

            public BoxSide(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
            {
                this.p0 = p0;
                this.p1 = p1;
                this.p2 = p2;
                this.p3 = p3;
            }

            public BoxSide(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) : this(p0.Xy, p1.Xy, p2.Xy, p3.Xy)
            {
            }
        }
    }
}
