using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace external_drive_lib.Native
{
    internal static class Win32Windows
    {
        public static List<int> GetCurrentProcessThreadIds()
        {
            try
            {
                var threadIds = new List<int>();
                var process = Process.GetCurrentProcess();
                foreach (ProcessThread t in process.Threads)
                {
                    threadIds.Add(t.Id);
                }
                return threadIds;
            }
            catch
            {
                return null;
            }
        }

        public static IReadOnlyList<IntPtr> GetAllTopWindows()
        {
            var toplevelWindows = new List<IntPtr>();
            var callback = new EnumThreadWndProc((hwnd, lparam) =>
            {
                toplevelWindows.Add(hwnd);
                return true;
            });
            foreach (var t in GetCurrentProcessThreadIds())
            {
                NativeMethods.EnumThreadWindows((uint) t, callback, IntPtr.Zero);
            }
            return new ReadOnlyCollection<IntPtr>(toplevelWindows);
        }

        public static IReadOnlyList<IntPtr> GetChildWindows(IntPtr w)
        {
            var childWindows = new List<IntPtr>();
            var callback = new EnumThreadWndProc((hwnd, lparam) =>
            {
                childWindows.Add(hwnd);
                return true;
            });
            NativeMethods.EnumChildWindows(w, callback, IntPtr.Zero);
            return new ReadOnlyCollection<IntPtr>(childWindows);
        }

        public static string GetWindowClassName(IntPtr hWnd)
        {
            var name = new StringBuilder(32);
            var result = NativeMethods.GetClassName(hWnd, name, name.Capacity);
            return result != 0 ? name.ToString() : null;
        }
    }
}
