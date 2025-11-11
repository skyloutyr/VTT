namespace VTT.Sound.Bindings
{
    using System.Runtime.InteropServices;
    using VTT.Util;

    public static unsafe class ALLoader
    {
        const string Lib = InterlopHelper.OpenALLibName;

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alGenBuffers(int n, uint* buffers);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alGenSources(int n, uint* sources);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alSourcei(uint sid, uint param, int value);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alSourcef(uint sid, uint param, float value);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alSource3f(uint sid, uint param, float x, float y, float z);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alSourcefv(uint sid, uint param, float* vals);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alSource3i(uint sid, uint param, int x, int y, int z);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alSourceiv(uint sid, uint param, int* vals);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alBufferData(uint buffer, uint format, void* data, int size, int freq);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint alGetError();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alDeleteBuffers(int n, uint* buffers);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alSourcePlay(uint src);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alSourcePause(uint src); 
        
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alSourceStop(uint src);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool alIsSource(uint src);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool alGetSourcei(uint src, uint pname, int* value);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alDeleteSources(int n, uint* buffers);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool alGetSourcef(uint src, uint pname, float* value);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool alSourceQueueBuffers(uint src, int n, uint* buffers);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool alSourceUnqueueBuffers(uint src, int n, uint* buffers);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte* alcGetString(void* device, uint param);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void* alcOpenDevice(byte* devicename);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void* alcCreateContext(void* device, int* attrlist);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool alcMakeContextCurrent(void* context);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void* alcGetCurrentContext();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alcDestroyContext(void* context);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alcCloseDevice(void* device);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alcGetIntegerv(void* device, uint param, int size, int* data);
    }
}
