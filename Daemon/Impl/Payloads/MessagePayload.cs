using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Impl.Payloads;

public class MessagePayload : Payload
{
    public static MessagePayload Create(WatcherChangeTypes changeType, string fullPath, string? name, string? oldName) => new MessagePayload
    {
        ChangeType = changeType,
        FullPath = fullPath,
        Name = name,
        OldName = oldName,
    };

    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public override PayloadType Type => PayloadType.Message;
    public WatcherChangeTypes ChangeType { get; set; }
    public string FullPath { get; set; }
    public string? Name { get; set; }
    public string? OldName { get; set; }
}