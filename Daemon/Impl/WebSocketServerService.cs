using Daemon.Configurations;
using Daemon.Interfaces;
using Microsoft.Extensions.Options;

namespace Daemon.Impl;

public class WebSocketServerService : IHostedService, IDisposable
{
    private readonly MyServer _server;

    public WebSocketServerService(
        ISubscriptionManager subscriptionManager,
        IOptions<ServerConfiguration> configuration,
        ILoggerFactory loggerFactory
    )
    {
        _server = new MyServer(configuration.Value, subscriptionManager, loggerFactory);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _server.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _server.Stop();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }
}