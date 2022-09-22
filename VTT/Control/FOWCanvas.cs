namespace VTT.Control
{
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using VTT.Util;

    public class FOWCanvas
    {
        private Image<Rgba64> _img;

        public int Width => this._img.Width;
        public int Height => this._img.Height;
        public Image<Rgba64> Canvas => this._img;

        public bool IsDeleted { get; set; }
        public bool NeedsSave { get; set; }

        private FOWCell[] _cells;

        public object Lock = new object();

        public FOWCanvas(int w, int h)
        {
            Rgba64 back = new Rgba64(0, 0, 0, 0);
            this._img = new Image<Rgba64>(w, h, back);
            this.InitCellsArray();
        }

        public FOWCanvas()
        {
        }

        internal Rgba64 GetPixel(int x, int y)
        {
            if (this.IsDeleted)
            {
                return default;
            }

            return this._img[x, y];
        }

        internal void SetPixel(int x, int y, Rgba64 pixel)
        {
            if (this.IsDeleted)
            {
                return;
            }

            this._img[x, y] = pixel;
            this.NeedsSave = true;
        }

        public void Write(BinaryWriter bw) => this._img.SaveAsPng(bw.BaseStream);

        public void Write(string file) => this._img.SaveAsPng(file);

        public void Read(BinaryReader br)
        {
            this._img = Image.Load<Rgba64>(br.BaseStream);
            this.InitCellsArray();
        }

        public void Read(string file)
        {
            this._img = Image.Load<Rgba64>(file);
            this.InitCellsArray();
        }

        private void InitCellsArray()
        {
            int w = this._img.Width;
            int h = this._img.Height;
            this._cells = new FOWCell[w * h];
            for (int i = 0; i < w * h; ++i)
            {
                this._cells[i] = new FOWCell(i % w, i / w, this);
            }
        }

        public void Dispose()
        {
            this._img.Dispose();
            this._cells = null;
        }

        public bool ProcessPolygon(Vector2[] polygon, bool reveal)
        {
            Vector2 offset = new Vector2(this.Width, this.Height) / 2f;
            offset = new Vector2(0.5f) + new Vector2(MathF.Floor(offset.X), MathF.Floor(offset.Y));

            for (int i = 0; i < polygon.Length; i++)
            {
                polygon[i] += offset;
            }

            Vector2 tl = Extensions.ComponentMin(polygon);
            Vector2 br = Extensions.ComponentMax(polygon);

            RectangleF rSelf = new RectangleF(0, 0, this.Width, this.Height);
            RectangleF rPoly = new RectangleF(tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y);
            if (rSelf.IntersectsWith(rPoly) || rSelf.Contains(rPoly) || rPoly.Contains(rSelf))
            {
                if (PolygonDecomposer.IsSimple(polygon))
                {
                    PolygonDecomposer.MakeCCW(ref polygon);
                    Vector2[][] polys = PolygonDecomposer.QuickDecompose(polygon);
                    RectangleF[] polyRects = polys.Select(p =>
                    {
                        Vector2 vMin = Extensions.ComponentMin(p);
                        Vector2 vMax = Extensions.ComponentMax(p);
                        return new RectangleF(vMin.X, vMin.Y, vMax.X - vMin.X, vMax.Y - vMin.Y);
                    }).ToArray();

                    int nXMin = (int)MathF.Floor(tl.X);
                    int nXMax = (int)MathF.Ceiling(br.X);
                    int nYMin = (int)MathF.Floor(tl.Y);
                    int nYMax = (int)MathF.Ceiling(br.Y);

                    int deltaX = nXMax - nXMin;
                    int deltaY = nYMax - nYMin;

                    ConcurrentBag<FOWCellAction> actions = new ConcurrentBag<FOWCellAction>();

                    Parallel.For(0, deltaX * deltaY, (i) =>
                    {
                        int dx = nXMin + (i % deltaX);
                        int dy = nYMin + (i / deltaX);

                        if (dx > 0 && dx < this.Width && dy > 0 && dy < this.Height)
                        {
                            RectangleF cellRect = new RectangleF(dx, dy, 1, 1);
                            for (int j = 0; j < polys.Length; ++j)
                            {
                                if (polyRects[j].IntersectsWith(cellRect) || polyRects[j].Contains(cellRect) || cellRect.Contains(polyRects[j])) // Have a cell to process
                                {
                                    ProcessCell(actions, dx, dy, polys[j], reveal);
                                }
                            }
                        }
                    });

                    bool result = false;
                    foreach (FOWCellAction acts in actions)
                    {
                        int idx = (acts.Y * this.Width) + acts.X;
                        result |= this._cells[idx].Mask(acts.Mask, acts.Action);
                    }

                    if (result)
                    {
                        this.NeedsSave = true;
                    }

                    return result;
                }
            }

            return false;
        }

        private static void ProcessCell(ConcurrentBag<FOWCellAction> actions, int dx, int dy, Vector2[] polygon, bool reveal)
        {
            // Have a broad phase intersection with a polygon, perform a 'narrow' check
            Vector2[] cellPoly = new Vector2[4] { new Vector2(dx + 1, dy + 1), new Vector2(dx, dy + 1), new Vector2(dx, dy), new Vector2(dx + 1, dy) };
            if (Intersects(polygon, new RectangleF(dx, dy, 1, 1))) // Narrowed broad intersection, perform additional narrow intersections for sub-cells
            {
                ulong mask = 0ul;
                float stepX = 0.125f;
                float stepY = 0.125f;

                float cursorX;
                float cursorY;

                for (int i = 0; i < 64; ++i)
                {
                    cursorX = dx + (stepX * (i % 8));
                    cursorY = dy + (stepY * (i / 8));
                    cellPoly[0] = new Vector2(cursorX + stepX, cursorY + stepY);
                    cellPoly[1] = new Vector2(cursorX, cursorY + stepY);
                    cellPoly[2] = new Vector2(cursorX, cursorY);
                    cellPoly[3] = new Vector2(cursorX + stepX, cursorY);
                    if (Intersects(polygon, new RectangleF(cursorX, cursorY, stepX, stepY))) // Polygon/small cell intersection
                    {
                        mask |= 1ul << i;
                    }
                }

                FOWCellAction act = new FOWCellAction() { X = dx, Y = dy, Mask = mask, Action = reveal };
                actions.Add(act);
            }
        }

        private static bool Intersects(Vector2[] polygonA, RectangleF rect)
        {
            Vector2 rectArbitraryPoint = new Vector2((rect.Width * 0.5f) + rect.X, (rect.Height * 0.5f) + rect.Y);
            if (IsPointInPoly(polygonA, rectArbitraryPoint))
            {
                return true; // Rect point is in polygon
            }

            Vector2 polyArbitraryPoint = polygonA[0];
            if (rect.Contains(polyArbitraryPoint.X, polyArbitraryPoint.Y))
            {
                return true; // Polygon point is in rectangle
            }

            for (int i = 0; i < polygonA.Length; ++i)
            {
                Vector2 now = polygonA[i];
                Vector2 next = polygonA[(i + 1) % polygonA.Length];
                float x1 = now.X;
                float x2 = next.X;
                float y1 = now.Y;
                float y2 = next.Y;
                if (
                    (x1 <= rect.Left && x2 <= rect.Left) || 
                    (y1 <= rect.Top && y2 <= rect.Top) ||
                    (x1 >= rect.Right && x2 >= rect.Right) ||
                    (y1 >= rect.Bottom && y2 >= rect.Bottom)
                )
                {
                    continue; // Line outside of rectangle
                }

                float m = (y2 - y1) / (x2 - x1);
                float y = (m * (rect.Left - x1)) + y1;
                if (y > rect.Top && y < rect.Bottom)
                {
                    return true;
                }

                y = (m * (rect.Right - x1)) + y1;
                if (y > rect.Top && y < rect.Bottom)
                {
                    return true;
                }

                float x = ((rect.Top - y1) / m) + x1;
                if (x > rect.Left && x < rect.Right)
                {
                    return true;
                }

                x = ((rect.Bottom - y1) / m) + x1;
                if (x > rect.Left && x < rect.Right)
                {
                    return true;
                }
            }

            return false;

            #region Old
            /*
            Vector2[][] polygons = new Vector2[][] { polygonA, polygonB };
            float minA, maxA, projected, minB, maxB;
            for (var i = 0; i < polygons.Length; i++)
            {
                Vector2[] polygon = polygons[i];
                for (int i1 = 0; i1 < polygon.Length; ++i1)
                {
                    int i2 = (i1 + 2) % polygon.Length;
                    Vector2 normal = new Vector2(polygon[i2].Y - polygon[i1].Y, polygon[i1].X - polygon[i2].X);
                    minA = float.MaxValue;
                    maxA = float.MinValue;

                    for (int j = 0; j < polygonA.Length; ++j)
                    {
                        projected = normal.X * polygonA[j].X + normal.Y * polygonA[j].Y;
                        if (projected < minA)
                        {
                            minA = projected;
                        }

                        if (projected > maxA)
                        {
                            maxA = projected;
                        }
                    }

                    minB = float.MaxValue;
                    maxB = float.MinValue;
                    for (int j = 0; j < polygonB.Length; ++j)
                    {
                        projected = normal.X * polygonB[j].X + normal.Y * polygonB[j].Y;
                        if (projected < minB)
                        {
                            minB = projected;
                        }
                        if (projected > maxB)
                        {
                            maxB = projected;
                        }
                    }

                    if (maxA < minB || maxB < minA)
                    {
                        return false;
                    }
                }
            }

            return true;
            */
            #endregion
        }

        private static bool IsPointInPoly(Vector2[] polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if ((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y) &&
                     p.X < ((polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y)) + polygon[i].X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }

    public class FOWCell
    {
        private int _x;
        private int _y;
        private FOWCanvas _canvas;

        public FOWCell(int x, int y, FOWCanvas canvas)
        {
            this._x = x;
            this._y = y;
            this._canvas = canvas;
        }

        public bool Set(bool value, int x, int y)
        {
            Rgba64 pixel = this._canvas.GetPixel(this._x, this._y);
            ulong val = pixel.PackedValue;
            ulong mask = 1UL << (x & 7) << (y << 3);
            if (!value)
            {
                val &= ~mask;
            }
            else
            {
                val |= mask;
            }

            bool r = pixel.PackedValue != val;
            pixel.PackedValue = val;
            this._canvas.SetPixel(this._x, this._y, pixel);
            return r;
        }

        public bool Mask(ulong mask, bool action)
        {
            Rgba64 pixel = this._canvas.GetPixel(this._x, this._y);
            ulong val = pixel.PackedValue;
            if (!action)
            {
                val &= ~mask;
            }
            else
            {
                val |= mask;
            }

            bool r = val != pixel.PackedValue;
            pixel.PackedValue = val;
            this._canvas.SetPixel(this._x, this._y, pixel);
            return r;
        }
    }

    public class FOWCellAction
    {
        public int X { get; set; }
        public int Y { get; set; }

        public ulong Mask { get; set; }
        public bool Action { get; set; }
    }
}
