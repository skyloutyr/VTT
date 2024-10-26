namespace VTT.GLFW
{
    using System.Runtime.InteropServices;
    using VTT.Util;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly unsafe struct GLFWimage
    {
        public readonly int width;
        public readonly int height;
        public readonly byte* pixels;

        public GLFWimage(int width, int height, byte[] pixels)
        {
            this.width = width;
            this.height = height;
            this.pixels = MemoryHelper.Allocate<byte>((nuint)pixels.Length);
            for (int i = pixels.Length - 1; i >= 0; --i)
            {
                this.pixels[i] = pixels[i];
            }
        }

        public GLFWimage(int width, int height, byte* pixels)
        {
            this.width = width;
            this.height = height;
            this.pixels = pixels;
        }

        public void Free() => MemoryHelper.Free(this.pixels);
    }
}
