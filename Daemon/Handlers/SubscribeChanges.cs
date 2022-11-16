using Daemon.Impl;
using Daemon.Interfaces;
using FluentResults;
using JetBrains.Annotations;
using MediatR;

namespace Daemon.Handlers;

public abstract class SubscribeChanges
{
    public class Command : IRequest<Result>
    {
        public Command(string directory, ClientSession clientSession)
        {
            Directory = directory;
            ClientSession = clientSession;
        }

        public string Directory { get; }
        public ClientSession ClientSession { get; }
    }

    [UsedImplicitly]
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly ISubscriptionManager _subscriptionManager;

        public Handler(ISubscriptionManager subscriptionManager)
        {
            _subscriptionManager = subscriptionManager;
        }

        public Task<Result> Handle(Command command, CancellationToken cancellationToken)
        {
            if (!Directory.Exists(command.Directory))
                return Task.FromResult(Result.Fail("Directory is not exist"));

            _subscriptionManager.Subscribe(command.ClientSession, command.Directory);
            return Task.FromResult(Result.Ok());
        }
    }
}