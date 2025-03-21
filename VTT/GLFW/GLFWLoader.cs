﻿namespace VTT.GLFW
{
    using System;
    using System.Runtime.InteropServices;

    public static unsafe class GLFWLoader
    {
        const string Lib = "glfw3";

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int glfwInit();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwWindowHint(int hint, int value);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr glfwGetPrimaryMonitor();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern GLFWvidmode* glfwGetVideoMode(IntPtr monitor);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr glfwCreateWindow(int width, int height, byte* title, IntPtr monitor, IntPtr share);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwTerminate();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetCursorPosCallback(IntPtr window, IntPtr callback);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetFramebufferSizeCallback(IntPtr window, IntPtr callback);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetWindowSizeCallback(IntPtr window, IntPtr callback);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetCharCallback(IntPtr window, IntPtr callback);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetKeyCallback(IntPtr window, IntPtr callback);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetScrollCallback(IntPtr window, IntPtr callback);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetDropCallback(IntPtr window, IntPtr callback);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetWindowFocusCallback(IntPtr window, IntPtr callback);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetMouseButtonCallback(IntPtr window, IntPtr callback);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwMakeContextCurrent(IntPtr window);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr glfwGetCurrentContext();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "LPStr is the correct type, LPWStr would be UTF-16, LPStr is ANSII 8 bytes, which is the correct expected type.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "Needs to be visible for any APIs that want this delegate explicitly")]
        public static extern IntPtr glfwGetProcAddress([In][MarshalAs(UnmanagedType.LPStr)]string procName);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int glfwExtensionSupported(byte* extension);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr glfwGetWindowMonitor(IntPtr window);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwRequestWindowAttention(IntPtr window);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSwapBuffers(IntPtr window);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int glfwGetKey(IntPtr window, int key);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int glfwGetMouseButton(IntPtr window, int button);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetWindowShouldClose(IntPtr window, int value);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int glfwWindowShouldClose(IntPtr window);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetCursorPos(IntPtr window, double x, double y);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetWindowTitle(IntPtr window, byte* title);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetWindowSize(IntPtr window, int x, int y);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSwapInterval(int interval);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetWindowAttrib(IntPtr window, int attrib, int value);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetWindowIcon(IntPtr window, int count, GLFWimage* images);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwGetFramebufferSize(IntPtr window, int* width, int* height);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwGetWindowSize(IntPtr window, int* width, int* height);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwPollEvents();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwDestroyWindow(IntPtr window);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwHideWindow(IntPtr window);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwShowWindow(IntPtr window);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwSetWindowMonitor(IntPtr window, IntPtr monitor, int x, int y, int w, int h, int refresh);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwGetWindowPos(IntPtr window, int* width, int* height);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern double glfwGetTime();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void glfwGetCursorPos(IntPtr window, double* x, double* y);
    }
}
