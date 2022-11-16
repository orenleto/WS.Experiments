using Daemon.Configurations;
using Daemon.Interfaces;
using MediatR;
using Microsoft.Extensions.Options;

namespace Daemon.Impl;

public class WebSocketServerService : IHostedService
{
    private readonly ILogger<WebSocketServerService> _logger;
    private readonly MyServer _server;

    public WebSocketServerService(
        IMediator mediator,
        ISubscriptionManager subscriptionManager,
        IOptions<ServerConfiguration> configuration,
        ILoggerFactory loggerFactory
    )
    {
        _server = new MyServer(configuration.Value, mediator, subscriptionManager, loggerFactory);
        _logger = loggerFactory.CreateLogger<WebSocketServerService>();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _server.Start();
        _logger.LogInformation("Server started at {time}", DateTime.UtcNow.ToString("U"));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Server stopped at {time}", DateTime.UtcNow.ToString("U"));
        _server.Stop();
        return Task.CompletedTask;
    }
}