using System.Collections.Concurrent;
using Daemon.Interfaces;

namespace Daemon.Impl;

public class SubscriptionManager : IDisposable, ISubscriptionManager
{
    private readonly ILogger<SubscriptionManager> _logger;
    private readonly ConcurrentDictionary<Guid, LinkedList<IWatcher>> _subscriptions = new();
    private readonly object _syncRoot = new();
    private readonly IWatcherFactory _watcherFactory;

    private readonly ConcurrentDictionary<string, IWatcher> _watchers = new();

    private readonly CancellationTokenSource _watcherTokenSource = new();

    public SubscriptionManager(IWatcherFactory watcherFactory, ILogger<SubscriptionManager> logger)
    {
        _logger = logger;
        _watcherFactory = watcherFactory;
    }

    public void Dispose()
    {
        _watcherTokenSource.Cancel();
        foreach (var watcher in _watchers.Values) watcher.Dispose();
    }

    public void Subscribe(IClientSession clientSession, string directory)
    {
        lock (_syncRoot)
        {
            if (_watchers.TryGetValue(directory, out var watcher))
            {
                _logger.LogInformation("Client {clientId} subscribe to existent watcher by {directory}", clientSession.Id, directory);
                watcher.AddCallback(clientSession.Send);
            }
            else
            {
                _logger.LogInformation("Client {clientId} subscribe to new watcher by {directory}", clientSession.Id, directory);
                var newWatcher = _watcherFactory.Create(directory, _watcherTokenSource.Token);
                watcher = _watchers.GetOrAdd(directory, newWatcher);
                watcher.AddCallback(clientSession.Send);
            }

            _logger.LogInformation("Client {clientId} subscribed on {directory}", clientSession.Id, watcher.Directory);
            var subscriptions = _subscriptions.GetOrAdd(clientSession.Id, _ => new LinkedList<IWatcher>());
            subscriptions.AddLast(watcher);
        }
    }

    public void UnsubscribeAll(IClientSession clientSession)
    {
        lock (_syncRoot)
        {
            if (!_subscriptions.TryRemove(clientSession.Id, out var subscriptions))
                return;

            foreach (var watcher in subscriptions)
            {
                _logger.LogInformation("Client {id} unsubscribed from {directory}", clientSession.Id, watcher.Directory);
                watcher.RemoveCallback(clientSession.Send);
                if (watcher.Subscribers == 0 && _watchers.TryRemove(watcher.Directory, out _)) watcher.Dispose();
            }
        }
    }
}