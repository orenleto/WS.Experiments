using System.Text.Json.Serialization;

namespace Daemon.Contracts.Payloads;

[JsonConverter(typeof(TypeIndicatorConverter.TextJson.TypeIndicatorConverter<Payload>))]
public abstract class Payload
{
    public abstract PayloadType Type { get; }
}