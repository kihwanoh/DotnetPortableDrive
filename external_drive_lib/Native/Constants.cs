using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace external_drive_lib.Native
{
    internal static class Constants
    {
        public const int MAX_REUSE_THREADS = 20;
        public const int RETRY_TIMES = 5;
        public const int SLEEP_BEFORE_RETRY_MS = 200;

        // https://msdn.microsoft.com/en-us/library/windows/desktop/ms633574(v=vs.85).aspx
        public const string WINDOW_DIALOG_CLASS_NAME = "#32770";
        public const string WINDOW_DIRECT_UI_NAME = "DirectUIHWND";
        public const string WINDOW_PROGRESSBAR_NAME = "msctls_progress32";
    }
}
