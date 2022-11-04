using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Daemon.Middlewares;

namespace Daemon;

public class ConnectedClient
{
    public ConnectedClient(int socketId, WebSocket socket, TaskCompletionSource<object> taskCompletion)
    {
        SocketId = socketId;
        Socket = socket;
        TaskCompletion = taskCompletion;
        BroadcastQueue = Channel.CreateUnbounded<FileSystemEventArgs>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    }

    public int SocketId { get; private set; }

    public WebSocket Socket { get; private set; }

    public TaskCompletionSource<object> TaskCompletion { get; private set; }

    public Channel<FileSystemEventArgs> BroadcastQueue { get; private set; }

    public CancellationTokenSource BroadcastLoopTokenSource { get; set; } = new CancellationTokenSource();

    public async Task BroadcastLoopAsync()
    {
        var cancellationToken = BroadcastLoopTokenSource.Token;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                while (await BroadcastQueue.Reader.WaitToReadAsync(cancellationToken))
                {
                    var fileSystemEventArgs = await BroadcastQueue.Reader.ReadAsync(cancellationToken);
                    Console.WriteLine($"Socket {SocketId}: Sending from queue.");
                    var @event = new FileSystemEvent(fileSystemEventArgs.ChangeType, fileSystemEventArgs.FullPath, fileSystemEventArgs.Name, null);
                    var msgbuf = new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(@event));
                    await Socket.SendAsync(msgbuf, WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                // normal upon task/token cancellation, disregard
            }
            catch (Exception ex)
            {
                Program.ReportException(ex);
            }
        }
    }

    public ValueTask BroadcastAsync(FileSystemEventArgs obj)
    {
        return BroadcastQueue.Writer.WriteAsync(obj, BroadcastLoopTokenSource.Token);
    }
}