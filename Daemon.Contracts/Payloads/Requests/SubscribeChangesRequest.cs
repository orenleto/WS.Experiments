using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Contracts.Payloads.Requests;

public class SubscribeChangesRequest : Request
{
    [TypeIndicator] public override string Method => "SubscribeChanges-String";
    public string Directory { get; set; }

    public SubscribeChangesRequest(string directory)
    {
        Directory = directory;
    }

    public SubscribeChangesRequest()
    {
        Directory = string.Empty;
    }
}