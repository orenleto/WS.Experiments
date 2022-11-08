using System.Text;
using System.Text.Json;
using Daemon.Contracts.Interfaces;
using Daemon.Contracts.Payloads;
using Daemon.Contracts.Payloads.Events;

namespace Client.Impl;

public class ProcessingLoopHandler : IProcessingHandler<FileSystemEvent>
{
    public async Task<FileSystemEvent?> Handle(ArraySegment<byte> rawData, CancellationToken cancellationToken)
    {
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
            return fileSystemEvent;
        }
        return null;
    }
}