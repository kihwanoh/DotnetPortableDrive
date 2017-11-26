using System;
using System.Collections.Generic;

namespace external_drive_lib
{
    public class DeviceChangedEventArgs : EventArgs
    {
        public Dictionary<string, string> AffectedDevices { get; internal set; }
    }
}
