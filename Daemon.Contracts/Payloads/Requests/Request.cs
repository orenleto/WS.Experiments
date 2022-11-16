using System.Text.Json.Serialization;
using TypeIndicatorConverter.TextJson;

namespace Daemon.Contracts.Payloads.Requests;

[JsonConverter(typeof(TypeIndicatorConverter<Request>))]
public abstract class Request
{
    public abstract string Method { get; }
}