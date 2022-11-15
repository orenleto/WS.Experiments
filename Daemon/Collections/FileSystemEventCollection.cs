using System.Collections;
using Daemon.Configurations;
using Daemon.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Daemon.Collections;

/// <inheritdoc/>
/// <summary>
/// Collection of any file system events currently happening in a given directory, Should be used
/// on a background task as this will block while waiting for change events
/// </summary>
public sealed class FileSystemEventCollection : IEnumerable<FileSystemEventArgs>, IDisposable
{
    internal readonly ManualResetEventSlim IsInitializedEvent = new();
    private readonly CancellationToken _cancellationToken;
    private readonly FileSystemEventConfiguration _configuration;
    private readonly ILogger<FileSystemEventCollection> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="FileSystemEventCollection"/>
    /// </summary>
    /// <param name="configuration">        Configuration to use </param>
    /// <param name="cancellationToken">    Cancellation token to signal to watcher to stop </param>
    /// <param name="logger">               Logger to use </param>
    public FileSystemEventCollection(FileSystemEventConfiguration configuration, CancellationToken cancellationToken, ILogger<FileSystemEventCollection>? logger = null)
    {
        if (cancellationToken == default)
            throw new ArgumentNullException(nameof(cancellationToken));

        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _cancellationToken = cancellationToken;
        _logger = logger ?? NullLogger<FileSystemEventCollection>.Instance;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemEventCollection"/>
    /// </summary>
    /// <param name="cancellationToken"> Cancellation token to signal to watcher to stop </param>
    /// <param name="directory">         Directory to monitor for events </param>
    /// <param name="filePattern">       File pattern to monitor within directory </param>
    /// <param name="logger">            Logger to use </param>
    public FileSystemEventCollection(CancellationToken cancellationToken, string directory, string? filePattern = null, ILogger<FileSystemEventCollection>? logger = null)
        : this(new FileSystemEventConfiguration(directory, filePattern), cancellationToken, logger)
    {
    }

    /// <inheritdoc/>
    /// <summary>
    /// Iterates over the collection of <see cref="FileSystemEventArgs"/> awaiting any new ones.
    /// This is long running and will block while waiting for the next file system event
    /// </summary>
    /// <remarks>
    /// On initial creation of collection will create an event for all files currently in monitored directory.
    /// </remarks>
    /// <returns> Non duplicate <see cref="FileSystemEventArgs"/> </returns>
    public IEnumerator<FileSystemEventArgs> GetEnumerator()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            _logger.CancellationRequested();
            yield break;
        }

        using var watcher = new FileSystemWatcher(_configuration.DirectoryToMonitor, _configuration.DirectoryFileFilter);
        using var queue = new FileSystemEventQueue(_configuration.DuplicateEventDelayWindow.TotalMilliseconds, _logger);

        Initialize(queue, watcher);

        if (_cancellationToken.IsCancellationRequested)
        {
            _logger.CancellationRequested();
            yield break;
        }

        while (queue.TryDequeue(out var fileSystemEventArgs, _cancellationToken))
            yield return fileSystemEventArgs!;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public void Dispose()
    {
        IsInitializedEvent.Dispose();
    }

    private void Initialize(FileSystemEventQueue queue, FileSystemWatcher watcher)
    {
        var tasks = new[]
        {
            Task.Run(() => InitializeWatcher(queue, watcher), _cancellationToken),
            // Task.Run(() => QueueInitialFiles(queue), _cancellationToken)
        };
        Task.WaitAll(tasks, _cancellationToken);
        IsInitializedEvent.Set();
    }

    private void InitializeWatcher(FileSystemEventQueue queue, FileSystemWatcher watcher)
    {
        _logger.Initializing<FileSystemEventCollection>();
        watcher.NotifyFilter = NotifyFilters.Attributes
                               | NotifyFilters.CreationTime
                               | NotifyFilters.DirectoryName
                               | NotifyFilters.FileName
                               | NotifyFilters.LastAccess
                               | NotifyFilters.LastWrite
                               | NotifyFilters.Security
                               | NotifyFilters.Size;

        watcher.Created += (_, e) => queue.Enqueue(e);
        watcher.Changed += (_, e) => queue.Enqueue(e);
        watcher.Deleted += (_, e) => queue.Enqueue(e);
        watcher.Renamed += (_, e) => queue.Enqueue(e);

        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;
    }

    private void QueueInitialFiles(FileSystemEventQueue queue)
    {
        _logger.QueuingInitialFiles();
        foreach (var file in Directory.GetFiles(_configuration.DirectoryToMonitor, _configuration.DirectoryFileFilter, SearchOption.TopDirectoryOnly))
        {
            queue.Enqueue(new FileSystemEventArgs(WatcherChangeTypes.All, _configuration.DirectoryToMonitor, Path.GetFileName(file)));
        }
    }
}