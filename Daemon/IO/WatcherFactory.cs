using Daemon.Configurations;
using Daemon.Interfaces;

namespace Daemon.IO;

public sealed class WatcherFactory : IWatcherFactory
{
    private readonly ILogger<IWatcher> _logger;

    public WatcherFactory(ILogger<IWatcher> logger)
    {
        _logger = logger;
    }

    public IWatcher Create(string directory, CancellationToken watcherCancellationToken)
    {
        var configuration = new FileSystemEventConfiguration(directory);
        return new Watcher(configuration, watcherCancellationToken, _logger);
    }
}