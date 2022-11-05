using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Daemon.Impl.Payloads;
using Daemon.Interfaces;
using Daemon.NetCoreServer;

namespace Daemon.Impl;

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
        _logger.LogInformation("New session with Id {id} connected!", Id);
    }

    public override void OnWsDisconnecting()
    {
        _subscriptionManager.UnsubscribeAll(this);
        _logger.LogInformation("Session with Id {id} disconnected!", Id);
    }

    public override void OnWsReceived(byte[] buffer, long offset, long size)
    {
        _logger.LogInformation("Incoming from Id {id} {byteCount}bytes", Id, size);
        try
        {
            var request = JsonSerializer.Deserialize<Request>(new ArraySegment<byte>(buffer, (int)offset, (int)size));
            if (request is SubscribeChangesRequest subscribeChangesRequest)
            {
                var directory = subscribeChangesRequest.Directory;
                if (Directory.Exists(directory))
                {
                    var payload = new SuccessEvent(directory);
                    SendTextAsync(JsonSerializer.SerializeToUtf8Bytes(payload));
                    _subscriptionManager.Subscribe(this, directory);
                }
                else
                {
                    _logger.LogError("Try subscribe on non-existent directory {path}", directory);
                    var payload = new ErrorEvent(directory, "Directory is not exists");
                    SendTextAsync(JsonSerializer.SerializeToUtf8Bytes(payload));
                }
            }
            else
            {
                _logger.LogError("Unimplemented method handler: {method}", request.Method);
                var payload = new ServerException(request.Method, "Unimplemented method");
                SendTextAsync(JsonSerializer.SerializeToUtf8Bytes(payload));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected exception: {ex} ({body})", ex, Encoding.UTF8.GetString(new ArraySegment<byte>(buffer, (int)offset, (int)size)));
            var payload = new ServerException("Unknown method", "Unexpected exception");
            SendTextAsync(JsonSerializer.SerializeToUtf8Bytes(payload));
        }
    }

    public void Send(EventArgs eventArgs)
    {
        try
        {
            var payload = eventArgs switch
            {
                RenamedEventArgs renamedEventArgs => new FileSystemEventPayload(renamedEventArgs.ChangeType, renamedEventArgs.FullPath, renamedEventArgs.Name, renamedEventArgs.OldName),
                FileSystemEventArgs fileSystemEventArgs => new FileSystemEventPayload(fileSystemEventArgs.ChangeType, fileSystemEventArgs.FullPath, fileSystemEventArgs.Name, null),
                _ => throw new ArgumentOutOfRangeException(nameof(eventArgs), "Unexpected type")
            };
            SendTextAsync(JsonSerializer.SerializeToUtf8Bytes(payload));
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected exception: {ex}", ex);
            var payload = new ServerException("SubscribeChanges-String", "Unexpected exception");
            SendTextAsync(JsonSerializer.SerializeToUtf8Bytes(payload));
        }
    }

    protected override void OnError(SocketError error)
    {
        _logger.LogError("Chat WebSocket session caught an error with code {error}", error);
    }
}