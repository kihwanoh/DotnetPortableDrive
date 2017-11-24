using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace external_drive_lib.Native
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadWndProc lpfn, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr hwndParent, EnumThreadWndProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32")]
        public static extern int ShowWindow(IntPtr hwnd, int nCmdShow);

        [DllImport("user32")]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        public const int MAX_REUSE_THREADS = 20;
        public const int RETRY_TIMES = 5;
        public const int SLEEP_BEFORE_RETRY_MS = 200;

        // https://msdn.microsoft.com/en-us/library/windows/desktop/ms633574(v=vs.85).aspx
        public const string WINDOW_DIALOG_CLASS_NAME = "#32770";
        public const string WINDOW_DIRECT_UI_NAME = "DirectUIHWND";
        public const string WINDOW_PROGRESSBAR_NAME = "msctls_progress32";
    }
}
