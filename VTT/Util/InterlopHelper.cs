namespace VTT.Util
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Runtime.InteropServices;

    public static class InterlopHelper
    {
        public const string GLFWLibName = "glfw";
        public const string OpenALLibName = "openal";

        static InterlopHelper() => NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), LibImportResolver);

        // https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.dllimportresolver?view=net-9.0
        // While it clearly isn't necessary to cache the library loaded, MS themselves remark that

        // '' The resolver is typically called once for each PInvoke entry point.
        // To improve performance, the implementation of the resolver can cache the libraryName
        // to handle mapping, as long as the library isn't unloaded via Free(IntPtr). ''
        // So while it doesn't seem right, and there seems to be internal caching going in NativeLibrary.Load
        // This is what MS recommends
        private static readonly Dictionary<string, IntPtr> libPtrs = new Dictionary<string, IntPtr>();

        private static IntPtr LibImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            return libraryName switch
            {
                GLFWLibName => GLFWLibResolver(libraryName, assembly, searchPath),
                OpenALLibName => OpenALLibResolver(libraryName, assembly, searchPath),
                _ => IntPtr.Zero
            };
        }

        // We also use Load instead of TryLoad bc if loading fails there is no fallback anyway

        private static IntPtr OpenALLibResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            return libPtrs.TryGetValue(libraryName, out IntPtr val)
                ? val
                : (libPtrs[libraryName] = NativeLibrary.Load(Environment.OSVersion.Platform switch
            {
                PlatformID.Win32NT => "openal32",
                PlatformID.Win32S => "openal32",
                PlatformID.Win32Windows => "openal32",
                PlatformID.WinCE => "openal32",
                PlatformID.Unix => "libopenal.so.1",
                PlatformID.MacOSX => "/System/Library/Frameworks/OpenAL.framework/OpenAL",
                _ => "libopenal"
            }, assembly, searchPath));
        }

        private static IntPtr GLFWLibResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            return libPtrs.TryGetValue(libraryName, out IntPtr val)
                ? val
                : (libPtrs[libraryName] = NativeLibrary.Load(Environment.OSVersion.Platform switch
            {
                PlatformID.Win32NT => "glfw3",
                PlatformID.Win32S => "glfw3",
                PlatformID.Win32Windows => "glfw3",
                PlatformID.WinCE => "glfw3",
                PlatformID.Unix => "libglfw.so.3",
                PlatformID.MacOSX => "libglfw.3.dylib",
                _ => "libglfw"
            }, assembly, searchPath));
        }
    }
}
