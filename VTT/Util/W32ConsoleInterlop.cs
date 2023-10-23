namespace VTT.Util
{
    using System;
    using System.Runtime.InteropServices;

    public static class W32ConsoleInterlop
    {
        const string KernelName = "kernel32.dll";
        const string User32Name = "user32.dll";

        [DllImport(KernelName)]
        private static extern IntPtr GetConsoleWindow();

        [DllImport(User32Name)]
        private static extern bool ShowWindow(IntPtr hWin, int nCmdShow);


        [DllImport(User32Name)]
        private static extern bool SetForegroundWindow(IntPtr hWin);

        [DllImport(User32Name)]
        private static extern IntPtr GetForegroundWindow();

        private static IntPtr _cWin;

        public static void ShowConsole(bool show)
        {
            try
            {
                if (_cWin == IntPtr.Zero)
                {
                    _cWin = GetConsoleWindow();
                    if (_cWin != IntPtr.Zero)
                    {
                        SetForegroundWindow(_cWin);
                        _cWin = GetForegroundWindow();
                    }
                }

                if (_cWin != IntPtr.Zero)
                {
                    ShowWindow(_cWin, show ? 5 : 0);
                }
            }
            catch
            {
                // NOOP - must not have kernel32 or user32 dlls (*nix/osx)
            }
        }
    }
}
