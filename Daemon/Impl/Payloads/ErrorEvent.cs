using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Impl.Payloads;

internal class ErrorEvent : Payload
{
    public ErrorEvent(string directory, string message)
    {
        Message = message;
        Directory = directory;
    }

    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public PayloadType Type => PayloadType.Error;
    public string Directory { get; set; }
    public string Message { get; set; }
}