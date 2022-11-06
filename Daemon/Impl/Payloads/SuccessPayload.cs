using Daemon.Impl.Requests;
using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Impl.Payloads;

public class SuccessPayload : Payload
{
    [TypeIndicator] public override PayloadType Type => PayloadType.Success;
    public Request Request { get; set; }
    internal Action<ClientSession> Callback { get; set; }
}