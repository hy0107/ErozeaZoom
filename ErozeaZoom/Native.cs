using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ErozeaZoom
{
    class Platform
    {
        private const Int32 PROCESS_QUERY_INFORMATION = 0x0400;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr OpenProcess(int dwDesiredAccess,
            [MarshalAs(UnmanagedType.Bool)]bool bInheritHandle,
            int dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

        public static int isProcessX64 (int processId)
        {
            IntPtr hProcess = OpenProcess(
                PROCESS_QUERY_INFORMATION, false, processId);
            if (hProcess != IntPtr.Zero)
            {
                int is64bitProc = -1;
                try
                {
                    // Detect whether the specified process is a 64-bit. 
                    is64bitProc = Is64BitProcess(hProcess) ? 1 : 0;
                }
                finally
                {
                    CloseHandle(hProcess);
                }
                return is64bitProc;
            }
            else
            {
                int errorCode = Marshal.GetLastWin32Error();
                return errorCode;
            }
        }

        private static bool Is64BitProcess(IntPtr hProcess)
        {
            bool flag = false;

            if (Environment.Is64BitOperatingSystem)
            {
                // On 64-bit OS, if a process is not running under Wow64 mode,  
                // the process must be a 64-bit process. 
                flag = !(IsWow64Process(hProcess, out flag) && flag);
            }

            return flag;
        }
    }
}
