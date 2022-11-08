using System.Net.WebSockets;
using Daemon.Contracts.Interfaces;
using Polly;
using Polly.Contrib.WaitAndRetry;

namespace Client.Impl;

public class WebSocketTransport : ITransport
{
    private static readonly IAsyncPolicy _retryPolicy = Policy
        .Handle<WebSocketException>()
        .WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 4));

    private readonly Uri _uri;
    private readonly ArraySegment<byte> _buffer;
    
    private Func<ArraySegment<byte>, CancellationToken, Task>? _continuations;
    private ClientWebSocket? _webSocket;

    public WebSocketTransport(Uri uri)
    {
        _uri = uri;
        _buffer = new ArraySegment<byte>(new byte[16 * 1024]);
    }

    public async Task Connect(CancellationToken cancellationToken)
    {
        if (_webSocket is null)
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                _webSocket = new ClientWebSocket();
                await _webSocket.ConnectAsync(_uri, cancellationToken);
            });
        }
        else if (_webSocket.State == WebSocketState.Open)
        {
        }
        else
        {
            // todo: использовать ResourceManager для текста ошибки
            throw new InvalidOperationException("Unable to connect closed websocket");
        }
    }
    
    public async Task SendAsync(ArraySegment<byte> data, CancellationToken cancellationToken)
    {
        if (_webSocket!.State != WebSocketState.Open)
            throw new InvalidOperationException("Unable to send message by invalid websocket state");
        await _webSocket.SendAsync(data, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, cancellationToken);
    }
    
    public async Task<ArraySegment<byte>> ReceiveAsync(CancellationToken cancellationToken)
    {
        var receiveResult = await _webSocket!.ReceiveAsync(_buffer, cancellationToken);
        if (_webSocket.State == WebSocketState.Open && receiveResult.MessageType != WebSocketMessageType.Close)
        {
            return _buffer[..receiveResult.Count];
        }

        if (_webSocket.State == WebSocketState.CloseReceived && receiveResult.MessageType == WebSocketMessageType.Close)
        {
            await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge Close frame", CancellationToken.None);
        }

        return ArraySegment<byte>.Empty;
    }

    public void Dispose()
    {
        _webSocket?.Dispose();
    }
}