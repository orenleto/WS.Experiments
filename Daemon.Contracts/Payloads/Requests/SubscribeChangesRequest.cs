using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Contracts.Payloads.Requests;

public class SubscribeChangesRequest : Request
{
    public SubscribeChangesRequest(string directory)
    {
        Directory = directory;
    }

    public SubscribeChangesRequest()
    {
        Directory = string.Empty;
    }

    [TypeIndicator] public override string Method => "SubscribeChanges-String";
    public string Directory { get; set; }
}