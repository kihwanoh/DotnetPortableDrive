using System.Collections.Generic;

namespace external_drive_lib
{
    public interface IFolder
    {
        // guaranteed to NOT THROW
        string Name { get; }
        bool Exists { get; }
        string FullPath { get; }
        IDrive Drive { get; }

        // can return null if this is a folder from the drive
        IFolder Parent { get; }
        IEnumerable<IFile> EnumerateFiles();
        IEnumerable<IFolder> EnumerateChildFolders();

        // throws if there's an error
        void DeleteAsync();
        void DeleteSync();
    }
}
