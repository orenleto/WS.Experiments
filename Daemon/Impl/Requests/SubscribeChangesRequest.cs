using Daemon.Contracts.Payloads.Requests;
using FluentResults;
using MediatR;
using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Impl.Requests;

public class SubscribeChangesRequest : Request, IRequest<Result<SubscribeResult>>
{
    [TypeIndicator] public override string Method => "SubscribeChanges-String";
    public string Directory { get; set; }
}