using Daemon.Contracts.Payloads;
using Daemon.Impl.Requests;
using Daemon.Interfaces;
using FluentResults;
using JetBrains.Annotations;
using MediatR;

namespace Daemon.Handlers;

[UsedImplicitly]
public class SubscribeChangesHandler : IRequestHandler<SubscribeChangesRequest, Result<SubscribeResult>>
{
    private readonly ISubscriptionManager _subscriptionManager;

    public SubscribeChangesHandler(ISubscriptionManager subscriptionManager)
    {
        _subscriptionManager = subscriptionManager;
    }
    
    public Task<Result<SubscribeResult>> Handle(SubscribeChangesRequest request, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(request.Directory))
            return Task.FromResult(Result.Fail<SubscribeResult>("Directory is not exist"));

        var result = new SubscribeResult
        {
            Payload = new SuccessPayload { Request = request },
            Activate = clientSession => _subscriptionManager.Subscribe(clientSession, request.Directory),
        };
        return Task.FromResult(Result.Ok(result));
    }
}