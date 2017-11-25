using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using external_drive_lib.Helpers;
using Shell32;

namespace external_drive_lib
{
    internal class PortableDevice : IDrive {
        private FolderItem _root;

        private readonly string _friendlyName;
        private string _rootPath;
        /* A USB device that is plugged in identifies itself by its VID/PID combination. 
         * A VID is a 16-bit vendor number (Vendor ID). A PID is a 16-bit product number (Product ID). 
         * The PC uses the VID/PID combination to find the drivers (if any) that are to be used for the USB device. */
        private string _vidPid = "";
        // this portable device's unique ID - think of it as a serial number
        private string _uniqueId = "";

        private bool _enumeratedChildren;
        private List<IFolder> _folders = new List<IFolder>();
        private List<IFile> _files = new List<IFile>();

        public PortableDevice(FolderItem fi) {
            _root = fi;
            _friendlyName = _root.Name;
            _rootPath = _root.Path;

            if ( UsbHelpers.PortablePathToVidpid(_rootPath, ref _vidPid))
                _uniqueId = _vidPid;
            // 1.2.3+ - sometimes, we can't match vidpid to unique id (for instance, iphones). in this case, do our best and just
            //          use the unique id from the path itself
            var uniqueIdFromPath = UsbHelpers.UniqueIdFromRootPath(_rootPath);
            if (uniqueIdFromPath != "") _uniqueId = uniqueIdFromPath;

            DriveType = PortableDeviceHelpers.FindDriveType(_root, _friendlyName);
        }

        public PortableDriveType DriveType { get; }

        public string RootName => _rootPath;

        public string UniqueId
        {
            get => _uniqueId;
            set => _uniqueId = value;
        }

        public string FriendlyName => _friendlyName;

        public bool UsbConnected { get; internal set; }

        public string VidPid { get; internal set; }
        
        public bool IsConnected()
        {
            return true;
        }

        public bool IsAvailable()
        {
            try
            {
                var items = ((Folder) _root.GetFolder).Items();
                var hasItems = items.Count >= 1;

                if (!DriveType.IsIosOperatingSystem() || !hasItems) return hasItems;
                // iphone - even if connected, until we allow "Read/Write" files, it won't be available
                // so, we might see "Internal Storage", but that will be completely empty
                var dcim = TryParseFolder("*/dcim");
                return dcim != null;
            }
            catch
            {
                return false;
            }
        }
        
        public IEnumerable<IFolder> EnumerateFolders()
        {
            if (_enumeratedChildren) return _folders;
            _enumeratedChildren = true;
            PortableDeviceHelpers.EnumerateChildren(this, _root, _folders, _files);
            return _folders;
        }

        public IEnumerable<IFile> EnumerateFiles()
        {
            if (_enumeratedChildren) return _files;
            _enumeratedChildren = true;
            PortableDeviceHelpers.EnumerateChildren(this, _root, _folders, _files);
            return _files;
        }

        public IFile ParseFile(string path)
        {
            var f = TryParseFile(path);
            if (f == null) throw new IOException("invalid path " + path);
            return f;
        }

        public IFolder ParseFolder(string path)
        {
            var f = TryParseFolder(path);
            if (f == null) throw new IOException("invalid path " + path);
            return f;
        }

        public IFile TryParseFile(string path)
        {
            var uniqueDriveId = "{" + _uniqueId + "}";
            if (path.StartsWith(uniqueDriveId, StringComparison.CurrentCultureIgnoreCase))
                path = path.Substring(uniqueDriveId.Length + 2); // ignore ":\" as well
            if (path.StartsWith(_rootPath, StringComparison.CurrentCultureIgnoreCase))
                path = path.Substring(_rootPath.Length + 1);

            var subFolderNames = path.Replace("/", "\\").Split('\\').ToList();
            var fileName = subFolderNames.Last();
            subFolderNames.RemoveAt(subFolderNames.Count - 1);

            var rawFolder = ParseSubFolder(subFolderNames);
            var file = (rawFolder?.GetFolder as Folder)?.ParseName(fileName);
            return file == null ? null : new PortableFile(this, file as FolderItem2);
        }

        public IFolder TryParseFolder(string path)
        {
            path = path.Replace("/", "\\");
            if (path.EndsWith("\\"))
                path = path.Substring(0, path.Length - 1);
            var uniqueDriveId = "{" + _uniqueId + "}";
            if (path.StartsWith(uniqueDriveId, StringComparison.CurrentCultureIgnoreCase))
                path = path.Substring(uniqueDriveId.Length + 2); // ignore ":\" as well
            if (path.StartsWith(_rootPath, StringComparison.CurrentCultureIgnoreCase))
                path = path.Substring(_rootPath.Length + 1);

            var subFolderNames = path.Split('\\').ToList();
            var rawFolder = ParseSubFolder(subFolderNames);
            return rawFolder == null ? null : new PortableFolder(this, rawFolder);
        }

        public IFolder CreateFolder(string path)
        {
            path = path.Replace("/", "\\");
            if (path.EndsWith("\\"))
                path = path.Substring(0, path.Length - 1);
            var id = "{" + _uniqueId + "}:\\";
            var containsDrivePrefix = path.StartsWith(id, StringComparison.CurrentCultureIgnoreCase);
            if (containsDrivePrefix)
                path = path.Substring(id.Length);

            var cur = _root;
            var subFolders = path.Split('\\');
            foreach (var subName in subFolders)
            {
                var folderObject = cur.GetFolder as Folder;
                var sub = folderObject?.ParseName(subName);
                if (sub == null && folderObject != null)
                {
                    folderObject.NewFolder(subName);
                    sub = folderObject.ParseName(subName);
                }
                if (sub == null)
                    throw new IOException("could not create part of path " + path);

                if (!sub.IsFolder)
                    throw new IOException("part of path is a file: " + path);
                cur = sub;
            }

            return new PortableFolder(this, cur);
        }

        public string ParsePortablePath(FolderItem fi)
        {
            var path = fi.Path;
            if (path.EndsWith("\\"))
                path = path.Substring(path.Length - 1);
            Debug.Assert(path.StartsWith(_rootPath, StringComparison.CurrentCultureIgnoreCase));
            // ignore the drive + "\\"
            path = path.Substring(_rootPath.Length + 1);
            var subFolderCount = path.Count(c => c == '\\') + 1;
            var cur = fi;
            var name = "";
            for (int i = 0; i < subFolderCount; ++i)
            {
                if (name != "")
                    name = "\\" + name;
                name = cur.Name + name;
                cur = ((Folder2) cur.Parent).Self;
            }

            name = "{" + _uniqueId + "}:\\" + name;
            return name;
        }

        private FolderItem ParseSubFolder(IEnumerable<string> subFolderPath)
        {
            var curFolder = _root.GetFolder as Folder;
            var curFolderItem = _root;
            var idx = 0;
            foreach (var sub in subFolderPath)
            {
                if (idx == 0 && sub == "*")
                {
                    // special case - replace with single root folder
                    var subItems = curFolder?.Items();
                    if (subItems != null && subItems.Count == 1 && subItems.Item(0).IsFolder)
                        curFolder = subItems.Item(0).GetFolder as Folder;
                    else
                        throw new IOException("Root drive doesn't have a single root folder (*)");
                }
                else
                {
                    var subFolder = curFolder?.ParseName(sub);
                    if (subFolder == null) return null;
                    curFolderItem = subFolder;
                    curFolder = curFolderItem.GetFolder as Folder;
                }
                ++idx;
            }
            return curFolderItem;
        }
    }
}
