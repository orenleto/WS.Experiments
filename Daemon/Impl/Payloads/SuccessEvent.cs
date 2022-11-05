using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Impl.Payloads;

internal class SuccessEvent : Payload
{
    public SuccessEvent(string directory)
    {
        Directory = directory;
    }

    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public PayloadType Type => PayloadType.Success;
    public string Directory { get; set; }
}