using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shell32;

namespace external_drive_lib.Helpers
{
    internal static class WindowsHelper
    {
        public static string TemporaryDirectoryPath { get; } = GenerateTemporaryDirectoryPath();

        private static string GenerateTemporaryDirectoryPath() {
            // we need a unique folder each time we're run, so that we never run into conflicts when moving stuff here
            var root_dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\external_drive_temp\\";
            var dir = root_dir + DateTime.Now.Ticks;
            // FIXME create a task to erase all other folders (previously created that is)
            try {
                // erase all the other created folders, if any
                var prev_dirs = new DirectoryInfo(root_dir).EnumerateDirectories().Select(d => d.FullName).ToList();
                Task.Run(() => DeleteFolders(prev_dirs));
                Directory.CreateDirectory(dir);
            } catch {
            }
            return dir;
        }

        private static void DeleteFolders(IEnumerable<string> folders)
        {
            foreach (var f in folders)
            {
                try
                {
                    Directory.Delete(f, true);
                }
                catch
                {
                    // ignored
                }
            }
        }

        public static Folder GetShell32Folder(object folder_path)
        {
            var shellAppType = Type.GetTypeFromProgID("Shell.Application");
            var shell = Activator.CreateInstance(shellAppType);
            return (Folder)shellAppType.InvokeMember("NameSpace", BindingFlags.InvokeMethod, null, shell, new object[] { folder_path });
        }

        private static long WaitForWinFileSize(string file_name, long size, int retry_count) {
            // 1.2.4+ note - that 0 can be a valid file size, meaning we're in the process of copying the file
            long cur_size = 0;
            for (var i = 0; i < retry_count && cur_size < size; ++i) {
                if ( File.Exists(file_name))
                    try {
                        cur_size = new FileInfo(file_name).Length;
                    } catch {
                    }
                if ( cur_size < size)
                    Thread.Sleep(50);
            }
            return cur_size;
        }

        public static void WaitForWinCopyComplete(long size, string file_name, int max_retry = 25, int max_retry_first_time = 100) {
            // 1.2.4+ 0 can be a valid size, that can mean we started copying
            long last_size = 0;
            // the idea is - if after waiting a while, something got copied (size has changed), we keep waiting
            // the copy process can take a while to start...
            last_size = WaitForWinFileSize(file_name, size, max_retry_first_time);
            if ( last_size <= 0)
                throw new IOException("File may have not been copied - " + file_name + " got 0, expected " + size);

            // the idea is - if after waiting a while, something got copied (size has changed), we keep waiting
            while (last_size < size) {
                var cur_size = WaitForWinFileSize(file_name, size, max_retry);
                if ( cur_size == last_size)
                    throw new IOException("File may have not been copied - " + file_name + " got " + cur_size + ", expected " + size);
                last_size = cur_size;
            }
        }

        private static long WaitForAndroidFileSize(string full_file_name, long size, int retry_count) {
            long cur_size = -1;
            for (var i = 0; i < retry_count ; ++i) {
                try {
                    cur_size = ExternalDriveRoot.Instance.ParseFile(full_file_name).Size;
                } catch {
                }
                if ( cur_size < size)
                    Thread.Sleep(50);
            }
            return cur_size;
        }

        public static void WaitForPortableCopyComplete(string full_file_name, long size, int max_retry = 25, int max_retry_first_time = 100) {
            long last_size = -1;
            // the idea is - if after waiting a while, something got copied (size has changed), we keep waiting
            // the copy process can take a while to start...
            last_size = WaitForAndroidFileSize(full_file_name, size, max_retry_first_time);
            if ( last_size < 0)
                throw new IOException("File may have not been copied - " + full_file_name + " got -1, expected " + size);
            while (last_size < size) {
                var cur_size = WaitForAndroidFileSize(full_file_name, size, max_retry);
                if ( cur_size == last_size)
                    throw new IOException("File may have not been copied - " + full_file_name + " got " + cur_size + ", expected " + size);
                last_size = cur_size;
            }
        }
        
        // note: this can only happen synchronously - otherwise, we'd end up deleting something from HDD before it was fully moved from the HDD
        public static void DeleteSyncPortableFile(FolderItem fi) {
            Debug.Assert( !fi.IsFolder);
            // https://msdn.microsoft.com/en-us/library/windows/desktop/bb787874(v=vs.85).aspx
            var move_options = 4 | 16 | 512 | 1024;
            try {
                var temp = TemporaryDirectoryPath;
                var temp_folder = GetShell32Folder(temp);
                var file_name = fi.Name;
                var size = fi.Size;
                temp_folder.MoveHere(fi, move_options);

                var name = temp + "\\" + file_name;
                WaitForWinCopyComplete(size, temp + "\\" + file_name);
                File.Delete(name);
            } catch {
                // backup - this will prompt a confirmation dialog
                // googled it quite a bit - there's no way to disable it
                fi.InvokeVerb("delete");
            }            
        }

        private static void GetRecursiveSize(DirectoryInfo dir, ref long size) {
            foreach ( var child in dir.EnumerateDirectories())
                GetRecursiveSize(child, ref size);

            foreach (var f in dir.EnumerateFiles())
                size += f.Length;
        }
        
        // FIXME surround in try/catch?
        public static void WaitForWinFolderMoveComplete(string folder, string old_full_path) {
            long last_size = -1;
            const int retry_find_folder_move_complete = 20;
            for (var r = 0; r < retry_find_folder_move_complete; ++r) {
                const int retry_get_recursive_size_count = 4;
                long cur_size = 0;
                GetRecursiveSize(new DirectoryInfo(folder), ref cur_size);
                for (var i = 0; i < retry_get_recursive_size_count && cur_size == last_size; ++i) {
                    Thread.Sleep(100);
                    cur_size = 0;
                    GetRecursiveSize(new DirectoryInfo(folder), ref cur_size);
                }
                if (cur_size > last_size) {
                    // at this point, the size increased - therefore, it's still moving files
                    last_size = cur_size;
                    // ... we'll wait until for X times we find not folder change
                    r = 0;
                    continue;
                }
                // at this point, we know that for 'retry_count' consecutive tries, the size remained the same
                // therefore, lets find out if the original Folder still exists
                try {
                    // if this doesn't throw, the folder is still not fully moved
                    ExternalDriveRoot.Instance.ParseFolder(old_full_path);
                } catch {
                    return;
                }
            }
            // here, we're not really sure if the move worked - for retry_find_folder_move_complete, the recursive size hasn't changed,
            // and we old folder still exists
            throw new IOException("could not delete " + old_full_path);
        }
        
        // note: this can only happen synchronously - otherwise, we'd end up deleting something from HDD before it was fully moved from the HDD
        public static void DeleteSyncPortableFolder(FolderItem fi, string old_full_path) {
            Debug.Assert( fi.IsFolder);

            // https://msdn.microsoft.com/en-us/library/windows/desktop/bb787874(v=vs.85).aspx
            var move_options = 4 | 16 | 512 | 1024;
            try
            {
                var temp = TemporaryDirectoryPath;
                var temp_folder = GetShell32Folder(temp);
                var folder_name = fi.Name;
                temp_folder.MoveHere(fi, move_options);

                // wait until folder dissapears from Android (the alternative would be to check all the file sizes match the original file sizes.
                // however, we'd need to do this recursively)

                var name = temp + "\\" + folder_name;
                WaitForWinFolderMoveComplete(name, old_full_path);
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

    }
}
