using System.Text.Json.Serialization;
using Daemon.Impl.Payloads;
using FluentResults;
using MediatR;

namespace Daemon.Impl.Requests;

[JsonConverter(typeof(TypeIndicatorConverter.TextJson.TypeIndicatorConverter<Request>))]
public abstract class Request : IRequest<Result<Payload>>
{
    public abstract string Method { get; }
}