using Daemon.Configurations;
using Daemon.Interfaces;

namespace Daemon.IO;

public sealed class WatcherFactory : IWatcherFactory
{
    private readonly ILogger<Watcher> _logger;

    public WatcherFactory(ILogger<Watcher> logger)
    {
        _logger = logger;
    }

    public Watcher Create(string directory, CancellationToken watcherCancellationToken)
    {
        var configuration = new FileSystemEventConfiguration(directory);
        return new Watcher(configuration, watcherCancellationToken, _logger);
    }
}