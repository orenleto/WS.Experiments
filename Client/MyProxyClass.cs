using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using JetBrains.Annotations;
using Path = System.IO.Path;

namespace Client;

public class MyProxyClass : IFileSystemDaemon
{
    private readonly CancellationTokenSource _networkConnectionTokenSource;
    private readonly Client _client;
    private readonly Channel<FileSystemEvent> _channel;
    
    private ClientWebSocket? _webSocket;

    public MyProxyClass(Client client)
    {
        _client = client;
        _channel = Channel.CreateUnbounded<FileSystemEvent>();
        _networkConnectionTokenSource = new CancellationTokenSource();
    }

    public async Task<CustomChannelReader<FileSystemEvent>> SubscribeChanges(string path)
    {
        var token = _networkConnectionTokenSource.Token;
        if (!await CheckConnection())
            // todo: использовать ResourceManager для текста ошибки
            throw new InvalidOperationException();
        
        var command = new SubscribeChangesRequest(path);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(command);
        if (!token.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, token);
        
        // todo: предусмотреть возможность подписки на разные директории;
        // как должны приходить события в один канал (логично по типу события) или для каждой директории свой канал (логично по возвращаемому типу)
        return new CustomChannelReader<FileSystemEvent>(this, _channel.Reader);
    }
    
    public async Task Cancel()
    {
        if (_webSocket?.State != WebSocketState.Open || _networkConnectionTokenSource.IsCancellationRequested)
            return;

        _networkConnectionTokenSource.Cancel();
        await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
    }

    private async ValueTask<bool> CheckConnection()
    {
        if (_webSocket is not null && _webSocket.State == WebSocketState.Open)
            return true;
        
        if (_webSocket is null)
        {
            var token = _networkConnectionTokenSource.Token;
            
            _webSocket = new ClientWebSocket();
            await _webSocket.ConnectAsync(_client.Uri, token);
            
            _ = Task.Run(() => SocketProcessingLoopAsync().ConfigureAwait(false));
            
            return true;
        }

        return false;
    }

    private async Task SocketProcessingLoopAsync()
    {
        if (_webSocket is null)
            // todo: использовать ResourceManager для текста ошибки
            throw new InvalidOperationException();
        
        var cancellationToken = _networkConnectionTokenSource.Token;
        var writer = _channel.Writer;
        try
        {
            var buffer = WebSocket.CreateClientBuffer(4096, 4096);
            while (_webSocket.State != WebSocketState.Closed && !cancellationToken.IsCancellationRequested)
            {
                var receiveResult = await _webSocket.ReceiveAsync(buffer, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    if (_webSocket.State == WebSocketState.Open && receiveResult.MessageType != WebSocketMessageType.Close)
                    {
                        var @event = JsonSerializer.Deserialize<FileSystemEvent>(new ReadOnlySpan<byte>(buffer.Array!, 0, receiveResult.Count));
                        if (@event is not null)
                            await writer.WriteAsync(@event, cancellationToken);
                        continue;
                    }
                    if (_webSocket.State == WebSocketState.CloseReceived && receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("Закрываем коннекцию – получили сообщение снаружи");
                        await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge Close frame", CancellationToken.None);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal upon task/token cancellation, disregard
        }
        finally
        {
            Console.WriteLine("Закрываем коннекцию – отменили токен или произошло исключение");
            _networkConnectionTokenSource.Cancel();
            _webSocket.Dispose();
            _channel.Writer.Complete();
        }
    }

    private interface IRequest
    {
        string MethodName { get; }
    }

    private struct SubscribeChangesRequest : IRequest
    {
        public string MethodName { get; }
        public string Path { get; }

        public SubscribeChangesRequest(string path)
        {
            MethodName = nameof(SubscribeChanges) + "-" + path.GetType().Name;
            Path = path;
        }
    }
}