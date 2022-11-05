using Daemon.Collections;
using Daemon.Configurations;
using Daemon.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Daemon.IO;

/// <summary>
/// Background watcher for <see cref="FileSystemEventArgs"/>
/// </summary>
public sealed class Watcher : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly ManualResetEventSlim _initializedEvent = new();
    private readonly CancellationTokenSource _cancellationTokenSource;
    
    private readonly FileSystemEventConfiguration _configuration;
    private readonly ILogger<Watcher> _logger;
    private readonly Thread _internalThread;
    
    private Func<FileSystemEventArgs, ValueTask>? _callback;
    private readonly Action<string>? _onTerminate;
    
    private FileSystemEventCollection _collection;

    public string Directory => _configuration.DirectoryToMonitor;

    /// <summary>
    /// Initializes a new instance of the <see cref="Watcher"/> class.
    /// </summary>
    /// <param name="callback">             Callback to execute on new file system event </param>
    /// <param name="configuration">        Initial configuration object </param>
    /// <param name="cancellationToken">    Cancellation token to signal to stop watching </param>
    /// <param name="logger">               Logger to use </param>
    public Watcher(Func<FileSystemEventArgs, ValueTask>? callback, FileSystemEventConfiguration configuration, CancellationToken cancellationToken, ILogger<Watcher>? logger = null, Action<string>? onTerminate = null)
    {
        if (cancellationToken == default)
            throw new ArgumentNullException(nameof(cancellationToken));

        _callback = callback;
        _onTerminate = onTerminate;
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _configuration = configuration;
        _logger = logger ?? NullLogger<Watcher>.Instance;

        _logger.Initializing<Watcher>(_callback is null ? "with no default callback" : "with default callback");
        lock (_syncRoot)
        {
            _internalThread = new Thread(StartCollectionWatcher);
        }
    }

    /// <summary>
    /// Add callback to current callback chain
    /// </summary>
    /// <param name="callback"> Callback to add to chain </param>
    public void AddCallback(Func<FileSystemEventArgs, ValueTask> callback)
    {
        if (callback is null)
            throw new ArgumentNullException(nameof(callback));

        lock (_syncRoot)
        {
            _callback += callback;
            _logger.LogInformation("Watcher {directory} has one more subscriber", _configuration.DirectoryToMonitor);
        }
    }

    /// <summary>
    /// Remove callback from current callback chain
    /// </summary>
    /// <param name="callback"> Callback to remove from chain </param>
    public void RemoveCallback(Func<FileSystemEventArgs, ValueTask> callback)
    {
        if (callback is null)
            throw new ArgumentNullException(nameof(callback));

        lock (_syncRoot)
        {
            _callback -= callback;
            if (_callback is null)
            {
                _logger.LogInformation("Watcher {directory} stopping initialized", _configuration.DirectoryToMonitor);
                _cancellationTokenSource.Cancel();
            }

        }
    }

    /// <summary>
    /// Begin monitoring directory for file changes
    /// </summary>
    /// <param name="callback"> Callback to execute when file system event occurs </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no callbacks available to execute
    /// </exception>
    public void Watch()
    {
        if (_callback is null)
            throw new InvalidOperationException("Unable to watch without callback to execute");

        _internalThread.Start();
        _initializedEvent.Wait(_cancellationTokenSource.Token);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting
    /// unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
        _initializedEvent.Dispose();
        _collection.Dispose();
    }

    private async void StartCollectionWatcher()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        _logger.LogInformation("Watcher {directory}  started", _configuration.DirectoryToMonitor);
        _collection = new FileSystemEventCollection(_configuration, cancellationToken);

        Task.Run(() =>
        {
            _collection.IsInitializedEvent.Wait(cancellationToken);
            _initializedEvent.Set();
        });

        using var collectionEnumerator = _collection.GetEnumerator();
        while (collectionEnumerator.MoveNext() && _callback is not null)
        {
            _logger.LogInformation("Watcher {directory}  produce event {event}", _configuration.DirectoryToMonitor, collectionEnumerator.Current.Name);
            await _callback!(collectionEnumerator.Current);
        }

        _onTerminate?.Invoke(_configuration.DirectoryToMonitor);
        _logger.LogInformation("Watcher {directory}  stopped", _configuration.DirectoryToMonitor);
    }
}