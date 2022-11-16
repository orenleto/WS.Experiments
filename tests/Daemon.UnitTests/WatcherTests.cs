using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Daemon.Configurations;
using Daemon.Interfaces;
using Daemon.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Daemon.UnitTests;

public class WatcherTests
{
    private const string FileName = "testFile.txt";

    [Fact]
    public void CollectionWatcherProcessLoop_MustCallCallback_WhenCreatedNewFile()
    {
        const string directory = $"./{nameof(CollectionWatcherProcessLoop_MustCallCallback_WhenCreatedNewFile)}";
        const string filePath = $"{directory}/{FileName}";
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        if (File.Exists(filePath))
            File.Delete(filePath);

        var cts = new CancellationTokenSource();
        var subscriber = new SimpleSubscriber();

        var watcher = new Watcher(new FileSystemEventConfiguration(directory), cts.Token, NullLogger<IWatcher>.Instance);
        Assert.Equal(0, watcher.Subscribers);
        Assert.Equal(directory, watcher.Directory);

        watcher.AddCallback(subscriber.Send);
        Assert.Equal(1, watcher.Subscribers);

        using (var fs = File.CreateText(filePath))
        {
            fs.Write("lorem ipsum dolor sit amet");
            fs.Flush();
        }

        subscriber.ReceivedEvent.Wait(cts.Token);
        cts.Cancel();

        Assert.True(subscriber.Events.Count > 0);
        Assert.Contains(subscriber.Events,
            args => args is FileSystemEventArgs { ChangeType: WatcherChangeTypes.Created, Name: FileName, FullPath: filePath });
    }

    [Fact]
    public void CollectionWatcherProcessLoop_MustCallAllCallbacks_WhenMoreThanOneSubscriber()
    {
        const string directory = $"./{nameof(CollectionWatcherProcessLoop_MustCallCallback_WhenCreatedNewFile)}";
        const string filePath = $"{directory}/{FileName}";
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        if (File.Exists(filePath))
            File.Delete(filePath);

        var cts = new CancellationTokenSource();
        var subscriber = new SimpleSubscriber();
        var anotherSubscriber = new SimpleSubscriber();

        var watcher = new Watcher(new FileSystemEventConfiguration(directory), cts.Token, NullLogger<IWatcher>.Instance);
        Assert.Equal(watcher.Subscribers, 0);
        Assert.Equal(watcher.Directory, directory);

        watcher.AddCallback(subscriber.Send);
        watcher.AddCallback(anotherSubscriber.Send);
        Assert.Equal(watcher.Subscribers, 2);

        using (var fs = File.CreateText(filePath))
        {
            fs.Write("lorem ipsum dolor sit amet");
            fs.Flush();
        }

        subscriber.ReceivedEvent.Wait(cts.Token);
        anotherSubscriber.ReceivedEvent.Wait(cts.Token);
        cts.Cancel();

        Assert.True(subscriber.Events.Count > 0);
        Assert.Contains(subscriber.Events,
            args => args is FileSystemEventArgs { ChangeType: WatcherChangeTypes.Created, Name: FileName, FullPath: filePath });

        Assert.True(anotherSubscriber.Events.Count > 0);
        Assert.Contains(anotherSubscriber.Events,
            args => args is FileSystemEventArgs { ChangeType: WatcherChangeTypes.Created, Name: FileName, FullPath: filePath });
    }

    [Fact]
    public void CollectionWatcherProcessLoop_MustNotListenDirectory_WithoutCallback()
    {
        const string directory = $"./{nameof(CollectionWatcherProcessLoop_MustNotListenDirectory_WithoutCallback)}";
        const string filePath = $"{directory}/{FileName}";
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        if (File.Exists(filePath))
            File.Delete(filePath);

        var cts = new CancellationTokenSource();
        var subscriber = new SimpleSubscriber();

        var watcher = new Watcher(new FileSystemEventConfiguration(directory), cts.Token, NullLogger<IWatcher>.Instance);
        Assert.Equal(0, watcher.Subscribers);
        Assert.Equal(directory, watcher.Directory);

        watcher.AddCallback(subscriber.Send);
        watcher.RemoveCallback(subscriber.Send);

        using (var fs = File.CreateText(filePath))
        {
            fs.Write("lorem ipsum dolor sit amet");
            fs.Flush();
        }

        cts.Cancel();

        Assert.Equal(0, watcher.Subscribers);
        Assert.Equal(0, subscriber.Events.Count);
    }

    [Fact]
    public void AddCallback_MustThrowInvalidOperationException_WhenWatcherStopped()
    {
        const string directory = $"./{nameof(AddCallback_MustThrowInvalidOperationException_WhenWatcherStopped)}";
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var cts = new CancellationTokenSource();
        var subscriber = new SimpleSubscriber();

        var watcher = new Watcher(new FileSystemEventConfiguration(directory), cts.Token, NullLogger<IWatcher>.Instance);

        watcher.AddCallback(subscriber.Send);
        watcher.RemoveCallback(subscriber.Send);

        Assert.Equal(0, watcher.Subscribers);

        Assert.Throws<InvalidOperationException>(() => watcher.AddCallback(subscriber.Send));
    }

    [Fact]
    public void AddCallback_MustCorrectlyCalculateSubscribers_InParallelsCase()
    {
        const int subscriberCount = 200;
        const string directory = $"./{nameof(AddCallback_MustCorrectlyCalculateSubscribers_InParallelsCase)}";
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var cts = new CancellationTokenSource();
        var subscriber = Enumerable.Range(0, subscriberCount).Select(_ => new SimpleSubscriber()).ToArray();

        var watcher = new Watcher(new FileSystemEventConfiguration(directory), cts.Token, NullLogger<IWatcher>.Instance);
        Assert.Equal(0, watcher.Subscribers);
        Assert.Equal(directory, watcher.Directory);

        Parallel.For(0, subscriberCount, i => watcher.AddCallback(subscriber[i].Send));
        cts.Cancel();

        Assert.Equal(subscriberCount, watcher.Subscribers);
    }

    [Fact]
    public void RemoveCallback_MustCorrectlyCalculateSubscribers_InParallelsCase()
    {
        const int subscriberCount = 200;
        const int unsubscriberCount = 132;
        Assert.True(unsubscriberCount <= subscriberCount);
        const string directory = $"./{nameof(RemoveCallback_MustCorrectlyCalculateSubscribers_InParallelsCase)}";
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var cts = new CancellationTokenSource();
        var subscriber = Enumerable.Range(0, subscriberCount).Select(_ => new SimpleSubscriber()).ToArray();

        var watcher = new Watcher(new FileSystemEventConfiguration(directory), cts.Token, NullLogger<IWatcher>.Instance);
        Assert.Equal(watcher.Subscribers, 0);
        Assert.Equal(watcher.Directory, directory);

        Parallel.For(0, subscriberCount, i => watcher.AddCallback(subscriber[i].Send));
        Parallel.For(0, unsubscriberCount, i => watcher.RemoveCallback(subscriber[i].Send));
        cts.Cancel();

        Assert.Equal(subscriberCount - unsubscriberCount, watcher.Subscribers);
    }

    [Fact]
    public void ActionCallback_MustCorrectlyCalculateSubscribers_InParallelsCase()
    {
        const int unsubscribedCount = 50;
        const int index = 3;
        const int subscriberCount = unsubscribedCount + index * unsubscribedCount;

        const string directory = $"./{nameof(ActionCallback_MustCorrectlyCalculateSubscribers_InParallelsCase)}";
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var cts = new CancellationTokenSource();
        var subscriber = Enumerable.Range(0, subscriberCount).Select(_ => new SimpleSubscriber()).ToArray();

        var watcher = new Watcher(new FileSystemEventConfiguration(directory), cts.Token, NullLogger<IWatcher>.Instance);
        Assert.Equal(watcher.Subscribers, 0);
        Assert.Equal(watcher.Directory, directory);

        Parallel.For(0, unsubscribedCount, i => watcher.AddCallback(subscriber[i].Send));
        Parallel.For(unsubscribedCount, subscriberCount, i =>
        {
            var removing = (i - unsubscribedCount) % index == 0;
            if (removing)
                watcher.RemoveCallback(subscriber[(i - unsubscribedCount) / index].Send);
            watcher.AddCallback(subscriber[i].Send);
        });
        cts.Cancel();

        Assert.Equal(subscriberCount - unsubscribedCount, watcher.Subscribers);
    }

    private class SimpleSubscriber : IClientSession
    {
        public SimpleSubscriber()
        {
            Id = Guid.NewGuid();
            Events = new List<EventArgs>();
        }

        public List<EventArgs> Events { get; }
        public ManualResetEventSlim ReceivedEvent { get; } = new();
        public Guid Id { get; }

        public void Send(EventArgs eventArgs)
        {
            Events.Add(eventArgs);
            ReceivedEvent.Set();
        }
    }
}