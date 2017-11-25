using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace external_drive_lib
{
    class WindowsFile : IFile {
        private string path_;
        private string name_;
        public WindowsFile(string path, string name) {
            Debug.Assert(!path.EndsWith("\\"));
            path_ = path;
            name_ = name;
            // drive len is 3
            Debug.Assert(path_.Length >= 3);
        }

        public string Name => name_;

        public IFolder Folder {
            get {
                var di = new DirectoryInfo(path_);
                return new WindowsFolder( di.Parent.FullName, di.Name );
            }
        }

        public string FullPath => path_ + "\\" + name_;

        public bool Exists => File.Exists(FullPath);
        public IDrive Drive => new WindowsDrive(path_.Substring(0,3));

        public long Size => new FileInfo(FullPath).Length;
        public DateTime LastWriteTime => new FileInfo(FullPath).LastWriteTime;

        public void CopyAsync(string destPath)
        {
            var dest = ExternalDriveRoot.Instance.ParseFolder(destPath) as IFolder2;
            if (dest != null)
                dest.CopyFile(this, false);
            else
                throw new IOException("destination path does not exist: " + destPath);
        }

        public void CopySync(string destPath)
        {
            var dest = ExternalDriveRoot.Instance.ParseFolder(destPath) as IFolder2;
            if (dest != null)
                dest.CopyFile(this, true);
            else
                throw new IOException("destination path does not exist: " + destPath);
        }

        public void DeleteAsync()
        {
            File.Delete(FullPath);
        }

        public void DeleteSync()
        {
            File.Delete(FullPath);
        }
    }
}
