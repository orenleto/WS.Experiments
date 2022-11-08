namespace Daemon.Contracts.Interfaces;

public interface IProcessingHandler<T> where T : class
{
    Task<T?> Handle(ArraySegment<byte> rawData, CancellationToken cancellationToken);
}