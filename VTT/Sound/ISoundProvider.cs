namespace VTT.Sound
{
    using System;

    public interface ISoundProvider
    {
        int NumChannels { get; }
        int SampleRate { get; }
        bool IsReady { get; }
        double Duration { get; }

        void GetRawDataFull(out IntPtr dataPtr, out int dataLength);
        void Free();
    }
}
