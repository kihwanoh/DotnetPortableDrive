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
        private static readonly Lazy<ExternalDriveRoot> LazyInstance = new Lazy<ExternalDriveRoot>(() => new ExternalDriveRoot());

        // note: not all devices register as USB hubs, some only register as controller devices
        private readonly DevicesMonitor _monitorUsbhubDevices = new DevicesMonitor();
        private readonly DevicesMonitor _monitorControllerDevices = new DevicesMonitor();
        private readonly Dictionary<string, string> _vidpidToUniqueId = new Dictionary<string, string>();
        // this includes all drives, even the internal ones
        private List<IDrive> _drives = new List<IDrive>();

        // ----- PROPERTIES
        public static ExternalDriveRoot Instance => LazyInstance.Value;

        public bool AutoCloseWinDialogs { get; set; } = true;

        public IReadOnlyList<IDrive> Drives
        {
            get
            {
                // returns all drives, even the internal HDDs - you might need this if you want to copy a file onto an external drive
                lock (this) return _drives;
            }
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

        

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Methods
        
        public void Refresh()
        {
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

        public IDrive TryGetDrive(string drivePrefix)
        {
            // As drive name, use any of: 
            // "{<unique_id>}:", "<drive-name>:", "[a<android-drive-index>]:", "[i<ios-index>]:", "[p<portable-index>]:", "[d<drive-index>]:"
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

        public IDrive GetDrive(string drivePrefix) {
            // throws if drive not found
            // case insensitive
            var d = TryGetDrive(drivePrefix);
            if ( d == null)
                throw new IOException("invalid drive " + drivePrefix);
            return d;
        }

        public IFile TryParseFile(string path) {
            // returns null on failure
            // split into drive + path
            string driveStr, folderOrFile;
            SplitIntoDriveAndFolderPath(path, out driveStr, out folderOrFile);
            if (driveStr == null)
                return null;
            var drive = GetDrive(driveStr);
            return drive.TryParseFile(folderOrFile);            
        }

        public IFolder TryParseFolder(string path) {
            // returns null on failure
            string driveStr, folderOrFile;
            SplitIntoDriveAndFolderPath(path, out driveStr, out folderOrFile);
            if ( driveStr == null)
                return null;
            var drive = TryGetDrive(driveStr);
            if (drive == null)
                return null;
            return drive.TryParseFolder(folderOrFile);            
        }

        public IFile ParseFile(string path) {
            // throws if anything goes wrong
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

        public IFolder ParseFolder(string path)
        {
            // throws if anything goes wrong
            string driveStr, folderOrFile;
            SplitIntoDriveAndFolderPath(path, out driveStr, out folderOrFile);
            if ( driveStr == null)
                throw new IOException("invalid path " + path);
            var drive = GetDrive(driveStr);
            return drive.ParseFolder(folderOrFile);
        }

        public IFolder NewFolder(string path)
        {
            // creates all folders up to the given path
            string driveStr, folderOrFile;
            SplitIntoDriveAndFolderPath(path, out driveStr, out folderOrFile);
            if ( driveStr == null)
                throw new IOException("invalid path " + path);
            var drive = GetDrive(driveStr);
            return drive.CreateFolder(folderOrFile);
        }

        #region Event Handler
        private void OnNewDevice(string vidPid, string uniqueId)
        {
            lock (this)
            {
                if (_vidpidToUniqueId.ContainsKey(vidPid))
                    _vidpidToUniqueId[vidPid] = uniqueId;
                else
                    _vidpidToUniqueId.Add(vidPid, uniqueId);
            }
            RefreshPortableUniqueIds();
            var alreadyADrive = false;
            lock (this)
            {
                var ad = _drives.FirstOrDefault(d => d.UniqueId == uniqueId) as PortableDevice;
                if (ad != null)
                {
                    ad.UsbConnected = true;
                    alreadyADrive = true;
                }
            }
            if (!alreadyADrive)
                WindowsHelper.Postpone(() => MonitorForDrive(vidPid, 0), 50);
        }
        private void OnDeletedDevice(string vidPid, string uniqueId)
        {
            lock (this)
            {
                var ad = _drives.FirstOrDefault(d => d.UniqueId == uniqueId) as PortableDevice;
                if (ad != null)
                    ad.UsbConnected = false;
            }
            Refresh();
        }

        private void DeviceAddedController(object sender, DeviceChangedEventArgs e)
        {
            if (!e.AffectedDevices.ContainsKey("Dependent")) return;
            var deviceId = e.AffectedDevices["Dependent"];
            string vidPid = "", uniqueId = "";
            if (UsbHelpers.DependentToVidpidAndUniqueId(deviceId, ref vidPid, ref uniqueId))
                OnNewDevice(vidPid, uniqueId);
        }
        private void DeviceRemovedController(object sender, DeviceChangedEventArgs e)
        {
            if (!e.AffectedDevices.ContainsKey("Dependent")) return;
            var deviceId = e.AffectedDevices["Dependent"];
            string vidPid = "", uniqueId = "";
            if (UsbHelpers.DependentToVidpidAndUniqueId(deviceId, ref vidPid, ref uniqueId))
                OnDeletedDevice(vidPid, uniqueId);
        }
        private void DeviceAdded(object sender, DeviceChangedEventArgs e)
        {
            if (e.AffectedDevices.ContainsKey("PNPDeviceID"))
            {
                var deviceId = e.AffectedDevices["PNPDeviceID"];
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
        private void DeviceRemoved(object sender, DeviceChangedEventArgs e)
        {
            if (e.AffectedDevices.ContainsKey("PNPDeviceID"))
            {
                var deviceId = e.AffectedDevices["PNPDeviceID"];
                string vidPid = "", uniqueId = "";
                if (UsbHelpers.PnpDeviceIdToVidpidAndUniqueId(deviceId, ref vidPid, ref uniqueId))
                    OnDeletedDevice(vidPid, uniqueId);
            }
            else
            {
                // deleted usb device with no PNPDeviceID
                Debug.Assert(false);
            }
        }
        #endregion

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

        private void SplitIntoDriveAndFolderPath(string path, out string drive, out string folderOrFile)
        {
            path = path.Replace("/", "\\");
            var endOfDrive = path.IndexOf(":\\");
            if (endOfDrive >= 0)
            {
                drive = path.Substring(0, endOfDrive + 2);
                folderOrFile = path.Substring(endOfDrive + 2);
            }
            else
                drive = folderOrFile = null;
        }

        private void RefreshPortableUniqueIds()
        {
            lock (this)
                foreach (PortableDevice ad in _drives.OfType<PortableDevice>())
                {
                    Debug.Assert(ad.VidPid != null);
                    if (_vidpidToUniqueId.ContainsKey(ad.VidPid))
                        ad.UniqueId = _vidpidToUniqueId[ad.VidPid];
                }
        }

        private void MonitorForDrive(string vidpid, int idx)
        {
            // here, we know the drive was connected, wait a bit until it's actually visible
            const int maxRetries = 10;
            var drivesNow = GetPortableDrives();
            var found = drivesNow.FirstOrDefault(d => ((PortableDevice)d).VidPid == vidpid);
            if (found != null)
                Refresh();
            else if (idx < maxRetries)
                WindowsHelper.Postpone(() => MonitorForDrive(vidpid, idx + 1), 100);
            else
            {
                // "can't find usb connected drive " + vidpid
                //Debug.Assert(false);
            }
        }
        
        // END OF Portable
        //////////////////////////////////////////////////////////////////////////////////////////////////////////

        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Windows

        private List<IDrive> GetWinDrives()
        {
            // for now, I return all drives - don't care about which is External, Removable, whatever
            return DriveInfo.GetDrives().Select(d => new WindowsDrive(d) as IDrive).ToList();
        }

        // END OF Windows
        //////////////////////////////////////////////////////////////////////////////////////////////////////////
    }
}
