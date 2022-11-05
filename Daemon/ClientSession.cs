using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Daemon.NetCoreServer;

namespace Daemon;

public class ClientSession : WsSession
{
    private readonly SubscriptionManager _subscriptionManager;

    public ClientSession(WsServer server, SubscriptionManager subscriptionManager) : base(server)
    {
        _subscriptionManager = subscriptionManager;
    }
    
    public override void OnWsConnected(HttpRequest request)
    {
        Console.WriteLine($"Chat WebSocket session with Id {Id} connected!");
    }

    public override void OnWsDisconnected()
    {
        _subscriptionManager.UnsubscribeAll(this);
        Console.WriteLine($"Chat WebSocket session with Id {Id} disconnected!");
    }

    public override void OnWsReceived(byte[] buffer, long offset, long size)
    {
        var message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
        Console.WriteLine($"Incoming from Id {Id}: {message}");

        var jsonTree = JsonDocument.Parse(message);
        var methodName = jsonTree.RootElement.GetProperty("MethodName").GetString();
        Console.WriteLine("Method name: " + methodName);

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
        Console.WriteLine($"Chat WebSocket session caught an error with code {error}");
    }
}

internal record FileSystemEvent(WatcherChangeTypes ChangeType, string FullPath, string? Name, string? OldName);