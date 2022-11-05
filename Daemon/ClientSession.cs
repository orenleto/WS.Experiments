using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Daemon.NetCoreServer;

namespace Daemon;

public class ClientSession : WsSession
{
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ILogger<ClientSession> _logger;

    public ClientSession(WsServer server, ISubscriptionManager subscriptionManager, ILogger<ClientSession> logger) : base(server)
    {
        _subscriptionManager = subscriptionManager;
        _logger = logger;
    }
    
    public override void OnWsConnected(HttpRequest request)
    {
        _logger.LogInformation("Chat WebSocket session with Id {id} connected!", Id);
    }

    public override void OnWsDisconnected()
    {
        _subscriptionManager.UnsubscribeAll(this);
        _logger.LogInformation("Chat WebSocket session with Id {id} disconnected!", Id);
    }

    public override void OnWsReceived(byte[] buffer, long offset, long size)
    {
        var message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
        _logger.LogInformation("Incoming from Id {id}: {message}", Id, message);

        var jsonTree = JsonDocument.Parse(message);
        var methodName = jsonTree.RootElement.GetProperty("MethodName").GetString();
        _logger.LogInformation("Method name: " + methodName);

        if (methodName == "SubscribeChanges-String")
        {
            var directory = jsonTree.RootElement.GetProperty("Path").GetString()!;
            _subscriptionManager.Subscribe(this, directory);
        }
    }

    public ValueTask SendAsync(EventArgs eventArgs)
    {
        var @event = eventArgs switch
        {
            RenamedEventArgs renamedEventArgs => new FileSystemEvent(renamedEventArgs.ChangeType, renamedEventArgs.FullPath, renamedEventArgs.Name, renamedEventArgs.OldName),
            FileSystemEventArgs fileSystemEventArgs => new FileSystemEvent(fileSystemEventArgs.ChangeType, fileSystemEventArgs.FullPath, fileSystemEventArgs.Name, null),
            _ => throw new ArgumentOutOfRangeException(nameof(eventArgs), "Unexpected type")
        };
        SendTextAsync(JsonSerializer.SerializeToUtf8Bytes(@event));
        return ValueTask.CompletedTask;
    }

    protected override void OnError(SocketError error)
    {
        _logger.LogError("Chat WebSocket session caught an error with code {error}", error);
    }
}

internal record FileSystemEvent(WatcherChangeTypes ChangeType, string FullPath, string? Name, string? OldName);