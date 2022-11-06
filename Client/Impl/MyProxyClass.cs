using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Client.Impl.Payloads;
using Client.Impl.Requests;
using Client.Interfaces;
using Client.IO;

namespace Client.Impl;

public class MyProxyClass : IFileSystemDaemon
{
    private readonly CancellationTokenSource _networkConnectionTokenSource = new CancellationTokenSource();
    private readonly ManualResetEventSlim _initializedEvent = new();
    private readonly ManualResetEventSlim _cancellationEvent = new();
    private readonly Configurations.Client _client;
    private readonly ClientWebSocket _webSocket;
    private readonly Channel<FileSystemEvent> _channel;

    public MyProxyClass(Configurations.Client client)
    {
        _client = client;
        _webSocket = new ClientWebSocket();
        _channel = Channel.CreateUnbounded<FileSystemEvent>();
    }

    public void Connect()
    {
        if (_webSocket.State == WebSocketState.Open)
            return;
        if (_webSocket.State == WebSocketState.None)
        {
            _ = Task.Run(() => SocketProcessingLoopAsync().ConfigureAwait(false));
            _initializedEvent.Wait(_networkConnectionTokenSource.Token);
            return;
        }

        // todo: использовать ResourceManager для текста ошибки
        throw new InvalidOperationException("Unable to connect closed websocket");
    }

    public async Task<CustomChannelReader<FileSystemEvent>> SubscribeChanges(string path)
    {
        if (_webSocket.State != WebSocketState.Open)
            throw new InvalidOperationException("Unable to send message by invalid websocket state");
        
        var token = _networkConnectionTokenSource.Token;
        var command = new SubscribeChangesRequest(path);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(command);
        if (!token.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, token);

        // todo: предусмотреть возможность подписки на разные директории;
        // как должны приходить события в один канал (логично по типу события) или для каждой директории свой канал (логично по возвращаемому типу)
        return new CustomChannelReader<FileSystemEvent>(this, _channel.Reader);
    }

    public async Task Cancel()
    {
        if (_webSocket.State != WebSocketState.Open)
            return;
        
        await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        _networkConnectionTokenSource.Cancel();
        _cancellationEvent.Wait();
    }

    private async Task SocketProcessingLoopAsync()
    {
        var cancellationToken = _networkConnectionTokenSource.Token;
        var writer = _channel.Writer;
        try
        {
            await _webSocket.ConnectAsync(_client.Uri, _networkConnectionTokenSource.Token);
            _initializedEvent.Set();
            
            var buffer = WebSocket.CreateClientBuffer(4096, 4096);
            while (_webSocket.State != WebSocketState.Closed && !cancellationToken.IsCancellationRequested)
            {
                var receiveResult = await _webSocket.ReceiveAsync(buffer, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                    continue;
                
                if (_webSocket.State == WebSocketState.Open && receiveResult.MessageType != WebSocketMessageType.Close)
                {
                    var message = JsonSerializer.Deserialize<Payload>(new ArraySegment<byte>(buffer.Array!, 0, receiveResult.Count));
                    if (message is MessagePayload payload)
                    {
                        Console.WriteLine("Получили данные об изменениях в директории {0}", payload.FullPath);
                        var @event = new FileSystemEvent(payload.ChangeType, payload.FullPath, payload.Name, payload.OldName);
                        await writer.WriteAsync(@event, cancellationToken);
                    }
                    else if (message is SuccessPayload success)
                    {
                        Console.WriteLine("Успешная подписка на изменения директории {0}", success.Directory);
                    }
                    else if (message is ErrorPayload exception)
                    {
                        Console.WriteLine("Ошибка при подписке на изменения директории {0}: {1}", exception.Directory, exception.Message);
                    }
                    else if (message is ExceptionPayload serverException)
                    {
                        Console.WriteLine("Ошибка на стороне сервера в методе {0}: {1}", serverException.Method, serverException.Message);
                    }
                }
                else if (_webSocket.State == WebSocketState.CloseReceived && receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("Закрываем коннекцию – получили сообщение снаружи");
                    await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge Close frame", CancellationToken.None);
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
            _channel.Writer.Complete();
            _cancellationEvent.Set();
            _webSocket.Dispose();
        }
    }
}