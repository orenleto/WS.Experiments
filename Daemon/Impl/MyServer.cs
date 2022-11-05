using System.Net.Sockets;
using Daemon.Configurations;
using Daemon.Interfaces;
using Daemon.NetCoreServer;

namespace Daemon.Impl;

public class MyServer : WsServer
{
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MyServer> _logger;

    public MyServer(ServerConfiguration configuration, ISubscriptionManager subscriptionManager, ILoggerFactory loggerFactory) : base(configuration.IpAddress, configuration.Port)
    {
        _subscriptionManager = subscriptionManager;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MyServer>();
    }
    
    protected override TcpSession CreateSession() => new ClientSession(this, _subscriptionManager, _loggerFactory.CreateLogger<ClientSession>());

    protected override void OnError(SocketError error)
    {
        _logger.LogError("MyServer WebSocket server caught an error with code {error}", error);
        base.OnError(error);
    }
}