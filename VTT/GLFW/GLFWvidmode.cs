namespace VTT.GLFW
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 24)]
    public readonly struct GLFWvidmode
    {
        [FieldOffset(0)]
        public readonly int width;

        [FieldOffset(4)]
        public readonly int height;

        [FieldOffset(8)]
        public readonly int redBits;

        [FieldOffset(12)]
        public readonly int greenBits;

        [FieldOffset(16)]
        public readonly int blueBits;

        [FieldOffset(20)]
        public readonly int refreshRate;
    }
}
