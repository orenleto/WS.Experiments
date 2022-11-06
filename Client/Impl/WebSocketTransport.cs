using System.Net.WebSockets;
using Client.Interfaces;

namespace Client.Impl;

public class WebSocketTransport : ITransport
{
    private readonly Uri _uri;
    private readonly CancellationTokenSource _transportTokenSource;
    private readonly ClientWebSocket _webSocket;
    private readonly ManualResetEventSlim _initializedEvent = new();
    private readonly ManualResetEventSlim _stopProcessingLoopEvent = new();

    public WebSocketTransport(Uri uri, CancellationToken cancellationToken)
    {
        _uri = uri;
        _webSocket = new ClientWebSocket();
        _transportTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    public Task Listen(Func<ArraySegment<byte>, CancellationToken, Task> continuation)
    {
        if (_webSocket.State == WebSocketState.Open)
            return Task.CompletedTask;
        if (_webSocket.State == WebSocketState.None)
        {
            _ = Task.Run(() => SocketProcessingLoopAsync(continuation).ConfigureAwait(false));
            _initializedEvent.Wait(_transportTokenSource.Token);
            return Task.CompletedTask;
        }

        // todo: использовать ResourceManager для текста ошибки
        throw new InvalidOperationException("Unable to connect closed websocket");
    }

    public void Stop()
    {
        _transportTokenSource.Cancel();
        _stopProcessingLoopEvent.Wait();
    }
    
    public async Task SendAsync(ArraySegment<byte> data)
    {
        if (_webSocket.State != WebSocketState.Open)
            throw new InvalidOperationException("Unable to send message by invalid websocket state");
        await _webSocket.SendAsync(data, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, _transportTokenSource.Token);
    }

    private async Task<ArraySegment<byte>> ReceiveAsync(ArraySegment<byte> buffer)
    {
        var receiveResult = await _webSocket.ReceiveAsync(buffer, _transportTokenSource.Token);
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

    private async Task SocketProcessingLoopAsync(Func<ArraySegment<byte>, CancellationToken, Task> continuation)
    {
        var cancellationToken = _transportTokenSource.Token;
        try
        {
            await _webSocket.ConnectAsync(_uri, cancellationToken);
            _initializedEvent.Set();
            
            var buffer = WebSocket.CreateClientBuffer(28 * 1024, 4 * 1024);
            while (_webSocket.State != WebSocketState.Closed && !cancellationToken.IsCancellationRequested)
            {
                var rawData = await ReceiveAsync(buffer);
                if (cancellationToken.IsCancellationRequested)
                    continue;
                if (_webSocket.State == WebSocketState.Open)
                    await continuation(rawData, cancellationToken);
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
        _initializedEvent.Dispose();
    }
}