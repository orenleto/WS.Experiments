namespace Client;

public record FileSystemEvent(WatcherChangeTypes ChangeType, string FullPath, string? Name, string? OldName);