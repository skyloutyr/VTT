namespace VTT.Sound.Bindings
{
    using System;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using static ALLoader;

    public static unsafe class AL
    {
        public static uint GenBuffer()
        {
            uint i = 0;
            alGenBuffers(1, &i);
            return i;
        }

        public static ReadOnlySpan<uint> GenBuffers(int n)
        {
            uint[] ret = new uint[n];
            GCHandle hnd = GCHandle.Alloc(ret, GCHandleType.Pinned);
            alGenBuffers(n, (uint*)Marshal.UnsafeAddrOfPinnedArrayElement(ret, 0));
            hnd.Free();
            return ret;
        }

        public static uint GenSource()
        {
            uint i = 0;
            alGenSources(1, &i);
            return i;
        }

        public static ReadOnlySpan<uint> GenSources(int n)
        {
            uint[] ret = new uint[n];
            GCHandle hnd = GCHandle.Alloc(ret, GCHandleType.Pinned);
            alGenSources(n, (uint*)Marshal.UnsafeAddrOfPinnedArrayElement(ret, 0));
            hnd.Free();
            return ret;
        }

        public static void Source(uint source, SourceFloatProperty prop, float val) => alSourcef(source, (uint)prop, val);
        public static void Source(uint source, SourceFloatVectorProperty prop, Vector3 val) => alSourcefv(source, (uint)prop, &val.X);
        public static void Source(uint source, SourceBoolProperty prop, bool val) => alSourcei(source, (uint)prop, val ? 1 : 0);
        public static void SourceState(uint source, SourceState state) => alSourcei(source, 0x1010, (int)state);
        public static void SourceBuffer(uint source, uint buffer) => alSourcei(source, 0x1009, (int)buffer);
        public static void BufferData(uint buffer, SoundDataFormat format, IntPtr data, int size, int freq) => alBufferData(buffer, (uint)format, (void*)data, size, freq);
        public static ALError GetError() => (ALError)alGetError();

        public static void DeleteBuffer(uint buffer) => alDeleteBuffers(1, &buffer);

        public static void DeleteBuffers(Span<uint> buffers)
        {
            fixed (uint* buffersPtr = buffers)
            {
                alDeleteBuffers(buffers.Length, buffersPtr);
            }
        }

        public static void SourcePlay(uint src) => alSourcePlay(src);
        public static void SourcePause(uint src) => alSourcePause(src);
        public static void SourceStop(uint src) => alSourceStop(src);
        public static bool IsSource(uint src) => alIsSource(src);
        public static SourceState GetSourceState(uint src)
        {
            int i = 0;
            alGetSourcei(src, 0x1010, &i);
            return (SourceState)i;
        }

        public static void DeleteSource(uint buffer) => alDeleteSources(1, &buffer);

        public static void DeleteSources(Span<uint> buffers)
        {
            fixed (uint* buffersPtr = buffers)
            {
                alDeleteSources(buffers.Length, buffersPtr);
            }
        }

        public static float GetSource(uint src, SourceFloatProperty prop)
        {
            float f = 0;
            alGetSourcef(src, (uint)prop, &f);
            return f;
        }

        public static void SourceQueueBuffer(uint src, uint buffer) => alSourceQueueBuffers(src, 1, &buffer);
        public static void SourceQueueBuffers(uint src, Span<uint> buffers)
        {
            fixed (uint* buffersPtr = buffers)
            {
                alSourceQueueBuffers(src, buffers.Length, buffersPtr);
            }
        }

        public static int GetSourceBuffersProcessed(uint src)
        {
            int i = 0;
            alGetSourcei(src, 0x1016, &i);
            return i;
        }

        public static uint SourceUnqueueBuffer(uint src)
        {
            uint i = 0;
            alSourceUnqueueBuffers(src, 1, &i);
            return i;
        }

        public static void SourceUnqueueBuffers(uint src, Span<uint> buffers)
        {
            fixed (uint* buffersPtr = buffers)
            {
                alSourceUnqueueBuffers(src, buffers.Length, buffersPtr);
            }
        }
    }
}
