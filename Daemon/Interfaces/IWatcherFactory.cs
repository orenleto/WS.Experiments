using Daemon.IO;

namespace Daemon.Interfaces;

public interface IWatcherFactory
{
    Watcher Create(string directory, CancellationToken watcherCancellationToken);
}