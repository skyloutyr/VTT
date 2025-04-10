namespace VTT.Util
{
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
    }
}
