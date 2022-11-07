using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Castle.DynamicProxy;
using Client.Impl.Payloads;
using Client.Impl.Requests;
using Client.Interfaces;
using Client.IO;

namespace Client.Impl;

public class ProxyInterceptor : IInterceptor
{
    private static readonly ConcurrentDictionary<MethodInfo, Type> _dataTransferTypes = new ConcurrentDictionary<MethodInfo, Type>();
    
    private readonly ITransport _transport;
    private readonly Channel<FileSystemEvent> _channel;
    
    public ProxyInterceptor(ITransport transport)
    {
        _transport = transport;
        _channel = Channel.CreateUnbounded<FileSystemEvent>();
    }
    
    public void Intercept(IInvocation invocation)
    {
        _transport.Listen(SocketProcessingLoopAsync);
        
        var transferType = _dataTransferTypes.GetOrAdd(invocation.Method, Generator.GenerateDTO);
        var command = Activator.CreateInstance(transferType, invocation.Arguments);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(command);
        invocation.ReturnValue = Callback();

        async Task<CustomChannelReader<FileSystemEvent>> Callback()
        {
            await _transport.SendAsync(bytes);
            return new CustomChannelReader<FileSystemEvent>(this, _channel.Reader);
        }
    }

    public Task Cancel()
    {
        _transport.Stop();
        _channel.Writer.Complete();
        return Task.CompletedTask;
    }

    private async Task SocketProcessingLoopAsync(ArraySegment<byte> rawData, CancellationToken cancellationToken)
    {
        var writer = _channel.Writer;
        var message = JsonSerializer.Deserialize<Payload>(rawData);
        if (message is SuccessPayload success)
        {
            Console.WriteLine("Успешное выполнение запроса {0}", success.Request.Method);
        }
        else if (message is ErrorPayload error)
        {
            Console.WriteLine("Ошибка при выполнении запроса {0}: {1}", error.Request, string.Join(", ", error.Errors));
        }
        else if (message is ExceptionPayload serverException)
        {
            Console.WriteLine("Исключение при выполнении запроса {0}: {1}", serverException.Request, serverException.Message);
        }
        else if (message is FileSystemEvent fileSystemEvent)
        {
            Console.WriteLine("Получили cообщение {0}", Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(message)));
            await writer.WriteAsync(fileSystemEvent, cancellationToken);
        }
    }
}