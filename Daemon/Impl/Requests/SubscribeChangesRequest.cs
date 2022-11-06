using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Impl.Requests;

public class SubscribeChangesRequest : Request
{
    [TypeIndicator] public override string Method => "SubscribeChanges-String";
    public string Directory { get; set; }
}