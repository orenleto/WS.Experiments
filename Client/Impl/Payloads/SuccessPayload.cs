using Client.Impl.Requests;
using TypeIndicatorConverter.Core.Attribute;

namespace Client.Impl.Payloads;

internal class SuccessPayload : Payload
{
    [TypeIndicator] public override PayloadType Type => PayloadType.Success;
    public Request Request { get; init; }
}