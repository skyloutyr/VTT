namespace VTT.Sound.Bindings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    public static unsafe class ALC
    {
        public static List<string> GetDeviceSpecifier()
        {
            byte* text = ALLoader.alcGetString(null, 0x1005);
            List<string> ret = new List<string>();
            byte* cPos = text;
            while (true)
            {
                string s = Marshal.PtrToStringAnsi((IntPtr)cPos);
                if (string.IsNullOrEmpty(s)) // Ends with 00, will return empty if we reach there
                {
                    break;
                }

                ret.Add(s);
                cPos += s.Length + 1; // null terminator
            }

            return ret;
        }

        public static string GetString(IntPtr device, ALCString param)
        {
            byte* ptr = ALLoader.alcGetString((void*)device, (uint)param);
            return Marshal.PtrToStringAnsi((IntPtr)ptr);
        }

        public static IntPtr OpenDevice(string name)
        {
            byte* ptr = (byte*)Marshal.StringToHGlobalAnsi(name);
            IntPtr ret = (IntPtr)ALLoader.alcOpenDevice(ptr);
            Marshal.FreeHGlobal((IntPtr)ptr);
            return ret;
        }

        public static IntPtr CreateContext(IntPtr device) => (IntPtr)ALLoader.alcCreateContext((void*)device, null);
        public static bool MakeContextCurrent(IntPtr ctx) => ALLoader.alcMakeContextCurrent((void*)ctx);
        public static void DestroyContext(IntPtr ctx) => ALLoader.alcDestroyContext((void*)ctx);
        public static void CloseDevice(IntPtr device) => ALLoader.alcCloseDevice((void*)device);

        public static Version GetVersion(IntPtr device)
        {
            int minor = 0;
            int major = 0;
            ALLoader.alcGetIntegerv((void*)device, 0x1000, 1, &major);
            ALLoader.alcGetIntegerv((void*)device, 0x1001, 1, &minor);
            return new Version(major, minor);
        }
    }

    public enum ALCString
    {
        DefaultDeviceSpecifier = 0x1004,
        CaptureDefaultDeviceSpecifier = 0x311,
        CaptureDeviceSpecifier = 0x310
    }
}
