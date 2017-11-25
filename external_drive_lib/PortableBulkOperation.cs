using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using external_drive_lib.Helpers;
using Shell32;

namespace external_drive_lib
{
    public static class PortableBulkOperation
    {
        private class CopyFileInfo
        {
            public string Name;
            public long Size;
        }

        // copies the folder's files - NOT its sub-folders
        //
        // callback - it's called after each file is copied. Args: the file, its index, number of files for total copy
        public static void BulkCopySync(string srcFolder, string destFolder, Action<string,int,int> copyCompleteCallback = null)
        {
            BulkCopySync( ExternalDriveRoot.Instance.ParseFolder(srcFolder).EnumerateFiles().ToList(), destFolder, copyCompleteCallback);
        }

        // callback - it's called after each file is copied. Args: the file, its index, number of files for total copy
        public static void BulkCopySync(IReadOnlyList<IFile> srcFiles, string destFolder, Action<string, int, int> copyCompleteCallback = null)
        {
            BulkCopy(srcFiles, destFolder, true, copyCompleteCallback);
        }

        // callback - it's called after each file is copied. Args: the file, its index, number of files for total copy
        public static void BulkCopyAsync(string srcFolder, string destFolder, Action<string,int,int> copyCompleteCallback = null) {
            BulkCopyAsync( ExternalDriveRoot.Instance.ParseFolder(srcFolder).EnumerateFiles().ToList(), destFolder, copyCompleteCallback);
        }

        // callback - it's called after each file is copied. Args: the file, its index, number of files for total copy
        public static void BulkCopyAsync(IReadOnlyList<IFile> srcFiles, string destFolder, Action<string,int,int> copyCompleteCallback = null) {
            BulkCopy(srcFiles, destFolder, false, copyCompleteCallback);
        }


        private static void BulkCopyWinSync(IReadOnlyList<string> srcFiles, string destFolderName, Action<string,int,int> copyCompleteCallback ) {
            var count = srcFiles.Count;
            var idx = 0;
            foreach (var f in srcFiles) {
                var name = Path.GetFileName(f);
                File.Copy(f, destFolderName + "\\" + name, true);
                try {
                    copyCompleteCallback?.Invoke(f,idx,count);
                } catch(IOException e) {
                    throw new IOException("could not find source file to copy " + f, e);
                }
                ++idx;
            }
        }

        private static void BulkCopyWin(IReadOnlyList<string> srcFiles, string destFolderName, bool synchronous, Action<string,int,int> copyCompleteCallback) {
            if (synchronous)
                BulkCopyWinSync(srcFiles, destFolderName, copyCompleteCallback);
            else
                Task.Run(() => BulkCopyWinSync(srcFiles, destFolderName, copyCompleteCallback));
        }
        
        private static void BulkCopy(IEnumerable<IFile> srcFiles, string destFolderName, bool synchronous, Action<string,int,int> copyCompleteCallback) {
            destFolderName = destFolderName.Replace("/", "\\");
            Debug.Assert(!destFolderName.EndsWith("\\"));
            // in case destination does not exist, create it
            ExternalDriveRoot.Instance.NewFolder(destFolderName);

            Dictionary<string, List<IFile>> filesByFolder = new Dictionary<string, List<IFile>>();
            foreach (var f in srcFiles) {
                var path = f.Folder.FullPath;
                if ( !filesByFolder.ContainsKey(path))
                    filesByFolder.Add(path, new List<IFile>());
                filesByFolder[path].Add(f);
            }

            var destFolder = ExternalDriveRoot.Instance.ParseFolder(destFolderName);
            var allSrcWin = srcFiles.All(f => f is WindowsFile);
            if (allSrcWin && destFolder is WindowsFolder) {
                BulkCopyWin( srcFiles.Select(f => (f as WindowsFile).FullPath).ToList(), destFolderName, synchronous, copyCompleteCallback);
                return;
            }

            //
            // here, we know the source or dest or both are android

            Folder destParentShellFolder = null;
            if (destFolder is PortableFolder)
                destParentShellFolder = (destFolder as PortableFolder).raw_folder_item().GetFolder as Folder;
            else if (destFolder is WindowsFolder) 
                destParentShellFolder = WindowsHelper.GetShell32Folder((destFolder as WindowsFolder).FullPath);                
            else 
                Debug.Assert(false);

            int count = filesByFolder.Sum(f => f.Value.Count);
            int idx = 0;
            foreach (var f in filesByFolder) {
                var srcParent = f.Value[0].Folder;
                var srcParentFileCount = srcParent.EnumerateFiles().Count();
                // filter can be specified by "file1;file2;file3.."
                string filterSpec = f.Value.Count == srcParentFileCount ? "*.*" : string.Join(";", f.Value.Select(n => n.Name));

                Folder srcParentShellFolder = null;
                if (srcParent is PortableFolder)
                    srcParentShellFolder = (srcParent as PortableFolder).raw_folder_item().GetFolder as Folder;
                else if (srcParent is WindowsFolder) 
                    srcParentShellFolder = WindowsHelper.GetShell32Folder((srcParent as WindowsFolder).FullPath);                
                else 
                    Debug.Assert(false);

                var srcItems = srcParentShellFolder.Items() as FolderItems3;
                // here, we filter only those files that you want from the source folder 
                srcItems.Filter(int.MaxValue & ~0x8000, filterSpec);
                // ... they're ignored, but still :)
                var copyOptions = 4 | 16 | 512 | 1024;
                // note: we want to compute these beforehand (before the copy takes place) - if a file is being copied, access to it can be locked,
                //       so even asking "f.name" will wait until the copy is 100% complete - which is NOT what we want
                List<CopyFileInfo> waitComplete = f.Value.Select(src => new CopyFileInfo {Name = src.Name, Size = src.Size}).ToList();
                if (srcItems.Count == f.Value.Count) {
                    if (synchronous && copyCompleteCallback != null)
                        Task.Run(() => destParentShellFolder.CopyHere(srcItems, copyOptions));
                    else
                        destParentShellFolder.CopyHere(srcItems, copyOptions);
                } else {
                    // "amazing" - for Android, the filter spec doesn't work - we need to copy each of them separately
                    Debug.Assert(f.Value[0] is PortableFile);
                    foreach (var file in f.Value)
                        destParentShellFolder.CopyHere((file as PortableFile).raw_folder_item(), copyOptions);
                }

                if ( synchronous)
                    WaitForCopyComplete(waitComplete, count, ref idx, destFolderName, copyCompleteCallback);
                else if (copyCompleteCallback != null)
                    // here, we're async, but with callback
                    Task.Run(() => WaitForCopyComplete(waitComplete, count, ref idx, destFolderName, copyCompleteCallback));
            }
        }

        private static void WaitForCopyComplete(List<CopyFileInfo> srcFiles, int count, ref int idx, string destFolderName, Action<string,int,int> copyCompleteCallback) {
            Debug.Assert(srcFiles.Count > 0);
            var destFolder = ExternalDriveRoot.Instance.ParseFolder(destFolderName);
            var destAndroid = destFolder is PortableFolder;
            var destWin = destFolder is WindowsFolder;
            Debug.Assert(destAndroid || destWin);

            /* 1.2.4+ - there are no really good defaults here for waiting for the copy to be complete. If it's a bulk copy,
             *          sometimes it just takes longer for a file's size to be non-zero, and we could end up with a IOException
             *          
             *          that's why we made these defaults a lot bigger on bulk_copy. We can't make them too big though, since we don't 
             *          want to wait indefinitely for a copy that might fail (say that the user unplugs the device)
             */
            int maxRetry = 75;
            int maxRetryFirstTime = 500;

            foreach (var f in srcFiles) {
                var destFile = destFolderName + "\\" + f.Name;
                if ( destWin)
                    WindowsHelper.WaitForWinCopyComplete(f.Size, destFile, maxRetry, maxRetryFirstTime);
                else if ( destAndroid)
                    WindowsHelper.WaitForPortableCopyComplete(destFile, f.Size, maxRetry, maxRetryFirstTime);
                copyCompleteCallback?.Invoke(f.Name, idx, count);
                ++idx;
            }
        }

    }
}
