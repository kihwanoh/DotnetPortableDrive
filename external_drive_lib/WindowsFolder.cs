using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using external_drive_lib.Helpers;

namespace external_drive_lib
{
    class WindowsFolder : IFolder2 {

        private string _parent, _name;

        public WindowsFolder(string parentFolder, string folderName) {
            _parent = parentFolder;
            _name = folderName;

            Debug.Assert(!_parent.EndsWith("\\") || ParentIsDrive());
            // drive len is 3
            Debug.Assert(_parent.Length >= 3);
        }
        
        public string Name {
            get { return _name; }
        }

        public bool Exists => Directory.Exists(FullPath);

        public string FullPath => _parent + (ParentIsDrive()? "" : "\\") + _name;

        public IDrive Drive => new WindowsDrive(_parent.Substring(0,3));

        public IFolder Parent {
            get {
                if (ParentIsDrive())
                    return null;
                var di = new DirectoryInfo(_parent);
                return new WindowsFolder(di.Parent.FullName, di.Name);
            }
        }
      
        public void CopyFile(IFile file, bool synchronous)
        {
            var copyOptions = 4 | 16 | 512 | 1024;
            var andoid = file as PortableFile;
            var win = file as WindowsFile;
            // it can either be android or windows
            //Debug.Assert(andoid != null || win != null);

            var destPath = FullPath + "\\" + file.Name;
            if (win != null)
            {
                if (synchronous)
                    File.Copy(file.FullPath, destPath, true);
                else
                    Task.Run(() => File.Copy(file.FullPath, destPath, true));
            }
            else if (andoid != null)
            {
                // android file to windows:

                // Windows stupidity - if file exists, it will display a stupid "Do you want to replace" dialog,
                // even if we speicifically told it not to (via the copy options)
                if (File.Exists(destPath)) File.Delete(destPath);
                var shellFolder = WindowsHelper.GetShell32Folder(FullPath);
                shellFolder.CopyHere(andoid.raw_folder_item(), copyOptions);
                //logger.Debug("winfolder: CopyHere complete");
                if (synchronous)
                WindowsHelper.WaitForWinCopyComplete(file.Size, destPath);
            }
        }

        public IEnumerable<IFile> EnumerateFiles()
        {
            return new DirectoryInfo(FullPath).EnumerateFiles().Select(f => new WindowsFile(FullPath, f.Name));
        }

        public IEnumerable<IFolder> EnumerateChildFolders()
        {
            return new DirectoryInfo(FullPath).EnumerateDirectories().Select(f => new WindowsFolder(FullPath, f.Name));
        }

        public void DeleteAsync()
        {
            Task.Run(() => DeleteSync());
        }

        public void DeleteSync()
        {
            Directory.Delete(FullPath, true);
        }

        private bool ParentIsDrive()
        {
            return _parent.Length <= 3;
        }
        
    }
}
