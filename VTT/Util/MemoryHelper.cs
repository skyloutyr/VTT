namespace VTT.Util
{
    using System.Runtime.InteropServices;
    using System.Text;

    // Class exists in case of a bad platform-specific implementation of NativeMemory methods
    public static unsafe class MemoryHelper
    {
        public static T* Allocate<T>(nuint nElements) where T : unmanaged => (T*)NativeMemory.Alloc(nElements, (nuint)sizeof(T));
        public static T* AllocateZeroed<T>(nuint nElements) where T : unmanaged => (T*)NativeMemory.AllocZeroed(nElements, (nuint)sizeof(T));
        public static T* Reallocate<T>(T* oldPtr, nuint nElements) where T : unmanaged => (T*)NativeMemory.Realloc(oldPtr, nElements * (nuint)sizeof(T));
        public static void Free(void* ptr)
        {
            if (ptr != null)
            {
                NativeMemory.Free(ptr);
            }
        }

        public static void* AllocateBytes(nuint nBytes) => Allocate<byte>(nBytes);
        public static void* AllocateBytesZeroed(nuint nBytes) => AllocateZeroed<byte>(nBytes);
        public static void* ReallocateBytes(void* oldPtr, nuint nBytes) => Reallocate((byte*)oldPtr, nBytes);
        public static void* StringToPointerAnsi(in string s, out int strlen)
        {
            // VTT is designed for GL 3, which by spec doesn't support UTF-8 encoded shader data
            // And this method is only used for shader source setters.
            byte[] arr = Encoding.ASCII.GetBytes(s);
            strlen = arr.Length;
            byte* ptr = Allocate<byte>((nuint)(arr.Length + 1));
            for (int i = 0; i < strlen; ++i)
            {
                ptr[i] = arr[i];
            }

            ptr[strlen] = 0;
            return ptr;
        }
    }
}
