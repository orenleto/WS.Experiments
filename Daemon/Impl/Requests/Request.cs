using System.Text.Json.Serialization;
using FluentResults;
using MediatR;

namespace Daemon.Impl.Requests;

[JsonConverter(typeof(TypeIndicatorConverter.TextJson.TypeIndicatorConverter<Request>))]
public abstract class Request : IRequest<Result<SubscribeResult>>
{
    public abstract string Method { get; }
}