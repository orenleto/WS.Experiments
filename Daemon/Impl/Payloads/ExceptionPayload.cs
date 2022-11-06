using Daemon.Impl.Requests;
using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Impl.Payloads;

public class ExceptionPayload : Payload
{
    [TypeIndicator] public override PayloadType Type => PayloadType.Exception;
    public Request? Request { get; set; }
    public string Message { get; set; }
}