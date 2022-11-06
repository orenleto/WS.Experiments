using System.Text.Json.Serialization;

namespace Client.Impl.Payloads;

[JsonConverter(typeof(TypeIndicatorConverter.TextJson.TypeIndicatorConverter<Payload>))]
internal abstract class Payload
{
    string Method { get; }
    PayloadType Type { get; }
}