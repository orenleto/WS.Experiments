using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Contracts.Payloads.Events;

public class FileSystemEvent : Payload
{
    [TypeIndicator] public override PayloadType Type => PayloadType.Message;
    public WatcherChangeTypes ChangeType { get; init; }
    public string FullPath { get; init; }
    public string? Name { get; init; }
    public string? OldName { get; init; }
}