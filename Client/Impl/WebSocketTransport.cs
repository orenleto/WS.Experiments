using System.Net.WebSockets;
using Client.Interfaces;
using Polly;
using Polly.Contrib.WaitAndRetry;

namespace Client.Impl;

public class WebSocketTransport : ITransport
{
    private static readonly IAsyncPolicy _retryPolicy = Policy
        .Handle<WebSocketException>()
        .WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 4));

    private readonly Uri _uri;
    private readonly CancellationTokenSource _transportTokenSource;
    private readonly ManualResetEventSlim _initializedEvent = new();
    private readonly ManualResetEventSlim _stopProcessingLoopEvent = new();
    
    private Func<ArraySegment<byte>, CancellationToken, Task>? _continuations;
    private ClientWebSocket? _webSocket;
    
    public WebSocketTransport(Uri uri, CancellationToken cancellationToken)
    {
        _uri = uri;
        _transportTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    public async Task Listen(Func<ArraySegment<byte>, CancellationToken, Task> continuation)
    {
        if (_webSocket is null)
        {
            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    _webSocket = new ClientWebSocket();
                    await _webSocket.ConnectAsync(_uri, _transportTokenSource.Token);
                    _continuations += continuation;
                    _initializedEvent.Set();
                });
            }
            catch (WebSocketException)
            {
                _transportTokenSource.Cancel();
                throw;
            }

            _ = Task.Run(() => SocketProcessingLoopAsync().ConfigureAwait(false));
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

    public void Stop()
    {
        _transportTokenSource.Cancel();
        _stopProcessingLoopEvent.Wait();
    }
    
    public async Task SendAsync(ArraySegment<byte> data)
    {
        _initializedEvent.Wait(_transportTokenSource.Token);
        if (_webSocket!.State != WebSocketState.Open)
            throw new InvalidOperationException("Unable to send message by invalid websocket state");
        await _webSocket.SendAsync(data, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, _transportTokenSource.Token);
    }

    private async Task<ArraySegment<byte>> ReceiveAsync(ArraySegment<byte> buffer)
    {
        var receiveResult = await _webSocket!.ReceiveAsync(buffer, _transportTokenSource.Token);
        if (_webSocket.State == WebSocketState.Open && receiveResult.MessageType != WebSocketMessageType.Close)
        {
           return new ArraySegment<byte>(buffer.Array!, 0, receiveResult.Count);
        }

        if (_webSocket.State == WebSocketState.CloseReceived && receiveResult.MessageType == WebSocketMessageType.Close)
        {
            await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge Close frame", CancellationToken.None);
        }

        return ArraySegment<byte>.Empty;
    }

    private async Task SocketProcessingLoopAsync()
    {
        var cancellationToken = _transportTokenSource.Token;
        _initializedEvent.Wait(cancellationToken);
        try
        {
            var buffer = WebSocket.CreateClientBuffer(28 * 1024, 4 * 1024);
            while (_webSocket!.State != WebSocketState.Closed && !cancellationToken.IsCancellationRequested)
            {
                var rawData = await ReceiveAsync(buffer);
                if (cancellationToken.IsCancellationRequested)
                    continue;
                if (_webSocket.State == WebSocketState.Open && _continuations is not null)
                    await _continuations(rawData, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // normal upon task/token cancellation, disregard
        }
        finally
        {
            _stopProcessingLoopEvent.Set();
        }
    }

    public void Dispose()
    {
        _transportTokenSource.Dispose();
        _stopProcessingLoopEvent.Dispose();
    }
}