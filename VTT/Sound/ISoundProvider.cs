namespace VTT.Sound
{
    using System;

    public interface ISoundProvider
    {
        int NumChannels { get; }
        int SampleRate { get; }
        bool IsReady { get; }

        void GetRawDataFull(out IntPtr dataPtr, out int dataLength);
        void Free();
    }
}
