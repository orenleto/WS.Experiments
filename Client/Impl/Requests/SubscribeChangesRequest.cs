using TypeIndicatorConverter.Core.Attribute;

namespace Client.Impl.Requests;

public class SubscribeChangesRequest : Request
{
    public SubscribeChangesRequest(string directory)
    {
        Directory = directory;
    }

    [TypeIndicator] public string Method => "SubscribeChanges-String";
    public string Directory { get;  }
}