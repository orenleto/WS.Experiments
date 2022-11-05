using System.Text.Json.Serialization;

namespace Daemon.Impl.Payloads;

[JsonConverter(typeof(TypeIndicatorConverter.TextJson.TypeIndicatorConverter<Request>))]
public abstract class Request
{
    public string Method { get; }
}