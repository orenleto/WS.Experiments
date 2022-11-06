using TypeIndicatorConverter.Core.Attribute;

namespace Client.Impl.Requests;

public class SubscribeChangesRequest : Request
{
    [TypeIndicator] public string Method => "SubscribeChanges-String";
    public string Directory { get; set; }
}