using System;

namespace external_drive_lib {
    public interface IFile
    {
        // guaranteed to NOT THROW
        string Name { get; }
        bool Exists { get; }
        long Size { get; }
        DateTime LastWriteTime { get; }
        string FullPath { get; }

        IDrive Drive { get; }
        IFolder Folder { get; }
        
        // note: dest_path can be to another external drive
        // throws if there's an error
        //
        // note: move can be implemented via copy() + delete()
        //
        // note: overwrites if destination exists
        void CopyAsync(string destPath);
        void DeleteAsync();
        void CopySync(string destPath);
        void DeleteSync();
    }

}

