using System.Text.Json.Serialization;

namespace Client.Impl.Requests;

[JsonConverter(typeof(TypeIndicatorConverter.TextJson.TypeIndicatorConverter<Request>))]
public abstract class Request
{
    public string Method { get; }
}