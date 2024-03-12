namespace VTT.GLFW
{
    using System;
    using System.Runtime.InteropServices;
    using static GLFWLoader;

    public static class Glfw
    {
        public const int DontCare = -1;

        public static bool Init()
        {
            return glfwInit() > 0;
        }

        public static void WindowHint(WindowCreationHint hint, int value) => glfwWindowHint((int)hint, value);
        public static void WindowHint(WindowCreationHint hint, bool value) => glfwWindowHint((int)hint, value ? 1 : 0);
        public static void WindowHint(WindowCreationHint hint, ClientAPI value) => glfwWindowHint((int)hint, (int)value);
        public static void WindowHint(WindowCreationHint hint, ContextCreationAPI value) => glfwWindowHint((int)hint, (int)value);
        public static void WindowHint(WindowCreationHint hint, OpenGLProfile value) => glfwWindowHint((int)hint, (int)value);
        public static IntPtr GetPrimaryMonitor() => glfwGetPrimaryMonitor();
        public static unsafe GLFWvidmode* GetVideoMode(IntPtr monitor) => glfwGetVideoMode(monitor);

        public static unsafe IntPtr CreateWindow(int w, int h, string title, IntPtr monitor, IntPtr share)
        {
            byte* titleB = (byte*)Marshal.StringToCoTaskMemUTF8(title);
            IntPtr ret = glfwCreateWindow(w, h, titleB, monitor, share);
            Marshal.ZeroFreeCoTaskMemUTF8((IntPtr)titleB);
            return ret;
        }

        public static void Terminate()
        {
            glfwTerminate();
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CursorPositionCallback(IntPtr window, double x, double y);
        private static CursorPositionCallback CursorPositionCallbackO;

        public static void SetCursorPosCallback(IntPtr window, CursorPositionCallback cb)
        {
            CursorPositionCallbackO = cb;
            glfwSetCursorPosCallback(window, Marshal.GetFunctionPointerForDelegate(CursorPositionCallbackO));
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FramebufferSizeCallback(IntPtr window, int x, int y);
        private static FramebufferSizeCallback FramebufferSizeCallbackO;

        public static void SetFramebufferSizeCallback(IntPtr window, FramebufferSizeCallback cb)
        {
            FramebufferSizeCallbackO = cb;
            glfwSetFramebufferSizeCallback(window, Marshal.GetFunctionPointerForDelegate(FramebufferSizeCallbackO));
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void WindowSizeCallback(IntPtr window, int x, int y);
        private static WindowSizeCallback WindowSizeCallbackO;

        public static void SetWindowSizeCallback(IntPtr window, WindowSizeCallback cb)
        {
            WindowSizeCallbackO = cb;
            glfwSetWindowSizeCallback(window, Marshal.GetFunctionPointerForDelegate(WindowSizeCallbackO));
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CharacterCallback(IntPtr window, uint codepoint);
        private static CharacterCallback CharacterCallbackO;

        public static void SetCharCallback(IntPtr window, CharacterCallback cb)
        {
            CharacterCallbackO = cb;
            glfwSetCharCallback(window, Marshal.GetFunctionPointerForDelegate(CharacterCallbackO));
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void KeyCallback(IntPtr window, Keys key, int scancode, InputState action, ModifierKeys mods);
        private static KeyCallback KeyCallbackO;

        public static void SetKeyCallback(IntPtr window, KeyCallback cb)
        {
            KeyCallbackO = cb;
            glfwSetKeyCallback(window, Marshal.GetFunctionPointerForDelegate(KeyCallbackO));
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ScrollCallback(IntPtr window, double x, double y);
        private static ScrollCallback ScrollCallbackO;

        public static void SetScrollCallback(IntPtr window, ScrollCallback cb)
        {
            ScrollCallbackO = cb;
            glfwSetScrollCallback(window, Marshal.GetFunctionPointerForDelegate(ScrollCallbackO));
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate void DropCallback(IntPtr window, int count, byte** paths);
        private static DropCallback DropCallbackO;

        public static void SetDropCallback(IntPtr window, DropCallback cb)
        {
            DropCallbackO = cb;
            glfwSetDropCallback(window, Marshal.GetFunctionPointerForDelegate(DropCallbackO));
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate void FocusCallback(IntPtr window, int focused);
        private static FocusCallback FocusCallbackO;

        public static void SetWindowFocusCallback(IntPtr window, FocusCallback cb)
        {
            FocusCallbackO = cb;
            glfwSetWindowFocusCallback(window, Marshal.GetFunctionPointerForDelegate(FocusCallbackO));
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate void MouseButtonCallback(IntPtr window, MouseButton button, InputState action, ModifierKeys mods);
        private static MouseButtonCallback MouseButtonCallbackO;

        public static void SetMouseButtonCallback(IntPtr window, MouseButtonCallback cb)
        {
            MouseButtonCallbackO = cb;
            glfwSetMouseButtonCallback(window, Marshal.GetFunctionPointerForDelegate(MouseButtonCallbackO));
        }

        public static void MakeContextCurrent(IntPtr window) => glfwMakeContextCurrent(window);
        public static unsafe bool ExtensionSupported(string name)
        {
            byte* ptr = (byte*)Marshal.StringToHGlobalAnsi(name);
            int i = glfwExtensionSupported(ptr);
            Marshal.FreeHGlobal((IntPtr)ptr);
            return i != 0;
        }

        public static IntPtr GetWindowMonitor(IntPtr window) => glfwGetWindowMonitor(window);
        public static void RequestWindowAttention(IntPtr window) => glfwRequestWindowAttention(window);
        public static void SwapBuffers(IntPtr window) => glfwSwapBuffers(window);
        public static InputState GetKey(IntPtr window, Keys key) => (InputState)glfwGetKey(window, (int)key);
        public static InputState GetMouseButton(IntPtr window, MouseButton button) => (InputState)glfwGetMouseButton(window, (int)button);
        public static void SetWindowShouldClose(IntPtr window, bool val) => glfwSetWindowShouldClose(window, val ? 1 : 0);
        public static bool WindowShouldClose(IntPtr window) => glfwWindowShouldClose(window) != 0;
        public static void SetCursorPos(IntPtr window, double x, double y) => glfwSetCursorPos(window, x, y);
        public static unsafe void SetWindowTitle(IntPtr window, string title)
        {
            byte* d = (byte*)Marshal.StringToCoTaskMemUTF8(title);
            glfwSetWindowTitle(window, d);
            Marshal.ZeroFreeCoTaskMemUTF8((IntPtr)d);
        }

        public static void SetWindowSize(IntPtr window, int w, int y) => glfwSetWindowSize(window, w, y);
        public static void SwapInterval(int interval) => glfwSwapInterval(interval);
        public static void SetWindowAttrib(IntPtr win, WindowProperty attrib, int value) => glfwSetWindowAttrib(win, (int)attrib, value);
        public static void SetWindowAttrib(IntPtr win, WindowProperty attrib, bool value) => glfwSetWindowAttrib(win, (int)attrib, value ? 1 : 0);
        public static void SetWindowIcon(IntPtr win, GLFWimage[] images)
        {
            unsafe
            {
                GCHandle hnd = GCHandle.Alloc(images, GCHandleType.Pinned);
                glfwSetWindowIcon(win, images.Length, (GLFWimage*)Marshal.UnsafeAddrOfPinnedArrayElement(images, 0));
                hnd.Free();
            }
        }

        public static unsafe void GetFramebufferSize(IntPtr window, out int x, out int y)
        {
            int i = 0;
            int j = 0;
            glfwGetFramebufferSize(window, &i, &j);
            x = i;
            y = j;
        }

        public static unsafe void GetWindowSize(IntPtr window, out int x, out int y)
        {
            int i = 0;
            int j = 0;
            glfwGetWindowSize(window, &i, &j);
            x = i;
            y = j;
        }

        public static void PollEvents() => glfwPollEvents();
        public static void DestroyWindow(IntPtr window) => glfwDestroyWindow(window);
        public static void HideWindow(IntPtr window) => glfwHideWindow(window);
        public static void ShowWindow(IntPtr window) => glfwShowWindow(window);
        public static void SetWindowMonitor(IntPtr window, IntPtr monitor, int x, int y, int w, int h, int rr) => glfwSetWindowMonitor(window, monitor, x, y, w, h, rr);
        public static unsafe void GetWindowPos(IntPtr window, out int x, out int y)
        {
            int i = 0;
            int j = 0;
            glfwGetWindowPos(window, &i, &j);
            x = i;
            y = j;
        }

        public static double GetTime() => glfwGetTime();
    }
}
