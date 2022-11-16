using System.Text.Json.Serialization;
using TypeIndicatorConverter.TextJson;

namespace Daemon.Contracts.Payloads;

[JsonConverter(typeof(TypeIndicatorConverter<Payload>))]
public abstract class Payload
{
    public abstract PayloadType Type { get; }
}