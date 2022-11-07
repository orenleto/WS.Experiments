using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Impl.Payloads;

public class FileSystemEvent : Payload
{
    public static FileSystemEvent Create(WatcherChangeTypes changeType, string fullPath, string? name, string? oldName)
    {
        return new FileSystemEvent
        {
            ChangeType = changeType,
            FullPath = fullPath,
            Name = name,
            OldName = oldName,
        };
    }
    
    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public override PayloadType Type => PayloadType.Message;
    public WatcherChangeTypes ChangeType { get; init; }
    public string FullPath { get; init; }
    public string? Name { get; init; }
    public string? OldName { get; init; }
}