using TypeIndicatorConverter.Core.Attribute;

namespace Client.Impl.Payloads;

internal class MessagePayload : Payload
{
    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public override PayloadType Type => PayloadType.Message;
    public WatcherChangeTypes ChangeType { get; set; }
    public string FullPath { get; set; }
    public string? Name { get; set; }
    public string? OldName { get; set; }
}