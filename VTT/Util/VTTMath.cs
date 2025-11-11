namespace VTT.Util
{
    using System;
    using System.Numerics;

    public static class VTTMath
    {
        public static unsafe float UInt32BitsToSingle(uint val) => *(float*)&val;
        public static unsafe float Int32BitsToSingle(int val) => *(float*)&val;
        public static unsafe uint SingleBitsToUInt32(float val) => *(uint*)&val;
        public static unsafe int SingleBitsToInt32(float val) => *(int*)&val;
        public static unsafe ushort ReinterpretCastInt16ToUInt16(short s) => *(ushort*)&s;

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) => ((p1.X - p3.X) * (p2.Y - p3.Y)) - ((p2.X - p3.X) * (p1.Y - p3.Y));
        public static bool PointInTriangle(Vector2 pt, Vector2 v1, Vector2 v2, Vector2 v3)
        {
            float d1, d2, d3;
            bool has_neg, has_pos;

            d1 = Sign(pt, v1, v2);
            d2 = Sign(pt, v2, v3);
            d3 = Sign(pt, v3, v1);

            has_neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            has_pos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(has_neg && has_pos);
        }

        public static Vector3 InterpolateBezier(Vector3 start, Vector3 end, Vector3 control, float a) => Vector3.Lerp(Vector3.Lerp(start, control, a), Vector3.Lerp(control, end, a), a);

        public static float EncodeAsUint14142(Vector3 normal, float extra = 0)
        {
            uint x = (uint)MathF.Floor(MathF.Abs(normal.X) * 16383);
            uint y = (uint)MathF.Floor(MathF.Abs(normal.Y) * 16383);
            uint sx = normal.X < 0 ? 8u : 0u;
            uint sy = normal.Y < 0 ? 4u : 0u;
            uint sz = normal.Z < 0 ? 2u : 0u;
            uint sw = extra < 0 ? 1u : 0u;
            return UInt32BitsToSingle(((x & 0x7fff) << 18) | ((y & 0x7fff) << 4) | sx | sy | sz | sw);
        }

        public static float EncodeAsUint1010102(Vector4 v4, uint padding = 0u)
        {
            return UInt32BitsToSingle(
                ((uint)MathF.Floor(v4.X * 1023) << 22) |
                ((uint)MathF.Floor(v4.Y * 1023) << 12) |
                ((uint)MathF.Floor(v4.Z * 1023) << 2) | padding
            );
        }

        public static bool CompareFloat(float f, float to, float epsilon = 1e-5f) => MathF.Abs(f - to) <= epsilon;

        public static (float, float) ClampKeepAR(float w, float h, float mw, float mh)
        {
            if (w <= mw && h <= mh)
            {
                return (w, h);
            }

            float ar = w / h;
            // Three cases to look at
            // One - both are larger than maximum allowed.
            if (w > mw && h > mh)
            {
                // Find the maximum delta, treat that side
                float dx = w - mw;
                float dy = h - mh;
                if (dx > dy)
                {
                    w = mw;
                    h = w / ar; // In this case w > h, so to preserve aspect h = w / ar (ar > 1)
                    return (w, h);
                }
                else
                {
                    h = mh;
                    w = h * ar; // In this case h >= w, so to preserve aspect w = h * ar (ar <= 1)
                    return (w, h);
                }
            }

            // Two - width is greater, but height isn't
            if (w > mw)
            {
                w = mw;
                h = w / ar; // In this case w > h, so to preserve aspect h = w / ar (ar > 1)
                return (w, h);
            }
            else // Three - height is greater, but width isn't
            {
                h = mh;
                w = h * ar; // In this case h >= w, so to preserve aspect w = h * ar (ar <= 1)
                return (w, h);
            }
        }
    }
}
