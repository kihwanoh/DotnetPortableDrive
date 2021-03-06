﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace external_drive_lib.Native
{
    internal sealed class MoveDialogThread
    {
        private readonly Thread _workThread;
        private bool _isWorking, _isUsed;

        public MoveDialogThread()
        {
            _workThread = new Thread(CheckForDialogsThread) {IsBackground = true};
        }

        public void Start()
        {
            if (_isUsed)
            {
                throw new InvalidOperationException("Already started/stopped watcher canoot be restarted.");
            }
            _isWorking = true;
            _workThread.Start();
        }

        public void Stop()
        {
            _isUsed = true;
            _isWorking = false;
        }

        private void CheckForDialogsThread()
        {
            var processedWindows = new HashSet<IntPtr>();
            while (_isWorking)
            {
                var checkSleepMs = ExternalDriveRoot.Instance.AutoCloseWinDialogs ? 50 : 500;
                Thread.Sleep(checkSleepMs);

                if (!ExternalDriveRoot.Instance.AutoCloseWinDialogs) continue;
                foreach (var w in Win32Windows.GetAllTopWindows())
                {
                    // already processed window handle
                    if (processedWindows.Contains(w)) continue;

                    // window is not dialog
                    processedWindows.Add(w);
                    if (Win32Windows.GetWindowClassName(w) != Constants.WINDOW_DIALOG_CLASS_NAME) continue;

                    // check if the dialog has ProgressBar
                    var children = Win32Windows.GetChildWindows(w);
                    var classNames = children.Select(Win32Windows.GetWindowClassName).ToList();
                    if (classNames.Any(c => c == Constants.WINDOW_DIRECT_UI_NAME) && classNames.Any(c => c == Constants.WINDOW_PROGRESSBAR_NAME))
                    {
                        // found a shell copy/move/delete window this would minimize it
                        //    ShowWindow(w, 6);
                        // hiding the window doesn't work - so, just move it outside the screen
                        NativeMethods.MoveWindow(w, -100000, 10, 600, 300, false);
                    }
                }
            }
        }
    }
}
