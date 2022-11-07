using Daemon.Contracts.Payloads.Requests;
using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Contracts.Payloads;

public class SuccessPayload : Payload
{
    [TypeIndicator] public override PayloadType Type => PayloadType.Success;
    public Request Request { get; init; }
}