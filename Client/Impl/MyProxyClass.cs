using System.Text.Json;
using System.Threading.Channels;
using Client.Impl.Payloads;
using Client.Impl.Requests;
using Client.Interfaces;
using Client.IO;

namespace Client.Impl;

public class MyProxyClass : IFileSystemDaemon
{
    private readonly ITransport _transport;
    private readonly Channel<FileSystemEvent> _channel;

    public MyProxyClass(ITransport transport)
    {
        _transport = transport;
        _channel = Channel.CreateUnbounded<FileSystemEvent>();
    }

    public void Connect() => _transport.Listen(SocketProcessingLoopAsync);

    public async Task<CustomChannelReader<FileSystemEvent>> SubscribeChanges(string directory)
    {
        var command = new SubscribeChangesRequest { Directory = directory };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(command);
        await _transport.SendAsync(new ArraySegment<byte>(bytes));
        return new CustomChannelReader<FileSystemEvent>(this, _channel.Reader);
    }

    public async Task Cancel()
    {
        _transport.Stop();
        _channel.Writer.Complete();
    }

    private async Task SocketProcessingLoopAsync(ArraySegment<byte> rawData, CancellationToken cancellationToken)
    {
        var writer = _channel.Writer;
        var message = JsonSerializer.Deserialize<Payload>(rawData);
        if (message is MessagePayload payload)
        {
            Console.WriteLine("Получили данные об изменениях в директории {0}", payload.FullPath);
            var @event = new FileSystemEvent(payload.ChangeType, payload.FullPath, payload.Name, payload.OldName);
            await writer.WriteAsync(@event, cancellationToken);
        }
        else if (message is SuccessPayload success)
        {
            Console.WriteLine("Успешное выполнение запроса {0}", success.Request.Method);
        }
        else if (message is ErrorPayload exception)
        {
            Console.WriteLine("Ошибка при выполнении запроса {0}: {1}", exception.Request, string.Join(", ", exception.Errors));
        }
        else if (message is ExceptionPayload serverException)
        {
            Console.WriteLine("Исключение при выполнении запроса {0}: {1}", serverException.Request, serverException.Message);
        }
    }
}