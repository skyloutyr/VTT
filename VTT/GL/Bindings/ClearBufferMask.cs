namespace VTT.GL.Bindings
{
    using System;

    [Flags]
    public enum ClearBufferMask
    {
        Depth = 0x00000100,
        Stencil = 0x00000400,
        Color = 0x00004000
    }
}