using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using external_drive_lib.Helpers;
using external_drive_lib.Native;

namespace external_drive_lib
{
    /* the root - the one that contains all external drives 
     */
    public class ExternalDriveRoot
    {
        private static Lazy<ExternalDriveRoot> _instance = new Lazy<ExternalDriveRoot>(() => new ExternalDriveRoot());

        // note: not all devices register as USB hubs, some only register as controller devices
        private DevicesMonitor _monitorUsbhubDevices = new DevicesMonitor();
        private DevicesMonitor _monitorControllerDevices = new DevicesMonitor();
        private Dictionary<string, string> _vidpidToUniqueId = new Dictionary<string, string>();
        // this includes all drives, even the internal ones
        private List<IDrive> _drives = new List<IDrive>();

        // ----- PROPERTIES
        public static ExternalDriveRoot Instance => _instance.Value;

        public bool AutoCloseWinDialogs { get; set; } = true;

        // returns all drives, even the internal HDDs - you might need this if you want to copy a file onto an external drive
        public IReadOnlyList<IDrive> Drives
        {
            get { lock (this) return _drives; }
        }

        // ----- CONSTRUCTOR
        private ExternalDriveRoot() {
            var existingDevices = WmiHelpers.FindObjects("Win32_USBHub");
            foreach (var device in existingDevices) {
                if (!device.ContainsKey("PNPDeviceID")) continue;

                var deviceId = device["PNPDeviceID"];
                string vidPid = "", uniqueId = "";
                if (!UsbHelpers.PnpDeviceIdToVidpidAndUniqueId(deviceId, ref vidPid, ref uniqueId)) continue;

                lock (this)
                {
                    if (_vidpidToUniqueId.ContainsKey(vidPid)) continue;
                    _vidpidToUniqueId.Add(vidPid, uniqueId);
                }
            }
            var existingControllerDevices = WmiHelpers.FindObjects("Win32_USBControllerDevice");
            foreach (var device in existingControllerDevices) {
                if (!device.ContainsKey("Dependent")) continue;

                var deviceId = device["Dependent"];
                string vidPid = "", uniqueId = "";
                if (!UsbHelpers.DependentToVidpidAndUniqueId(deviceId, ref vidPid, ref uniqueId)) continue;

                lock (this)
                {
                    if (!_vidpidToUniqueId.ContainsKey(vidPid)) _vidpidToUniqueId.Add(vidPid, uniqueId);
                }
            }

            Refresh();

            _monitorUsbhubDevices.DeviceAdded += DeviceAdded;
            _monitorUsbhubDevices.DeviceDeleted += DeviceRemoved;
            _monitorUsbhubDevices.MonitorChanges("Win32_USBHub");

            _monitorControllerDevices.DeviceAdded += DeviceAddedController;
            _monitorControllerDevices.DeviceDeleted += DeviceRemovedController;
            _monitorControllerDevices.MonitorChanges("Win32_USBControllerDevice");

            new MoveDialogThread().Start();
        }
        
        private void OnNewDevice(string vidPid, string uniqueId) {
            lock (this) {
                if (_vidpidToUniqueId.ContainsKey(vidPid))
                    _vidpidToUniqueId[vidPid] = uniqueId;
                else
                    _vidpidToUniqueId.Add(vidPid, uniqueId);
            }
            RefreshPortableUniqueIds();
            var alreadyADrive = false;
            lock (this) {
                var ad = _drives.FirstOrDefault(d => d.UniqueId == uniqueId) as PortableDevice;
                if (ad != null) {
                    ad.UsbConnected = true;
                    alreadyADrive = true;
                }
            }
            if (!alreadyADrive)
                WindowsHelper.Postpone(() => MonitorForDrive(vidPid, 0), 50);            
        }
        private void OnDeletedDevice(string vidPid, string uniqueId) {            
            lock (this) {
                var ad = _drives.FirstOrDefault(d => d.UniqueId == uniqueId) as PortableDevice;
                if (ad != null)
                    ad.UsbConnected = false;
            }
            Refresh();
        }

        private void DeviceAddedController(Dictionary<string, string> properties) {
            if (!properties.ContainsKey("Dependent")) return;
            var deviceId = properties["Dependent"];
            string vidPid = "", uniqueId = "";
            if (UsbHelpers.DependentToVidpidAndUniqueId(deviceId, ref vidPid, ref uniqueId)) 
                OnNewDevice(vidPid, uniqueId);
        }
        private void DeviceRemovedController(Dictionary<string, string> properties) {
            if (!properties.ContainsKey("Dependent")) return;
            var deviceId = properties["Dependent"];
            string vidPid = "", uniqueId = "";
            if (UsbHelpers.DependentToVidpidAndUniqueId(deviceId, ref vidPid, ref uniqueId)) 
                OnDeletedDevice(vidPid, uniqueId);
        }

        // here, we know the drive was connected, wait a bit until it's actually visible
        private void MonitorForDrive(string vidpid, int idx) {
            const int maxRetries = 10;
            var drivesNow = GetPortableDrives();
            var found = drivesNow.FirstOrDefault(d => ((PortableDevice) d).VidPid == vidpid);
            if (found != null) 
                Refresh();
            else if (idx < maxRetries)
                WindowsHelper.Postpone(() => MonitorForDrive(vidpid, idx + 1), 100);
            else {
                // "can't find usb connected drive " + vidpid
                //Debug.Assert(false);
            }
        }
        private void DeviceAdded(Dictionary<string, string> properties)
        {
            if (properties.ContainsKey("PNPDeviceID"))
            {
                var deviceId = properties["PNPDeviceID"];
                string vidPid = "", uniqueId = "";
                if (UsbHelpers.PnpDeviceIdToVidpidAndUniqueId(deviceId, ref vidPid, ref uniqueId))
                    OnNewDevice(vidPid, uniqueId);
            }
            else
            {
                // added usb device with no PNPDeviceID
                //Debug.Assert(false);
            }
        }
        private void DeviceRemoved(Dictionary<string, string> properties) {
            if (properties.ContainsKey("PNPDeviceID")) {
                var deviceId = properties["PNPDeviceID"];
                string vidPid = "", uniqueId = "";
                if (UsbHelpers.PnpDeviceIdToVidpidAndUniqueId(deviceId, ref vidPid, ref uniqueId)) 
                    OnDeletedDevice(vidPid, uniqueId);                
            } else {
                // deleted usb device with no PNPDeviceID
                Debug.Assert(false);
            }
        }
        
        public void Refresh() {
            List<IDrive> drivesNow = new List<IDrive>();
            try {
                drivesNow.AddRange(GetWinDrives());
            } catch (Exception e) {
                throw new IOException( "error getting win drives ", e);
            }
            try {
                drivesNow.AddRange(GetPortableDrives());
            } catch (Exception e) {
                throw new IOException("error getting android drives ", e);
            }
            lock (this) {
                _drives = drivesNow;
            }
            RefreshPortableUniqueIds();
        }
        private void RefreshPortableUniqueIds() {
            lock(this)
                foreach (PortableDevice ad in _drives.OfType<PortableDevice>())
                {
                    Debug.Assert(ad.VidPid != null);
                    if (_vidpidToUniqueId.ContainsKey(ad.VidPid))
                        ad.UniqueId = _vidpidToUniqueId[ad.VidPid];
                }
        }

        // As drive name, use any of: 
        // "{<unique_id>}:", "<drive-name>:", "[a<android-drive-index>]:", "[i<ios-index>]:", "[p<portable-index>]:", "[d<drive-index>]:"
        public IDrive TryGetDrive(string drivePrefix)
        {
            drivePrefix = drivePrefix.Replace("/", "\\");
            // case insensitive
            foreach (var d in Drives)
                if (string.Compare(d.RootName, drivePrefix, StringComparison.CurrentCultureIgnoreCase) == 0 
                    || string.Compare("{" + d.UniqueId + "}:\\", drivePrefix, StringComparison.CurrentCultureIgnoreCase) == 0)
                    return d;

            if (!drivePrefix.StartsWith("[") || !drivePrefix.EndsWith("]:\\")) return null;
            drivePrefix = drivePrefix.Substring(1, drivePrefix.Length - 4);
            if (drivePrefix.StartsWith("d", StringComparison.CurrentCultureIgnoreCase))
            {
                // d<drive-index>
                drivePrefix = drivePrefix.Substring(1);
                var idx = 0;
                if (!int.TryParse(drivePrefix, out idx)) return null;
                var all = Drives;
                if (all.Count > idx)
                    return all[idx];
            }
            else if (drivePrefix.StartsWith("a", StringComparison.CurrentCultureIgnoreCase))
            {
                drivePrefix = drivePrefix.Substring(1);
                var idx = 0;
                if (!int.TryParse(drivePrefix, out idx)) return null;
                var android = Drives.Where(d => d.DriveType.IsAndroidOperatingSystem()).ToList();
                if (android.Count > idx) return android[idx];
            }
            else if (drivePrefix.StartsWith("i", StringComparison.CurrentCultureIgnoreCase))
            {
                drivePrefix = drivePrefix.Substring(1);
                var idx = 0;
                if (!int.TryParse(drivePrefix, out idx)) return null;
                var ios = Drives.Where(d => d.DriveType.IsIosOperatingSystem()).ToList();
                if (ios.Count > idx) return ios[idx];
            }
            else if (drivePrefix.StartsWith("p", StringComparison.CurrentCultureIgnoreCase))
            {
                drivePrefix = drivePrefix.Substring(1);
                var idx = 0;
                if (!int.TryParse(drivePrefix, out idx)) return null;
                var portable = Drives.Where(d => d.DriveType.IsPortableDevice()).ToList();
                if (portable.Count > idx) return portable[idx];
            }

            return null;
        }

        // throws if drive not found
        public IDrive GetDrive(string drivePrefix) {
            // case insensitive
            var d = TryGetDrive(drivePrefix);
            if ( d == null)
                throw new IOException("invalid drive " + drivePrefix);
            return d;
        }

        private void SplitIntoDriveAndFolderPath(string path, out string drive, out string folderOrFile) {
            path = path.Replace("/", "\\");
            var endOfDrive = path.IndexOf(":\\");
            if (endOfDrive >= 0) {
                drive = path.Substring(0, endOfDrive + 2);
                folderOrFile = path.Substring(endOfDrive + 2);
            } else
                drive = folderOrFile = null;
        }

        // returns null on failure
        public IFile TryParseFile(string path) {
            // split into drive + path
            string driveStr, folderOrFile;
            SplitIntoDriveAndFolderPath(path, out driveStr, out folderOrFile);
            if (driveStr == null)
                return null;
            var drive = GetDrive(driveStr);
            return drive.TryParseFile(folderOrFile);            
        }

        // returns null on failure
        public IFolder TryParseFolder(string path) {
            string driveStr, folderOrFile;
            SplitIntoDriveAndFolderPath(path, out driveStr, out folderOrFile);
            if ( driveStr == null)
                return null;
            var drive = TryGetDrive(driveStr);
            if (drive == null)
                return null;
            return drive.TryParseFolder(folderOrFile);            
        }

        // throws if anything goes wrong
        public IFile ParseFile(string path) {
            // split into drive + path
            string driveStr, folderOrFile;
            SplitIntoDriveAndFolderPath(path, out driveStr, out folderOrFile);
            if ( driveStr == null)
                throw new IOException("invalid path " + path);
            var drive = TryGetDrive(driveStr);
            if (drive == null)
                return null;
            return drive.ParseFile(folderOrFile);
        }

        // throws if anything goes wrong
        public IFolder ParseFolder(string path) {
            string driveStr, folderOrFile;
            SplitIntoDriveAndFolderPath(path, out driveStr, out folderOrFile);
            if ( driveStr == null)
                throw new IOException("invalid path " + path);
            var drive = GetDrive(driveStr);
            return drive.ParseFolder(folderOrFile);
        }

        // creates all folders up to the given path
        public IFolder NewFolder(string path) {
            string driveStr, folderOrFile;
            SplitIntoDriveAndFolderPath(path, out driveStr, out folderOrFile);
            if ( driveStr == null)
                throw new IOException("invalid path " + path);
            var drive = GetDrive(driveStr);
            return drive.CreateFolder(folderOrFile);
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Portable


        private List<IDrive> GetPortableDrives() {
            var newDrives = PortableDeviceHelpers.GetPortableConnectedDeviceDrives().Select(d => new PortableDevice(d) as IDrive).ToList();
            List<IDrive> oldDrives = null;
            lock (this)
                oldDrives = _drives.Where(d => d is PortableDevice).ToList();

            // if we already have this drive, reuse that
            List<IDrive> result = new List<IDrive>();
            foreach (var new_ in newDrives) {
                var old = oldDrives.FirstOrDefault(od => od.RootName == new_.RootName);
                result.Add(old ?? new_);
            }
            return result;
        }

        // END OF Portable
        //////////////////////////////////////////////////////////////////////////////////////////////////////////

        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Windows

        // for now, I return all drives - don't care about which is External, Removable, whatever

        private List<IDrive> GetWinDrives() {
            return DriveInfo.GetDrives().Select(d => new WindowsDrive(d) as IDrive).ToList();
        }
        // END OF Windows
        //////////////////////////////////////////////////////////////////////////////////////////////////////////

    }
}
