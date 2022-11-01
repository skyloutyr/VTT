namespace VTT.Util
{
    public static class VTTMath
    {
        public static unsafe float UInt32BitsToSingle(uint val) => *(float*)&val;
        public static unsafe float Int32BitsToSingle(int val) => *(float*)&val;
        public static unsafe uint SingleBitsToUInt32(float val) => *(uint*)&val;
        public static unsafe int SingleBitsToInt32(float val) => *(int*)&val;
    }
}
