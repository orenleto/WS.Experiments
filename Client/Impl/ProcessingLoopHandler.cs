using System.Text;
using System.Text.Json;
using Daemon.Contracts.Interfaces;
using Daemon.Contracts.Payloads;
using Daemon.Contracts.Payloads.Events;
using Daemon.Contracts.Payloads.Requests;

namespace Client.Impl;

public class ProcessingLoopHandler : IProcessingHandler<FileSystemEvent>
{
    public async Task<FileSystemEvent?> Handle(ArraySegment<byte> rawData, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<Payload>(rawData);
        if (message is SuccessPayload success)
        {
            var request = success.Request as SubscribeChangesRequest;
            Console.WriteLine("Успешное выполнение запроса {0}", request.Method);
            return new FileSystemEvent
            {
                ChangeType = WatcherChangeTypes.All,
                FullPath = request.Directory
            };
        }

        if (message is ErrorPayload error)
        {
            var errorMessage = string.Join(", ", error.Errors);
            Console.WriteLine("Ошибка при выполнении запроса {0}: {1}", error.Request, errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        if (message is ExceptionPayload serverException)
        {
            Console.WriteLine("Исключение при выполнении запроса {0}: {1}", serverException.Request, serverException.Message);
            throw new Exception(serverException.Message);
        }

        if (message is FileSystemEvent fileSystemEvent)
        {
            Console.WriteLine("Получили cообщение {0}", Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(message)));
            return fileSystemEvent;
        }

        return null;
    }
}