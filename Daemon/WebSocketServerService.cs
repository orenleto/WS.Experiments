using System.Net;

namespace Daemon;

public class WebSocketServerService : IHostedService, IDisposable
{
    private readonly ILogger<WebSocketServerService> _logger;
    private readonly MyServer _server;

    public WebSocketServerService(ILogger<WebSocketServerService> logger)
    {
        _logger = logger;
        _server = new MyServer(IPAddress.Loopback, 5000);
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