using TypeIndicatorConverter.Core.Attribute;

namespace Client.Impl.Payloads;

public class FileSystemEvent : Payload
{
    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public override PayloadType Type => PayloadType.Message;

    public WatcherChangeTypes ChangeType { get; init; }
    public string FullPath { get; init; }
    public string? Name { get; init; }
    public string? OldName { get; init; }
}