using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Daemon.IO;
using Daemon.Middlewares;

namespace Daemon;

public class ConnectedClient
{
    private readonly LinkedList<Watcher> _watchers;
    private readonly Channel<EventArgs> _broadcastQueue;
    
    public int SocketId { get; }
    public WebSocket Socket { get; }
    public TaskCompletionSource<object> TaskCompletion { get; }
    public CancellationTokenSource BroadcastLoopTokenSource { get; }
    
    public ConnectedClient(int socketId, WebSocket socket, TaskCompletionSource<object> taskCompletion)
    {
        _watchers = new LinkedList<Watcher>();
        _broadcastQueue = Channel.CreateUnbounded<EventArgs>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        SocketId = socketId;
        Socket = socket;
        TaskCompletion = taskCompletion;
        BroadcastLoopTokenSource = new CancellationTokenSource();
    }

    public async Task BroadcastLoopAsync()
    {
        var cancellationToken = BroadcastLoopTokenSource.Token;
        EventArgs? eventArgs = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                while (await _broadcastQueue.Reader.WaitToReadAsync(cancellationToken) || eventArgs is not null)
                {
                    eventArgs = eventArgs ?? await _broadcastQueue.Reader.ReadAsync(cancellationToken);
                    Console.WriteLine($"Socket {SocketId}: Sending from queue.");
                    var @event = eventArgs switch
                    {
                        RenamedEventArgs renamedEventArgs => new FileSystemEvent(renamedEventArgs.ChangeType, renamedEventArgs.FullPath, renamedEventArgs.Name, renamedEventArgs.OldName),
                        FileSystemEventArgs fileSystemEventArgs => new FileSystemEvent(fileSystemEventArgs.ChangeType, fileSystemEventArgs.FullPath, fileSystemEventArgs.Name, null),
                        _ => throw new ArgumentOutOfRangeException(nameof(eventArgs), "Unexpected type")
                    };

                    await Socket.SendAsync(
                        new ReadOnlyMemory<byte>(JsonSerializer.SerializeToUtf8Bytes(@event)),
                        WebSocketMessageType.Binary,
                        endOfMessage: true,
                        cancellationToken: cancellationToken
                    );
                    eventArgs = null;
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Произошла отмена слушания сокета, клиент должен отписаться от всех watchers");
                foreach (var watcher in _watchers)
                    watcher.RemoveCallback(BroadcastAsync);
            }
            catch (WebSocketException)
            {
            }
            catch (Exception ex)
            {
                Program.ReportException(ex);
            }
        }
    }

    public void Subscribe(Watcher watcher)
    {
        watcher.AddCallback(BroadcastAsync);
        _watchers.AddLast(watcher);
    }

    private ValueTask BroadcastAsync(FileSystemEventArgs fileSystemEventArgs)
    {
        return _broadcastQueue.Writer.WriteAsync(fileSystemEventArgs, BroadcastLoopTokenSource.Token);
    }
}