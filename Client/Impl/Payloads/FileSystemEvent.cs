namespace Client.Impl.Payloads;

public record FileSystemEvent(WatcherChangeTypes ChangeType, string FullPath, string? Name, string? OldName);