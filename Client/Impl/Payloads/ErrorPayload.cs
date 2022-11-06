using Client.Impl.Requests;
using TypeIndicatorConverter.Core.Attribute;

namespace Client.Impl.Payloads;

public class ErrorPayload : Payload
{
    [TypeIndicator] public override PayloadType Type => PayloadType.Error;
    public Request Request { get; init; }
    public string[] Errors { get; init; }
}