namespace Daemon.Collections;

internal class FileSystemEventArgsComparer : IEqualityComparer<FileSystemEventArgs>, IComparer<FileSystemEventArgs>
{
    public int Compare(FileSystemEventArgs x, FileSystemEventArgs y)
    {
        return !(x is RenamedEventArgs renameX && y is RenamedEventArgs renameY)
            ? (x.ChangeType, x.FullPath, x.Name).CompareTo((y.ChangeType, y.FullPath, y.Name))
            : (renameX.ChangeType, renameX.FullPath, renameX.Name, renameX.OldFullPath, renameX.OldName)
            .CompareTo((renameY.ChangeType, renameY.FullPath, renameY.Name, renameY.OldFullPath, renameY.OldName));
    }

    public bool Equals(FileSystemEventArgs x, FileSystemEventArgs? y)
    {
        return !(x is null) && !(y is null)
                            && (IsStandardFileChange(x, y) || IsDelayedFileChange(x, y));
    }

    public int GetHashCode(FileSystemEventArgs obj)
    {
        return !(obj is RenamedEventArgs renameObj)
            ? (obj.ChangeType, obj.FullPath, obj.Name).GetHashCode()
            : (renameObj.ChangeType, renameObj.FullPath, renameObj.Name, renameObj.OldFullPath, renameObj.OldName).GetHashCode();
    }

    private static bool AreFileSystemEventArgsFilePathsEqual(FileSystemEventArgs event1, FileSystemEventArgs? event2)
    {
        return event1.FullPath == event2.FullPath && event1.Name == event2.Name;
    }

    private static bool IsDelayedFileChange(FileSystemEventArgs event1, FileSystemEventArgs? event2)
    {
        return (event1.ChangeType & WatcherChangeTypes.Created) != 0
               && (event2.ChangeType & WatcherChangeTypes.Created) != 0
               && AreFileSystemEventArgsFilePathsEqual(event1, event2);
    }

    private static bool IsEqualRenamedEvent(FileSystemEventArgs event1, FileSystemEventArgs? event2)
    {
        return !(event1 is RenamedEventArgs renamedEvent1 && event2 is RenamedEventArgs renamedEvent2)
               || (renamedEvent1.OldFullPath == renamedEvent2.OldFullPath
                   && renamedEvent1.OldName == renamedEvent2.OldName);
    }

    private static bool IsNameAndEventEqual(FileSystemEventArgs event1, FileSystemEventArgs? event2)
    {
        return (event1.ChangeType & event2.ChangeType) != 0 && AreFileSystemEventArgsFilePathsEqual(event1, event2);
    }

    private static bool IsStandardFileChange(FileSystemEventArgs event1, FileSystemEventArgs? event2)
    {
        return IsNameAndEventEqual(event1, event2) && IsEqualRenamedEvent(event1, event2);
    }
}