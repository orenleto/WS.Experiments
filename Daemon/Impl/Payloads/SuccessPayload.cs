using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Impl.Payloads;

internal class SuccessPayload : Payload
{
    public SuccessPayload(string directory)
    {
        Directory = directory;
    }

    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public PayloadType Type => PayloadType.Success;
    public string Directory { get; set; }
}