using TypeIndicatorConverter.Core.Attribute;

namespace Client.Impl.Payloads;

internal class SuccessPayload : Payload
{
    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public PayloadType Type => PayloadType.Success;
    public string Directory { get; set; }
}