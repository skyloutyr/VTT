namespace VTT.GL.Bindings
{
    public enum TextureTarget
    {
        Texture1D = 0x0DE0,
        Texture2D = 0x0DE1,
        Texture3D = 0x806F,
        Texture1DArray = 0x8C18,
        Texture2DArray = 0x8C1A,
        Rectangle = 0x84F5,
        CubeMap = 0x8513,
        CubeMapArray = 0x9009,
        Buffer = 0x8C2A,
        Texture2DMultisample = 0x9100,
        Texture2DMultisampleArray = 0x9102,
        CubeMapPositiveX = 0x8515,
        CubeMapNegativeX = 0x8516,
        CubeMapPositiveY = 0x8517,
        CubeMapNegativeY = 0x8518,
        CubeMapPositiveZ = 0x8519,
        CubeMapNegativeZ = 0x851A
    }
}