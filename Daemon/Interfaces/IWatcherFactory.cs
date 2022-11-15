namespace Daemon.Interfaces;

public interface IWatcherFactory
{
    IWatcher Create(string directory, CancellationToken watcherCancellationToken);
}