using System.Collections.Generic;

namespace external_drive_lib
{
    public interface IDrive
    {
        // drive type
        PortableDriveType DriveType { get; }
        // this is the drive path, such as "c:\" - however, for non-conventional drives, it can be a really weird path
        string RootName { get; }
        // the drive's Unique ID - it is the same between program runs
        string UniqueId { get; }
        // a friendly name for the drive
        string FriendlyName { get; }


        // returns true if the drive is connected
        // note: not as a property, since this could actually take time to find out - we don't want to break debugging
        bool IsConnected();
        // returns true if the drive is available - note that the drive can be connected via USB, but locked (thus, not available)
        bool IsAvailable();
        // file manipulation
        IEnumerable<IFolder> EnumerateFolders();
        IEnumerable<IFile> EnumerateFiles();
        // throws on failure
        IFile ParseFile(string path);
        IFolder ParseFolder(string path);
        // returns null on failure
        IFile TryParseFile(string path);
        IFolder TryParseFolder(string path);
        // creates the full path to the folder
        IFolder CreateFolder(string folder);
    }

}
