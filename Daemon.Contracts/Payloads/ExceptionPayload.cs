using Daemon.Contracts.Payloads.Requests;
using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Contracts.Payloads;

public class ExceptionPayload : Payload
{
    [TypeIndicator] public override PayloadType Type => PayloadType.Exception;
    public Request? Request { get; init; }
    public string Message { get; init; }
}