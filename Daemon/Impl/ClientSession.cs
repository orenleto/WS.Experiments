using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Daemon.Interfaces;
using Daemon.NetCoreServer;
using TypeIndicatorConverter.Core.Attribute;

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
        _logger.LogInformation("Chat WebSocket session with Id {id} connected!", Id);
    }

    public override void OnWsDisconnected()
    {
        _subscriptionManager.UnsubscribeAll(this);
        _logger.LogInformation("Chat WebSocket session with Id {id} disconnected!", Id);
    }

    public override void OnWsReceived(byte[] buffer, long offset, long size)
    {
        _logger.LogInformation("Incoming from Id {id} {byteCount}bytes", Id, size);
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
                _logger.LogError("Trying subscribe on unexists directory {path}", directory);
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

    public ValueTask SendAsync(EventArgs eventArgs)
    {
        var payload = eventArgs switch
        {
            RenamedEventArgs renamedEventArgs => new FileSystemEventPayload(renamedEventArgs.ChangeType, renamedEventArgs.FullPath, renamedEventArgs.Name, renamedEventArgs.OldName),
            FileSystemEventArgs fileSystemEventArgs => new FileSystemEventPayload(fileSystemEventArgs.ChangeType, fileSystemEventArgs.FullPath, fileSystemEventArgs.Name, null),
            _ => throw new ArgumentOutOfRangeException(nameof(eventArgs), "Unexpected type")
        };
        SendTextAsync(JsonSerializer.SerializeToUtf8Bytes(payload));
        return ValueTask.CompletedTask;
    }

    protected override void OnError(SocketError error)
    {
        _logger.LogError("Chat WebSocket session caught an error with code {error}", error);
    }
}

[JsonConverter(typeof(TypeIndicatorConverter.TextJson.TypeIndicatorConverter<Request>))]
public abstract class Request
{
    public string Method { get; }
}

public class SubscribeChangesRequest : Request
{
    [TypeIndicator] public string Method => "SubscribeChanges-String";
    public string Directory { get; set; }
}

internal enum PayloadType
{
    Exception = -1,
    Success = 1,
    Message = 2,
    Error = 3,
}

[JsonConverter(typeof(TypeIndicatorConverter.TextJson.TypeIndicatorConverter<Payload>))]
internal abstract class Payload
{
    private string Method { get; }
    private PayloadType Type { get; }
}

internal class SuccessEvent : Payload
{
    public SuccessEvent(string directory)
    {
        Directory = directory;
    }

    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public PayloadType Type => PayloadType.Success;
    public string Directory { get; set; }
}

internal class FileSystemEventPayload : Payload
{
    public FileSystemEventPayload(WatcherChangeTypes changeType, string fullPath, string? name, string? oldName)
    {
        ChangeType = changeType;
        FullPath = fullPath;
        Name = name;
        OldName = oldName;
    }

    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public PayloadType Type => PayloadType.Message;
    public WatcherChangeTypes ChangeType { get; set; }
    public string FullPath { get; set; }
    public string? Name { get; set; }
    public string? OldName { get; set; }
}

internal class ErrorEvent : Payload
{
    public ErrorEvent(string directory, string message)
    {
        Message = message;
        Directory = directory;
    }

    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public PayloadType Type => PayloadType.Error;
    public string Directory { get; set; }
    public string Message { get; set; }
}

internal class ServerException : Payload
{
    public ServerException(string method, string message)
    {
        Method = method;
        Message = message;
    }
    
    [TypeIndicator] public PayloadType Type => PayloadType.Exception;
    public string Method { get; }
    public string Message { get; }
}