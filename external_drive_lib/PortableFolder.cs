using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using external_drive_lib.Helpers;
using Shell32;

namespace external_drive_lib
{
    // https://blog.dotnetframework.org/2014/12/10/read-extended-properties-of-a-file-in-c/ -> this gets properties of a folder

    internal class PortableFolder : IFolder2 {

        private FolderItem _fi;
        private PortableDevice _drive;

        private bool _enumeratedChildren;
        private List<IFolder> _folders = new List<IFolder>();
        private List<IFile> _files = new List<IFile>();

        public PortableFolder(PortableDevice drive,FolderItem fi) {
            _drive = drive;
            _fi = fi;
            Debug.Assert(fi.IsFolder);
        }

        // ----- PROPERTIES
        public string FullPath => _drive.ParsePortablePath(_fi);

        public IDrive Drive => _drive;

        public string Name => _fi.Name;

        public bool Exists {
            get
            {
                try
                {
                    if (_drive.IsAvailable())
                    {
                        // if this throws, drive exists, but folder does not
                        ExternalDriveRoot.Instance.ParseFolder(FullPath);
                        return true;
                    }
                }
                catch
                {
                    // ignored
                }
                return false;
            }
        }

        public IFolder Parent => new PortableFolder(_drive, ((Folder2) _fi.Parent).Self);


        // for bulk copy
        public FolderItem raw_folder_item() {
            return _fi;
        }

        public IEnumerable<IFile> EnumerateFiles()
        {
            if (_enumeratedChildren) return _files;
            _enumeratedChildren = true;
            PortableDeviceHelpers.EnumerateChildren(_drive, _fi, _folders, _files);
            return _files;
        }
           
        public IEnumerable<IFolder> EnumerateChildFolders() {
            if (_enumeratedChildren) return _folders;
            _enumeratedChildren = true;
            PortableDeviceHelpers.EnumerateChildren(_drive, _fi, _folders, _files);
            return _folders;
        }

        public void DeleteAsync()
        {
            var full = FullPath;
            Task.Run(() => WindowsHelper.DeleteSyncPortableFolder(_fi, full));
        }

        public void DeleteSync()
        {
            var full = FullPath;
            WindowsHelper.DeleteSyncPortableFolder(_fi, full);
        }

        public void CopyFile(IFile file, bool synchronous) {
            var copyOptions = 4 | 16 | 512 | 1024 ;
            var andoid = file as PortableFile;
            var win = file as WindowsFile;
            // it can either be android or windows
            //Debug.Assert(andoid != null || win != null);
            FolderItem destItem = null;
            var souceName = file.Name;
            if (andoid != null)
            {
                destItem = andoid.raw_folder_item();
            }
            else if (win != null)
            {
                var winFileName = new FileInfo(win.FullPath);

                var shellFolder = WindowsHelper.GetShell32Folder(winFileName.DirectoryName);
                var shellFile = shellFolder.ParseName(winFileName.Name);
                Debug.Assert(shellFile != null);
                destItem = shellFile;
            }

            // Windows stupidity - if file exists, it will display a stupid "Do you want to replace" dialog,
            // even if we speicifically told it not to (via the copy options)
            //
            // so, if file exists, delete it first
            var existingName = ((Folder) _fi.GetFolder).ParseName(souceName);
            if ( existingName != null)
                WindowsHelper.DeleteSyncPortableFile(existingName);

            ((Folder) _fi.GetFolder).CopyHere(destItem, copyOptions);
            if ( synchronous)
                WindowsHelper.WaitForPortableCopyComplete(FullPath + "\\" + souceName, file.Size);
        }
    }
}
