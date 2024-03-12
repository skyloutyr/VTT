namespace VTT.GLFW
{
    using System;

    [Flags]
    public enum ModifierKeys
    {
        Shift = 1,
        Control = 2,
        Alt = 4,
        Super = 8,
        CapsLock = 16,
        NumLock = 32
    }
}
