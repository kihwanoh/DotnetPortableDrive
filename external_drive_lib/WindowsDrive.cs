using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace external_drive_lib
{
    /* note: the main reason we have win drives is so that you can copy from a windows drive to an android drive
     */
    class WindowsDrive : IDrive {

        private string _root;

        public PortableDriveType DriveType => PortableDriveType.InternalHdd;

        public string RootName => _root;

        public string UniqueId => _root;

        public string FriendlyName => _root;
        
        // ----- Constructor
        public WindowsDrive(DriveInfo di) {
            try {
                _root = di.RootDirectory.FullName;
            } catch {
                // "bad drive " + di + " : " + e;
                //_valid = false;
            }
        }
        public WindowsDrive(string root) {
            _root = root;
        }

        public bool IsConnected()
        {
            return true;
        }

        public bool IsAvailable()
        {
            return true;
        }

        public IEnumerable<IFolder> EnumerateFolders()
        {
            return new DirectoryInfo(_root).EnumerateDirectories().Select(f => new WindowsFolder(_root, f.Name));
        }

        public IEnumerable<IFile> EnumerateFiles()
        {
            return new DirectoryInfo(_root).EnumerateFiles().Select(f => new WindowsFile(_root, f.Name));
        }

        public IFile ParseFile(string path)
        {
            var f = TryParseFile(path);
            if (f == null)
                throw new IOException("invalid path " + path);
            return f;
        }

        public IFolder ParseFolder(string path)
        {
            var f = TryParseFolder(path);
            if (f == null)
                throw new IOException("invalid path " + path);
            return f;
        }

        public IFile TryParseFile(string path)
        {
            {
                path = path.Replace("/", "\\");
                var containsDrivePrefix = path.StartsWith(_root, StringComparison.CurrentCultureIgnoreCase);
                var full = containsDrivePrefix ? path : _root + path;
                if (File.Exists(full))
                {
                    var fi = new FileInfo(full);
                    return new WindowsFile(fi.DirectoryName, fi.Name);
                }
                return null;
            }
        }

        public IFolder TryParseFolder(string path)
        {
            path = path.Replace("/", "\\");
            var containsDrivePrefix = path.StartsWith(_root, StringComparison.CurrentCultureIgnoreCase);
            var full = containsDrivePrefix ? path : _root + path;
            if (Directory.Exists(full))
            {
                var fi = new DirectoryInfo(full);
                return new WindowsFolder(fi.Parent.FullName, fi.Name);
            }
            return null;
        }

        public IFolder CreateFolder(string path)
        {
            path = path.Replace("/", "\\");
            if (path.EndsWith("\\"))
                path = path.Substring(0, path.Length - 1);
            var containsDrivePrefix = path.StartsWith(_root, StringComparison.CurrentCultureIgnoreCase);
            if (!containsDrivePrefix)
                path = _root + path;
            Directory.CreateDirectory(path);

            return ParseFolder(path);
        }
    }
}
