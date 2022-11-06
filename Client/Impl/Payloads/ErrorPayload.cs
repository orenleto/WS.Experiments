using TypeIndicatorConverter.Core.Attribute;

namespace Client.Impl.Payloads;

internal class ErrorPayload : Payload
{
    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public PayloadType Type => PayloadType.Error;
    public string Directory { get; set; }
    public string Message { get; set; }
}