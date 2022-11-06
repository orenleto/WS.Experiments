namespace Client.Interfaces;

public interface ITransport : IDisposable
{
    Task Listen(Func<ArraySegment<byte>, CancellationToken, Task> continuation);
    Task SendAsync(ArraySegment<byte> data);
    void Stop();
}