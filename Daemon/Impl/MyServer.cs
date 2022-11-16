using System.Net.Sockets;
using Daemon.Configurations;
using Daemon.Interfaces;
using MediatR;
using NetCoreServer;

namespace Daemon.Impl;

public class MyServer : WsServer
{
    private readonly ILogger<MyServer> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMediator _mediator;
    private readonly ISubscriptionManager _subscriptionManager;

    public MyServer(
        ServerConfiguration configuration,
        IMediator mediator,
        ISubscriptionManager subscriptionManager,
        ILoggerFactory loggerFactory
    ) : base(configuration.IpAddress, configuration.Port)
    {
        _mediator = mediator;
        _subscriptionManager = subscriptionManager;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MyServer>();
    }

    protected override TcpSession CreateSession()
    {
        return new ClientSession(this, _mediator, _subscriptionManager, _loggerFactory.CreateLogger<ClientSession>());
    }

    protected override void OnStarted()
    {
        base.OnStarted();
        _logger.LogInformation("Start listening {Address}:{Port}", Address, Port);
    }

    protected override void OnError(SocketError error)
    {
        _logger.LogError("MyServer WebSocket server caught an error with code {error}", error);
        base.OnError(error);
    }
}