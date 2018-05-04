using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LagfreeAgent
{
	internal static class Win32Utils
    {

        private static class NativeMethods
        {
            public delegate bool EnumWindowsProc(IntPtr hwnd, ref object lParam);
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, ref object lParam);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsWindowVisible(IntPtr hwnd);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern int GetWindowThreadProcessId(IntPtr hwnd, out int lpdwProcessId);
        }


        public static HashSet<int> GetVisiblePids()
        {
            HashSet<int> pids = new HashSet<int>();
            object pidsObj = pids;
            if (!NativeMethods.EnumWindows(EnumWindowsCallback, ref pidsObj))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            pids = (HashSet<int>)pidsObj;
            return pids;
        }

        private static bool EnumWindowsCallback(IntPtr hwnd, ref object lParam)
        {
            HashSet<int> pids = (HashSet<int>)lParam;
            if (NativeMethods.IsWindowVisible(hwnd))
            {
                NativeMethods.GetWindowThreadProcessId(hwnd, out int pid);
                pids.Add(pid);
            }
            return true;
        }
    }
}
