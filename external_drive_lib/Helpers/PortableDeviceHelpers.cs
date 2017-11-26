using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using external_drive_lib.Native;
using Shell32;

namespace external_drive_lib.Helpers
{
    public static class PortableDeviceHelpers
    {
        public static PortableDriveType FindDriveType(FolderItem root, string friendlyName)
        {
            var driveType = PortableDriveType.Portable;
            bool isAndroid = false, isPhone = false, isTablet = false, isApple = false, isIphone = false;

            if (friendlyName.ToLower().StartsWith(Constants.AppleDeviceName)) isApple = true;
            if (friendlyName.ToLower().Contains(Constants.IphoneDeviceName)) isIphone = true;

            try
            {
                if (root.IsFolder)
                {
                    var items = (root.GetFolder as Folder)?.Items();
                    if (items != null && items.Count == 1)
                    {
                        var child = items.Item(0);
                        var name = child.Name;
                        if (child.IsFolder)
                        {
                            switch (name)
                            {
                                case Constants.PhoneDeviceName:
                                    isPhone = true;
                                    break;
                                case Constants.TabletDeviceName:
                                    isTablet = true;
                                    break;
                            }
                            // at this point, see if child has a sub-folder called Android
                            isAndroid = (child.GetFolder as Folder)?.ParseName(Constants.AndroidDeviceName) != null;
                        }
                    }
                }
            }
            catch
            {
                // just leave drive type as portable
            }

            if (isPhone) driveType = isAndroid ? PortableDriveType.AndroidPhone : PortableDriveType.Iphone;
            else if (isTablet) driveType = isAndroid ? PortableDriveType.AndroidTablet : PortableDriveType.Ipad;
            else if (isAndroid) driveType = PortableDriveType.AndroidUnknown;
            if (isApple) driveType = isIphone ? PortableDriveType.Iphone : PortableDriveType.IosUnknown;

            return driveType;
        }

        public static bool IsPortableDevice(this PortableDriveType dt)
        {
            return dt == PortableDriveType.AndroidUnknown 
                || dt == PortableDriveType.AndroidPhone 
                || dt == PortableDriveType.AndroidTablet 
                || dt == PortableDriveType.Portable
                || dt == PortableDriveType.Iphone
                || dt == PortableDriveType.Ipad 
                || dt == PortableDriveType.IosUnknown;
        }

        public static bool IsAndroidOperatingSystem(this PortableDriveType dt)
        {
            return dt == PortableDriveType.AndroidUnknown 
                || dt == PortableDriveType.AndroidPhone
                || dt == PortableDriveType.AndroidTablet;
        }

        public static bool IsIosOperatingSystem(this PortableDriveType dt)
        {
            return dt == PortableDriveType.Iphone 
                || dt == PortableDriveType.Ipad 
                || dt == PortableDriveType.IosUnknown;
        }

        internal static void EnumerateChildren(PortableDevice drive, FolderItem fi, List<IFolder> folders, List<IFile> files)
        {
            folders.Clear();
            files.Clear();

            if (!fi.IsFolder) return;
            foreach (FolderItem child in ((Folder) fi.GetFolder).Items())
            {
                if (child.IsLink)
                {
                    // android shortcut " + child.Name
                    Debug.Assert(false);
                }
                else if (child.IsFolder)
                {
                    folders.Add(new PortableFolder(drive, child));
                }
                else
                {
                    files.Add(new PortableFile(drive, child as FolderItem2));
                }
            }
        }

        internal static List<string> GetVerbs(FolderItem fi)
        {
            return (from FolderItemVerb verb in fi.Verbs() select verb.Name).ToList();
        }

        internal static long PortableFileSize(FolderItem2 fi)
        {
            try { return (long)fi.ExtendedProperty("size"); }
            catch { /* ignored */ }
            try
            {
                var sizeStr = ((Folder) fi.Parent).GetDetailsOf(fi, 2).ToUpper().Trim();
                var sizeUnit = sizeStr.Substring(sizeStr.Length - 2, 2).Trim(); // unit
                var sizeNonConverted = sizeStr.Substring(0, sizeStr.Length - 2).Trim(); // number

                var index = Constants.BytesSuffix.IndexOf(sizeUnit);
                double.TryParse(sizeNonConverted, out double parsed);
                return Convert.ToInt64(Math.Pow(1024, index) * parsed);
            } catch { /* ignored */ }
            return -1;
        }

        internal static Folder GetMyComputer()
        {
            return WindowsHelper.GetShell32Folder(ShellSpecialFolderConstants.ssfDRIVES);
        }

        internal static List<FolderItem> GetPortableConnectedDeviceDrives()
        {
            var usbDrives = new List<FolderItem>();

            foreach (FolderItem fi in GetMyComputer().Items())
            {
                var path = fi.Path;
                try
                {
                    if (Directory.Exists(path) || path.Contains(":\\")) continue;
                }
                catch
                {
                    // a usb drive
                }
                usbDrives.Add(fi);
            }
            return usbDrives;
        }
    }
}
