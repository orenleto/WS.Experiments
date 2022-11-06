using Daemon.Impl.Payloads;

namespace Daemon.Impl.Requests;

public class SubscribeResult
{
    public SuccessPayload Payload { get; init; }
    public Action<ClientSession> Activate { get; init; }
}