using System.Collections.Concurrent;
using Daemon.Configurations;
using Daemon.IO;

namespace Daemon;

public class SubscriptionManager : IDisposable, ISubscriptionManager
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<SubscriptionManager> _logger;
    private readonly object _syncRoot = new object();
    
    private readonly CancellationTokenSource _watcherTokenSource = new CancellationTokenSource();
    
    private readonly ConcurrentDictionary<string, Watcher> _watchers = new ConcurrentDictionary<string, Watcher>();
    private readonly ConcurrentDictionary<Guid, LinkedList<Watcher>> _subscriptions = new ConcurrentDictionary<Guid, LinkedList<Watcher>>();

    public SubscriptionManager(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<SubscriptionManager>();
    }
    
    public void Subscribe(ClientSession clientSession, string directory)
    {
        if (_watchers.TryGetValue(directory, out var watcher))
        {
            _logger.LogInformation("Client {clientId} subscribe on directory {directory}", clientSession.Id, directory);
            watcher.AddCallback(clientSession.SendAsync);
        }
        else
        {
            lock (_syncRoot)
            {
                if (_watchers.TryGetValue(directory, out watcher))
                {
                    _logger.LogInformation("Client {clientId} subscribe on directory {directory} from lock section", clientSession.Id, directory);
                    watcher.AddCallback(clientSession.SendAsync);
                }
                else
                {
                    _logger.LogInformation("Client {clientId} subscribe on directory {directory} by new watcher", clientSession.Id, directory);
                    watcher = new Watcher(clientSession.SendAsync, new FileSystemEventConfiguration(directory), _watcherTokenSource.Token, _loggerFactory.CreateLogger<Watcher>(), RemoveWatcher);
                    watcher = _watchers.GetOrAdd(directory, watcher);
                    watcher.Watch();
                }
            }
        }
        
        var subscriptions = _subscriptions.GetOrAdd(clientSession.Id, _ => new LinkedList<Watcher>());
        lock (subscriptions)
        {
            subscriptions.AddLast(watcher);
        }
    }

    public void UnsubscribeAll(ClientSession clientSession)
    {
        if (!_subscriptions.TryRemove(clientSession.Id, out var subscriptions))
        {
            return;
        }

        lock (subscriptions)
        {
            foreach (var watcher in subscriptions.Intersect(_watchers.Values))
            {
                _logger.LogInformation("Client {id} unsubscribed from {directory}", clientSession.Id, watcher.Directory);
                watcher.RemoveCallback(clientSession.SendAsync);
            }
        }
    }
    
    public void Dispose()
    {
        _watcherTokenSource.Cancel();
    }

    private void RemoveWatcher(string directory)
    {
        _logger.LogInformation("Remove watcher for {directory}", directory);
        lock (_syncRoot)
        {
            if (_watchers.TryRemove(directory, out var watcher))
                watcher.Dispose();
        }
        
    }
}