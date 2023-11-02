namespace VTT.Util
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    public static class W32MinidumpInterlop
    {
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct MINIDUMP_EXCEPTION_INFORMATION
        {
            public uint ThreadId;
            public IntPtr ExceptionPointers;
            public int ClientPointers;
        }

        [DllImport("Dbghelp.dll")]
        static extern bool MiniDumpWriteDump(IntPtr hProcess, uint ProcessId, IntPtr hFile, int DumpType, ref MINIDUMP_EXCEPTION_INFORMATION ExceptionParam, IntPtr UserStreamParam, IntPtr CallbackParam);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentProcessId();

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        public static void GenerateMiniDump(string file)
        {
            try
            {
                MINIDUMP_EXCEPTION_INFORMATION mei = new MINIDUMP_EXCEPTION_INFORMATION();
                mei.ClientPointers = 1;
                mei.ExceptionPointers = Marshal.GetExceptionPointers();
                mei.ThreadId = GetCurrentThreadId();
                using FileStream fs = File.OpenWrite(file);
                MiniDumpWriteDump(GetCurrentProcess(), GetCurrentProcessId(), fs.SafeFileHandle.DangerousGetHandle(), 2, ref mei, IntPtr.Zero, IntPtr.Zero);
            }
            catch (DllNotFoundException)
            {
                // NOOP - P/Invoke failed
            }
        }
    }
}
