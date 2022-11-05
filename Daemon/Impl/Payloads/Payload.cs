using System.Text.Json.Serialization;

namespace Daemon.Impl.Payloads;

[JsonConverter(typeof(TypeIndicatorConverter.TextJson.TypeIndicatorConverter<Payload>))]
internal abstract class Payload
{
    private string Method { get; }
    private PayloadType Type { get; }
}