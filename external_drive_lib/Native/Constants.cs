using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace external_drive_lib.Native
{
    internal static class Constants
    {
        public const string TempFolderName = "ExternalDriveLibTemp";
        public static readonly List<string> BytesSuffix = new List<string> { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB

        public const string AndroidDeviceName = "android";
        public const string AppleDeviceName = "apple";
        public const string IphoneDeviceName = " iphone";
        public const string PhoneDeviceName = "Phone";
        public const string TabletDeviceName = "Tablet";

        public const int MAX_REUSE_THREADS = 20;
        public const int RETRY_TIMES = 5;
        public const int SLEEP_BEFORE_RETRY_MS = 200;

        // https://msdn.microsoft.com/en-us/library/windows/desktop/ms633574(v=vs.85).aspx
        public const string WINDOW_DIALOG_CLASS_NAME = "#32770";
        public const string WINDOW_DIRECT_UI_NAME = "DirectUIHWND";
        public const string WINDOW_PROGRESSBAR_NAME = "msctls_progress32";
    }
}
