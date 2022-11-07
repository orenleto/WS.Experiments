using System.Text.Json.Serialization;

namespace Daemon.Contracts.Payloads.Requests;

[JsonConverter(typeof(TypeIndicatorConverter.TextJson.TypeIndicatorConverter<Request>))]
public abstract class Request
{
    public abstract string Method { get; }
}