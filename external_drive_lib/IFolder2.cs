namespace external_drive_lib
{
    // this is not exposed - so that users only use IFile.copy() instead
    internal interface IFolder2 : IFolder
    {
        // this is the only way to make sure a file gets copied where it should, no matter where the destination is
        // (since we could copy a file from android to sd card or whereever)
        void CopyFile(IFile file, bool synchronous);
    }
}