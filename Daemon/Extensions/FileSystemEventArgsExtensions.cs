using Daemon.Collections;

namespace Daemon.Extensions;

internal static class FileSystemEventArgsExtensions
{
    private static readonly FileSystemEventArgsComparer _comparer = new();

    public static bool IsDuplicate(this FileSystemEventArgs event1, FileSystemEventArgs? event2)
    {
        return _comparer.Equals(event1, event2);
    }
}