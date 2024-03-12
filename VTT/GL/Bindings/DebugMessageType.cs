namespace VTT.GL.Bindings
{
    public enum DebugMessageType : uint
    {
        Error = 0x824C,
        DeprecatedBehaviour = 0x824D,
        UndefinedBehaviour = 0x824E,
        Portability = 0x824F,
        Performance = 0x8250,
        Other = 0x8251,
        Marker = 0x8268,
        PushGroup = 0x8269,
        PopGroup = 0x826A
    }
}