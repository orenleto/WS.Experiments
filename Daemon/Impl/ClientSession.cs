using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Daemon.Impl.Payloads;
using Daemon.Impl.Requests;
using Daemon.Interfaces;
using Daemon.NetCoreServer;
using MediatR;

namespace Daemon.Impl;

public class ClientSession : WsSession
{
    private readonly IMediator _mediator;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ILogger<ClientSession> _logger;

    public ClientSession(WsServer server, IMediator mediator, ISubscriptionManager subscriptionManager, ILogger<ClientSession> logger) : base(server)
    {
        _mediator = mediator;
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

    public override async void OnWsReceived(byte[] buffer, long offset, long size)
    {
        _logger.LogInformation("Incoming from Id {id} {byteCount}bytes", Id, size);
        try
        {
            var request = JsonSerializer.Deserialize<Request>(new ArraySegment<byte>(buffer, (int)offset, (int)size));
            if (request is not null)
            {
                var result = await _mediator.Send(request);
                if (result.IsSuccess)
                {
                    _logger.LogInformation("Successfully complete {@request}", request);
                    var subscribeResult = result.Value;
                    SendTextAsync(JsonSerializer.SerializeToUtf8Bytes(subscribeResult.Payload));
                    subscribeResult.Activate(this);
                }
                else
                {
                    _logger.LogError("Failure complete {@request}: {@reasons}", request, result.Reasons.Select(r => r.Message));
                    var payload = new ErrorPayload
                    {
                        Request = request,
                        Errors = result.Errors.Select(e => e.Message).ToArray(),
                    };
                    SendTextAsync(JsonSerializer.SerializeToUtf8Bytes(payload));
                }
            }
            else
            {
                _logger.LogError("Unimplemented method handler: {method}", request.Method);
                var payload = new ExceptionPayload
                {
                    Request = request,
                    Message = "Unrecognized request: " + Encoding.UTF8.GetString(new ArraySegment<byte>(buffer, (int)offset, (int)size)),
                };
                SendTextAsync(JsonSerializer.SerializeToUtf8Bytes(payload));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected exception: {ex} ({body})", ex,
                Encoding.UTF8.GetString(new ArraySegment<byte>(buffer, (int)offset, (int)size)));
            var payload = new ExceptionPayload
            {
                Request = null,
                Message = ex.Message,
            };
            SendTextAsync(JsonSerializer.SerializeToUtf8Bytes(payload));
        }
    }

    public void Send(EventArgs eventArgs)
    {
        try
        {
            var payload = eventArgs switch
            {
                RenamedEventArgs rename => MessagePayload.Create(rename.ChangeType, rename.FullPath, rename.Name, rename.OldName),
                FileSystemEventArgs fileEvent => MessagePayload.Create(fileEvent.ChangeType, fileEvent.FullPath, fileEvent.Name, null),
                _ => throw new ArgumentOutOfRangeException(nameof(eventArgs), "Unexpected type")
            };
            SendTextAsync(JsonSerializer.SerializeToUtf8Bytes(payload));
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected exception: {ex}", ex);
            var payload = new ExceptionPayload
            {
                Request = null,
                Message = ex.Message,
            };
            SendTextAsync(JsonSerializer.SerializeToUtf8Bytes(payload));
        }
    }

    protected override void OnError(SocketError error)
    {
        _logger.LogError("Chat WebSocket session caught an error with code {error}", error);
    }
}