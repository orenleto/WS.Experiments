using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Impl.Payloads;

public class SubscribeChangesRequest : Request
{
    [TypeIndicator] public string Method => "SubscribeChanges-String";
    public string Directory { get; set; }
}