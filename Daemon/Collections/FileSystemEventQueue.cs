using System.Collections.Concurrent;
using System.Timers;
using Daemon.Extensions;
using Timer = System.Timers.Timer;

namespace Daemon.Collections;

internal sealed class FileSystemEventQueue : IDisposable
{
    private readonly ConcurrentDictionary<FileSystemEventArgs, Timer> _deduplicateQueue;
    private readonly double _duplicateDelayWindow;
    private readonly SemaphoreSlim _enqueueSemaphore;
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<FileSystemEventArgs?> _queue;

    internal FileSystemEventQueue(double duplicateDelayWindow, ILogger logger)
    {
        _deduplicateQueue = new ConcurrentDictionary<FileSystemEventArgs, Timer>();
        _queue = new ConcurrentQueue<FileSystemEventArgs?>();
        _enqueueSemaphore = new SemaphoreSlim(0);
        _duplicateDelayWindow = duplicateDelayWindow;
        _logger = logger;
    }

    public void Dispose()
    {
        _enqueueSemaphore.Dispose();
        if (!_deduplicateQueue.IsEmpty)
            foreach (var info in _deduplicateQueue)
                info.Value.Dispose();
    }

    public void Enqueue(FileSystemEventArgs fileSystemEventArgs)
    {
        if (!_deduplicateQueue.TryGetValue(GetOriginatingKey(fileSystemEventArgs), out var timer))
        {
            timer = new Timer { Interval = _duplicateDelayWindow, AutoReset = false };
            timer.Elapsed += OnTimerElapsed;

            _deduplicateQueue.TryAdd(fileSystemEventArgs, timer);
            _logger.OriginalTimerAdded(fileSystemEventArgs);
        }
        else
        {
            _logger.DuplicateTimerRestart(fileSystemEventArgs);
        }

        timer.Stop();
        timer.Start();
    }

    public bool TryDequeue(out FileSystemEventArgs? fileEventArgs, CancellationToken cancellationToken)
    {
        fileEventArgs = null;
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.CancellationRequested();
            return false;
        }

        try
        {
            _enqueueSemaphore.Wait(cancellationToken);
            return _queue.TryDequeue(out fileEventArgs);
        }
        catch (OperationCanceledException)
        {
            _logger.CancellationRequested();
            return false;
        }
    }

    private FileSystemEventArgs GetOriginatingKey(FileSystemEventArgs fileSystemEventArgs)
    {
        foreach (var @event in _deduplicateQueue)
            if (@event.Key.IsDuplicate(fileSystemEventArgs))
                return @event.Key;

        return fileSystemEventArgs;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        var timer = sender as Timer;

        var kvp = _deduplicateQueue.First(t => t.Value == timer);
        _queue.Enqueue(kvp.Key);
        _logger.Enqueue(kvp.Key);

        if (_deduplicateQueue.TryRemove(kvp.Key, out var removedInfo))
            removedInfo?.Dispose();

        _enqueueSemaphore.Release();
    }
}