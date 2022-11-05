using System.Collections.Concurrent;
using Daemon.Configurations;
using Daemon.IO;

namespace Daemon;

public class SubscriptionManager : IDisposable
{
    private static readonly object _syncRoot = new object();
    
    private static readonly CancellationTokenSource WatcherTokenSource = new CancellationTokenSource();
    
    private static readonly ConcurrentDictionary<string, Watcher> _watchers = new ConcurrentDictionary<string, Watcher>();
    private static readonly ConcurrentDictionary<Guid, LinkedList<Watcher>> _subscriptions = new ConcurrentDictionary<Guid, LinkedList<Watcher>>();

    public void Subscribe(ClientSession clientSession, string directory)
    {
        Console.WriteLine($"Subscribe on {directory}");
        
        if (_watchers.TryGetValue(directory, out var watcher))
        {
            Console.WriteLine($"Subscription {directory} exist without lock");
            watcher.AddCallback(clientSession.SendAsync);
        }
        else
        {
            lock (_syncRoot)
            {
                if (_watchers.TryGetValue(directory, out watcher))
                {
                    Console.WriteLine($"Subscription {directory} exist under lock");
                    watcher.AddCallback(clientSession.SendAsync);
                }
                else
                {
                    Console.WriteLine($"Create new subscription on {directory}");
                    watcher = new Watcher(clientSession.SendAsync, new FileSystemEventConfiguration(directory), WatcherTokenSource.Token, null, RemoveWatcher);
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
                watcher.RemoveCallback(clientSession.SendAsync);    
        }
    }
    
    public void Dispose()
    {
        WatcherTokenSource.Cancel();
    }

    private static void RemoveWatcher(string directory)
    {
        Console.WriteLine($"Remove watcher for {directory}");
        lock (_syncRoot)
        {
            if (_watchers.TryRemove(directory, out var watcher))
                watcher.Dispose();
        }
        
    }
}