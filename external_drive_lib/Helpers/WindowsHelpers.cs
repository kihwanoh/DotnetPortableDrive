using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using external_drive_lib.Native;
using Shell32;

namespace external_drive_lib.Helpers
{
    internal static class WindowsHelper
    {
        public static string TemporaryDirectoryPath { get; } = GenerateTemporaryDirectoryPath();
        
        // ----- PUBLIC METHODS
        public static Folder GetShell32Folder(object folderPath)
        {
            var shellAppType = Type.GetTypeFromProgID("Shell.Application");
            var shell = Activator.CreateInstance(shellAppType);
            return (Folder)shellAppType.InvokeMember("NameSpace", BindingFlags.InvokeMethod, null, shell, new[] { folderPath });
        }

        public static void WaitForWinCopyComplete(long size, string fileName, int maxRetry = 25, int maxRetryFirstTime = 100) {
            // 1.2.4+ 0 can be a valid size, that can mean we started copying
            long lastSize = 0;
            // the idea is - if after waiting a while, something got copied (size has changed), we keep waiting
            // the copy process can take a while to start...
            lastSize = WaitForWinFileSize(fileName, size, maxRetryFirstTime);
            if ( lastSize <= 0)
                throw new IOException("File may have not been copied - " + fileName + " got 0, expected " + size);

            // the idea is - if after waiting a while, something got copied (size has changed), we keep waiting
            while (lastSize < size) {
                var curSize = WaitForWinFileSize(fileName, size, maxRetry);
                if ( curSize == lastSize)
                    throw new IOException("File may have not been copied - " + fileName + " got " + curSize + ", expected " + size);
                lastSize = curSize;
            }
        }

        public static void WaitForPortableCopyComplete(string fullFileName, long size, int maxRetry = 25, int maxRetryFirstTime = 100) {
            long lastSize = -1;
            // the idea is - if after waiting a while, something got copied (size has changed), we keep waiting
            // the copy process can take a while to start...
            lastSize = WaitForAndroidFileSize(fullFileName, size, maxRetryFirstTime);
            if ( lastSize < 0)
                throw new IOException("File may have not been copied - " + fullFileName + " got -1, expected " + size);
            while (lastSize < size) {
                var curSize = WaitForAndroidFileSize(fullFileName, size, maxRetry);
                if ( curSize == lastSize)
                    throw new IOException("File may have not been copied - " + fullFileName + " got " + curSize + ", expected " + size);
                lastSize = curSize;
            }
        }
        
        public static void DeleteSyncPortableFile(FolderItem fi)
        {
            // note: this can only happen synchronously - otherwise, we'd end up deleting something from HDD before it was fully moved from the HDD
            Debug.Assert( !fi.IsFolder);
            // https://msdn.microsoft.com/en-us/library/windows/desktop/bb787874(v=vs.85).aspx
            var moveOptions = 4 | 16 | 512 | 1024;
            try {
                var temp = TemporaryDirectoryPath;
                var tempFolder = GetShell32Folder(temp);
                var fileName = fi.Name;
                var size = fi.Size;
                tempFolder.MoveHere(fi, moveOptions);

                var name = temp + "\\" + fileName;
                WaitForWinCopyComplete(size, temp + "\\" + fileName);
                File.Delete(name);
            } catch {
                // backup - this will prompt a confirmation dialog
                // googled it quite a bit - there's no way to disable it
                fi.InvokeVerb("delete");
            }            
        }
        
        public static void WaitForWinFolderMoveComplete(string folder, string oldFullPath)
        {
            // TODO: surround in try/catch?
            long lastSize = -1;
            const int retryFindFolderMoveComplete = 20;
            for (var r = 0; r < retryFindFolderMoveComplete; ++r) {
                const int retryGetRecursiveSizeCount = 4;
                long curSize = 0;
                GetRecursiveSize(new DirectoryInfo(folder), ref curSize);
                for (var i = 0; i < retryGetRecursiveSizeCount && curSize == lastSize; ++i) {
                    Thread.Sleep(100);
                    curSize = 0;
                    GetRecursiveSize(new DirectoryInfo(folder), ref curSize);
                }
                if (curSize > lastSize) {
                    // at this point, the size increased - therefore, it's still moving files
                    lastSize = curSize;
                    // ... we'll wait until for X times we find not folder change
                    r = 0;
                    continue;
                }
                // at this point, we know that for 'retry_count' consecutive tries, the size remained the same
                // therefore, lets find out if the original Folder still exists
                try {
                    // if this doesn't throw, the folder is still not fully moved
                    ExternalDriveRoot.Instance.ParseFolder(oldFullPath);
                } catch {
                    return;
                }
            }
            // here, we're not really sure if the move worked - for retry_find_folder_move_complete, the recursive size hasn't changed,
            // and we old folder still exists
            throw new IOException("could not delete " + oldFullPath);
        }
        
        public static void DeleteSyncPortableFolder(FolderItem fi, string oldFullPath) {
            // note: this can only happen synchronously - otherwise, we'd end up deleting something from HDD before it was fully moved from the HDD
            Debug.Assert( fi.IsFolder);

            // https://msdn.microsoft.com/en-us/library/windows/desktop/bb787874(v=vs.85).aspx
            var moveOptions = 4 | 16 | 512 | 1024;
            try
            {
                var temp = TemporaryDirectoryPath;
                var tempFolder = GetShell32Folder(temp);
                var folderName = fi.Name;
                tempFolder.MoveHere(fi, moveOptions);

                // wait until folder dissapears from Android (the alternative would be to check all the file sizes match the original file sizes.
                // however, we'd need to do this recursively)

                var name = temp + "\\" + folderName;
                WaitForWinFolderMoveComplete(name, oldFullPath);
                Directory.Delete(name, true);
            } catch {
                // backup - this will prompt a confirmation dialog
                // googled it quite a bit - there's no way to disable it
                fi.InvokeVerb("delete");
            }
        }

        public static void Postpone(Action a, int ms)
        {
            Task.Delay(ms).ContinueWith(task => a());
        }

        // ----- PRIVATE METHODS
        private static string GenerateTemporaryDirectoryPath()
        {
            // we need a unique folder each time we're run, so that we never run into conflicts when moving stuff here
            var rootDir = Path.Combine(Path.GetTempPath(), Constants.TempFolderName);
            // this path is guaranteed to be unique
            var sessionDir = Path.Combine(rootDir, Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            Directory.CreateDirectory(sessionDir);
            RecrusiveDeleteFolders(Directory.EnumerateDirectories(rootDir), true);
            return sessionDir;
        }

        private static void RecrusiveDeleteFolders(IEnumerable<string> folders, bool asyncProcess = false)
        {
            if (asyncProcess) Task.Run(() => InternalRecrusiveDeleteFolders(folders));
            else InternalRecrusiveDeleteFolders(folders);
        }

        private static void InternalRecrusiveDeleteFolders(IEnumerable<string> folders)
        {
            foreach (var f in folders)
            {
                try { Directory.Delete(f, true); }
                catch { /* ignored */ }
            }
        }

        private static long WaitForWinFileSize(string fileName, long size, int retryCount)
        {
            // 1.2.4+ note - that 0 can be a valid file size, meaning we're in the process of copying the file
            long curSize = 0;
            for (var i = 0; i < retryCount && curSize < size; ++i)
            {
                if (!File.Exists(fileName)) continue;

                try {curSize = new FileInfo(fileName).Length;}
                catch {/* ignored */}
                if (curSize < size) Thread.Sleep(50);
            }
            return curSize;
        }

        private static long WaitForAndroidFileSize(string fullFileName, long size, int retryCount)
        {
            long curSize = -1;
            for (var i = 0; i < retryCount; ++i)
            {
                try {curSize = ExternalDriveRoot.Instance.ParseFile(fullFileName).Size;}
                catch {/* ignored */}
                if (curSize < size) Thread.Sleep(50);
            }
            return curSize;
        }

        private static void GetRecursiveSize(DirectoryInfo dir, ref long size)
        {
            foreach (var child in dir.EnumerateDirectories())
                GetRecursiveSize(child, ref size);

            size += dir.EnumerateFiles().Sum(f => f.Length);
        }
    }
}
