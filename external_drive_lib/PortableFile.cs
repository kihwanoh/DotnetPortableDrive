using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using external_drive_lib.Helpers;
using Shell32;

namespace external_drive_lib
{
    internal class PortableFile : IFile
    {
        private FolderItem2 _fi;
        private PortableDevice _drive;
        public PortableFile(PortableDevice drive, FolderItem2 fi) {
            _drive = drive;
            _fi = fi;
            Debug.Assert(!fi.IsFolder);
        }

        // for android_folder.copy
        internal FolderItem2 raw_folder_item() {
            return _fi;
        }

        // ----- PROPERTIES
        public bool Exists
        {
            get
            {
                try
                {
                    if (Drive.IsAvailable())
                    {
                        // if this throws, drive exists, but file does not
                        ExternalDriveRoot.Instance.ParseFile(FullPath);
                        return true;
                    }
                }
                catch
                {
                    //ignored
                }
                return false;
            }
        }

        public string Name => _fi.Name;

        public IFolder Folder => new PortableFolder(_drive, ((Folder2) _fi.Parent).Self);

        public string FullPath => _drive.ParsePortablePath(_fi);

        public IDrive Drive => _drive;

        public long Size => PortableDeviceHelpers.PortableFileSize(_fi);

        public DateTime LastWriteTime
        {
            get
            {
                try
                {
                    var dt = (DateTime)_fi.ExtendedProperty("write");
                    return dt;
                }
                catch
                {
                    // ignored
                }
                try
                {
                    // this will return something like "5/11/2017 08:29"
                    var dateStr = ((Folder) _fi.Parent).GetDetailsOf(_fi, 3).ToLower();
                    var dtBackup = DateTime.Parse(dateStr);
                    return dtBackup;
                }
                catch
                {
                    return DateTime.MinValue;
                }
            }
        }


        // ----- METHODS
        public void CopyAsync(string destPath)
        {
            var dest = ExternalDriveRoot.Instance.ParseFolder(destPath) as IFolder2;
            if (dest != null)
                dest.CopyFile(this, false);
            else
                throw new IOException("destination path does not exist: " + destPath);
        }

        public void DeleteAsync()
        {
            Task.Run(() => WindowsHelper.DeleteSyncPortableFile(_fi));
        }

        public void CopySync(string destPath)
        {
            var dest = ExternalDriveRoot.Instance.ParseFolder(destPath) as IFolder2;
            if (dest != null)
                dest.CopyFile(this, true);
            else
                throw new IOException("destination path does not exist: " + destPath);
        }

        public void DeleteSync()
        {
            WindowsHelper.DeleteSyncPortableFile(_fi);
        }
    }
}
