using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ErozeaZoom
{
    public static class Memory
    {
        private const ProcessAccessFlags ProcessFlags =
            ProcessAccessFlags.VirtualMemoryRead
            | ProcessAccessFlags.VirtualMemoryWrite
            | ProcessAccessFlags.VirtualMemoryOperation
            | ProcessAccessFlags.QueryInformation;

        public static IEnumerable<int> GetPids()
        {
            foreach (var p in Process.GetProcesses())
            {
                if (string.Equals(p.ProcessName, "ffxiv", StringComparison.Ordinal)
                    || string.Equals(p.ProcessName, "ffxiv_dx11", StringComparison.Ordinal))
                {
                    yield return p.Id;
                }
                p.Dispose();
            }
        }

        private static bool VersionVerify(Settings settings, int pid)
        {
            using (var p = Process.GetProcessById(pid))
            {
                var dir = Path.GetDirectoryName(p.MainModule.FileName);
                var verinfo = Path.Combine(dir, "ffxivgame.ver");
                var ver = File.ReadAllText(verinfo);
                return ver == settings.Version;
            }
        }

        public static void Kernel(Settings settings, int pid)
        {
            if (string.Equals(settings.LastUpdate, "unupdated", StringComparison.Ordinal))
            {
                MessageBox.Show("资源文件尚未下载，请检查更新");
                return;
            }

            if (!VersionVerify(settings, pid))
            {
                MessageBox.Show("当前游戏版本不是该程序支持的版本，请检查更新或联系作者");
                return;
            }

            //var isThis64 = IntPtr.Size == 8;
            //var isFFXIV64 = Platform.isProcessX64(pid) == 1;
            //if (isThis64 != isFFXIV64)
            //{
            //    var Message = isThis64 ? ("本程序设计运行于64位系统\n无法为您的FF14客户端(PID=" + pid + ")提供服务") : ("本程序设计运行于32位系统\n无法为您的FF14客户端(PID=" + pid + ")提供服务");
            //    MessageBox.Show(Message);
            //    return;
            //}

            Apply(settings, pid);
        }

        private static void Apply(Settings settings, int pid)
        {
            using (var p = Process.GetProcessById(pid))
            {
                var ptr = IntPtr.Zero;
                try
                {
                    ptr = OpenProcess(ProcessFlags, false, pid);
                    if (string.Equals(p.ProcessName, "ffxiv", StringComparison.Ordinal))
                    {
                        ApplyX86(settings, p, ptr);
                    }
                    else if (string.Equals(p.ProcessName, "ffxiv_dx11", StringComparison.Ordinal))
                    {
                        ApplyX64(settings, p, ptr);
                    }
                }
                finally
                {
                    if (ptr != IntPtr.Zero)
                    {
                        CloseHandle(ptr);
                    }
                }
            }
        }

        private static void ApplyX86(Settings settings, Process process, IntPtr ptr)
        {
            var addr = GetAddress(4, process, ptr, settings.DX9_StructureAddress, settings.DX9_ZoomMax);
            Write(settings.DesiredZoom, ptr, addr);
            addr = GetAddress(4, process, ptr, settings.DX9_StructureAddress, settings.DX9_ZoomCurrent);
            Write(settings.DesiredZoom, ptr, addr);

            addr = GetAddress(4, process, ptr, settings.DX9_StructureAddress, settings.DX9_FovCurrent);
            Write(settings.DesiredFov, ptr, addr);
            addr = GetAddress(4, process, ptr, settings.DX9_StructureAddress, settings.DX9_FovMax);
            Write(settings.DesiredFov, ptr, addr);
        }

        private static void ApplyX64(Settings settings, Process process, IntPtr ptr)
        {
            var addr = GetAddress(8, process, ptr, settings.DX11_StructureAddress, settings.DX11_ZoomMax);
            Write(settings.DesiredZoom, ptr, addr);
            addr = GetAddress(8, process, ptr, settings.DX11_StructureAddress, settings.DX11_ZoomCurrent);
            Write(settings.DesiredZoom, ptr, addr);

            addr = GetAddress(8, process, ptr, settings.DX11_StructureAddress, settings.DX11_FovCurrent);
            Write(settings.DesiredFov, ptr, addr);
            addr = GetAddress(8, process, ptr, settings.DX11_StructureAddress, settings.DX11_FovMax);
            Write(settings.DesiredFov, ptr, addr);
        }

        private static void Write(float value, IntPtr handle, IntPtr address)
        {
            var buffer = BitConverter.GetBytes(value);
            IntPtr written;
            if (!(WriteProcessMemory(handle, address, buffer, buffer.Length, out written)))
            {
                throw new Exception("Could not write process memory: " + Marshal.GetLastWin32Error());
            }
        }

        private static IntPtr GetAddress(byte size, Process process, IntPtr ptr, IEnumerable<int> offsets, int finalOffset)
        {
            var addr = process.MainModule.BaseAddress;
            var buffer = new byte[size];
            foreach (var offset in offsets)
            {
                IntPtr read;
                if (!(ReadProcessMemory(ptr, IntPtr.Add(addr, offset), buffer, buffer.Length, out read)))
                {
                    throw new Exception("Unable to read process memory");
                }
                addr = (size == 8)
                    ? new IntPtr(BitConverter.ToInt64(buffer, 0))
                    : new IntPtr(BitConverter.ToInt32(buffer, 0));
            }
            return IntPtr.Add(addr, finalOffset);
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [Flags]
        private enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }
    }
}