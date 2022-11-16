using Daemon.Collections;
using Daemon.Configurations;
using Daemon.Extensions;
using Daemon.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Daemon.IO;

/// <summary>
///     Background watcher for <see cref="FileSystemEventArgs" />
/// </summary>
public sealed class Watcher : IWatcher
{
    private readonly CancellationTokenSource _cancellationTokenSource;

    private readonly FileSystemEventConfiguration _configuration;
    private readonly ManualResetEventSlim _initializedEvent = new();
    private readonly Thread _internalThread;
    private readonly ILogger<IWatcher> _logger;
    private readonly ManualResetEventSlim _stoppedEvent = new();
    private readonly object _syncRoot = new();
    private Action<FileSystemEventArgs>? _callback;
    private FileSystemEventCollection _collection;
    private bool _isStopped;

    private int _subscribers;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Watcher" /> class.
    /// </summary>
    /// <param name="callback">             Activate to execute on new file system event </param>
    /// <param name="configuration">        Initial configuration object </param>
    /// <param name="cancellationToken">    Cancellation token to signal to stop watching </param>
    /// <param name="logger">               Logger to use </param>
    public Watcher(FileSystemEventConfiguration configuration, CancellationToken cancellationToken, ILogger<IWatcher>? logger = null)
    {
        if (cancellationToken == default)
            throw new ArgumentNullException(nameof(cancellationToken));

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _configuration = configuration;
        _logger = logger ?? NullLogger<IWatcher>.Instance;
        _subscribers = 0;
        _isStopped = false;

        _logger.Initializing<Watcher>();
        lock (_syncRoot)
        {
            _internalThread = new Thread(CollectionWatcherProcessLoop);
        }
    }

    public string Directory => _configuration.DirectoryToMonitor;
    public int Subscribers => _subscribers;

    /// <summary>
    ///     Add callback to current callback chain.
    ///     If callback chain was empty method starts watching process.
    /// </summary>
    /// <param name="callback"> Activate to add to chain </param>
    public void AddCallback(Action<FileSystemEventArgs> callback)
    {
        if (callback is null)
            throw new ArgumentNullException(nameof(callback));

        lock (_syncRoot)
        {
            if (_isStopped)
                throw new InvalidOperationException();
            _callback += callback;
            _logger.LogInformation("Watcher {directory} has new subscriber", _configuration.DirectoryToMonitor);
        }

        if (Interlocked.Increment(ref _subscribers) == 1)
        {
            _internalThread.Start();
            _initializedEvent.Wait(_cancellationTokenSource.Token);
            _logger.LogInformation("Watcher {directory} started", _configuration.DirectoryToMonitor);
        }
    }

    /// <summary>
    ///     Remove callback from current callback chain
    ///     If callback chain is empty – stopped watch process
    /// </summary>
    /// <param name="callback"> Activate to remove from chain </param>
    public void RemoveCallback(Action<FileSystemEventArgs> callback)
    {
        if (callback is null)
            throw new ArgumentNullException(nameof(callback));
        if (_isStopped)
            throw new InvalidOperationException();


        if (Interlocked.Decrement(ref _subscribers) == 0)
        {
            _cancellationTokenSource.Cancel();
            _stoppedEvent.Wait();
            _logger.LogInformation("Watcher {directory} stopped", _configuration.DirectoryToMonitor);
        }

        lock (_syncRoot)
        {
            _callback -= callback;
            _isStopped = _callback == null;
            _logger.LogInformation("Watcher {directory} was unsubscribed from publish event", _configuration.DirectoryToMonitor);
        }
    }


    /// <summary>
    ///     Performs application-defined tasks associated with freeing, releasing, or resetting
    ///     unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        _subscribers = -1;
        _cancellationTokenSource.Dispose();
        _initializedEvent.Dispose();
        _stoppedEvent.Dispose();
        _collection.Dispose();
        _logger.LogInformation("Watcher {directory} was disposed", _configuration.DirectoryToMonitor);
    }

    private void CollectionWatcherProcessLoop()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        _collection = new FileSystemEventCollection(_configuration, cancellationToken);

        _ = Task.Run(() =>
        {
            _collection.IsInitializedEvent.Wait(cancellationToken);
            _initializedEvent.Set();
        });

        using var collectionEnumerator = _collection.GetEnumerator();
        while (collectionEnumerator.MoveNext())
        {
            _logger.LogInformation("Watcher {directory} produce event {event}", _configuration.DirectoryToMonitor, collectionEnumerator.Current.Name);
            _callback!(collectionEnumerator.Current);
        }

        _stoppedEvent.Set();
    }
}