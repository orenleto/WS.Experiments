using Daemon.Impl.Payloads;
using Daemon.Impl.Requests;
using Daemon.Interfaces;
using FluentResults;
using JetBrains.Annotations;
using MediatR;

namespace Daemon.Handlers;

[UsedImplicitly]
public class SubscribeChangesHandler : IRequestHandler<SubscribeChangesRequest, Result<Payload>>
{
    private readonly ISubscriptionManager _subscriptionManager;

    public SubscribeChangesHandler(ISubscriptionManager subscriptionManager)
    {
        _subscriptionManager = subscriptionManager;
    }
    
    public Task<Result<Payload>> Handle(SubscribeChangesRequest request, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(request.Directory))
            return Task.FromResult(Result.Fail<Payload>("Directory is not exist"));

        var successPayload = new SuccessPayload {
            Request = request,
            Callback = clientSession => _subscriptionManager.Subscribe(clientSession, request.Directory)
        };
        return Task.FromResult(Result.Ok<Payload>(successPayload));
    }
}