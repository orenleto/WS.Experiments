using Daemon.Configurations;
using Daemon.Interfaces;
using Microsoft.Extensions.Options;

namespace Daemon.Impl;

public class WebSocketServerService : IHostedService, IDisposable
{
    private readonly MyServer _server;
    private readonly ILogger<WebSocketServerService> _logger;

    public WebSocketServerService(
        ISubscriptionManager subscriptionManager,
        IOptions<ServerConfiguration> configuration,
        ILoggerFactory loggerFactory
    )
    {
        _server = new MyServer(configuration.Value, subscriptionManager, loggerFactory);
        _logger = loggerFactory.CreateLogger<WebSocketServerService>();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Server started at {time}", DateTime.UtcNow.ToString("U"));
        _server.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Server stopped at {time}", DateTime.UtcNow.ToString("U"));
        _server.Stop();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }
}