using Daemon.Configurations;
using Daemon.IO;

namespace Daemon;

public class DaemonService : BackgroundService
{
    private readonly ILogger<DaemonService> _logger;
    private Watcher _watcher;

    public DaemonService(ILogger<DaemonService> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _watcher = new Watcher(new FileSystemEventConfiguration("/Users/uumka/Desktop/CV"), stoppingToken);
        _watcher.Watch(Callback);
        return Task.CompletedTask;
    }

    private void Callback(FileSystemEventArgs args)
    {
        var message = args.ChangeType switch
        {
            WatcherChangeTypes.Created => $"Created: {args.FullPath}",
            WatcherChangeTypes.Deleted => $"Deleted: {args.FullPath}",
            WatcherChangeTypes.Changed => $"Changed: {args.FullPath}",
            WatcherChangeTypes.Renamed => $"Renamed: {args.FullPath} <- {((RenamedEventArgs)args).OldFullPath}",
            WatcherChangeTypes.All => $"Monitor: {args.FullPath}",
            _ => $"Unknown event"
        };
        _logger.LogInformation(message);
    }
}
