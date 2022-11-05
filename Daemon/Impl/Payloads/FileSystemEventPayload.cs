using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Impl.Payloads;

internal class FileSystemEventPayload : Payload
{
    public FileSystemEventPayload(WatcherChangeTypes changeType, string fullPath, string? name, string? oldName)
    {
        ChangeType = changeType;
        FullPath = fullPath;
        Name = name;
        OldName = oldName;
    }

    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public PayloadType Type => PayloadType.Message;
    public WatcherChangeTypes ChangeType { get; set; }
    public string FullPath { get; set; }
    public string? Name { get; set; }
    public string? OldName { get; set; }
}