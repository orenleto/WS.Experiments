namespace Daemon.Contracts.Interfaces;

public interface ITransport : IDisposable
{
    Task Connect(CancellationToken cancellationToken);
    Task SendAsync(ArraySegment<byte> data, CancellationToken cancellationToken);
    Task<ArraySegment<byte>> ReceiveAsync(CancellationToken cancellationToken);
}