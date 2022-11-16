using System;
using System.IO;
using System.Threading;
using Daemon.Impl;
using Daemon.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Daemon.UnitTests;

public class SubscriptionManagerTests
{
    private const string directoryName = "SomeDirectory";

    private readonly Mock<IClientSession> _clientSession;
    private readonly SubscriptionManager _sut;
    private readonly Mock<IWatcher> _watcher;
    private readonly Mock<IWatcherFactory> _watcherFactory;

    public SubscriptionManagerTests()
    {
        _clientSession = new Mock<IClientSession>();
        _clientSession
            .Setup(o => o.Id)
            .Returns(Guid.Parse("6B66C8BB-C60E-4ABF-B8BF-C98FD0978019"));

        _watcher = new Mock<IWatcher>();
        _watcher
            .Setup(o => o.Directory)
            .Returns(directoryName);

        _watcherFactory = new Mock<IWatcherFactory>();
        _watcherFactory
            .Setup(o => o.Create(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(_watcher.Object);

        _sut = new SubscriptionManager(_watcherFactory.Object, NullLogger<SubscriptionManager>.Instance);
    }

    [Fact]
    public void Subscribe_MustCreateNewWatcher_WhenTakeNewDirectory()
    {
        _sut.Subscribe(_clientSession.Object, directoryName);

        _watcherFactory.Verify(o => o.Create(directoryName, It.IsAny<CancellationToken>()), Times.Once);
        _watcher.Verify(o => o.AddCallback(_clientSession.Object.Send), Times.Once);
    }

    [Fact]
    public void Subscribe_MustCreateNewWatcher_ForEveryNewDirectory()
    {
        const string customDirectoryName = "CustomDirectoryName";
        _sut.Subscribe(_clientSession.Object, directoryName);
        _sut.Subscribe(_clientSession.Object, customDirectoryName);

        _watcherFactory.Verify(o => o.Create(directoryName, It.IsAny<CancellationToken>()), Times.Once);
        _watcherFactory.Verify(o => o.Create(customDirectoryName, It.IsAny<CancellationToken>()), Times.Once);
        _watcher.Verify(o => o.AddCallback(It.IsAny<Action<FileSystemEventArgs>>()), Times.Exactly(2));
    }

    [Fact]
    public void Subscribe_MustReuseWatcher_ForSameDirectoryFromDistinctClients()
    {
        var clientSession = new Mock<IClientSession>();
        clientSession
            .Setup(o => o.Id)
            .Returns(Guid.Parse("58F638C3-D41E-4942-9956-04E4BF137235"));

        _sut.Subscribe(_clientSession.Object, directoryName);
        _sut.Subscribe(clientSession.Object, directoryName);

        _watcherFactory.Verify(o => o.Create(directoryName, It.IsAny<CancellationToken>()), Times.Once);
        _watcher.Verify(o => o.AddCallback(_clientSession.Object.Send), Times.Once);
        _watcher.Verify(o => o.AddCallback(clientSession.Object.Send), Times.Once);
    }

    [Fact]
    public void Unsubscribe_MustRemoveCallbackAndDisposeWatcher_WhenLastSubscriberUnsubscribe()
    {
        _sut.Subscribe(_clientSession.Object, directoryName);

        _sut.UnsubscribeAll(_clientSession.Object);

        _watcher.Verify(o => o.RemoveCallback(_clientSession.Object.Send), Times.Once);
        _watcher.Verify(o => o.Dispose(), Times.Once);
    }

    [Fact]
    public void Unsubscribe_MustRemoveCallbackOnly_WhenWatcherHasAnotherSubscribers()
    {
        var clientSession = new Mock<IClientSession>();
        clientSession
            .Setup(o => o.Id)
            .Returns(Guid.Parse("8A12CC53-5267-4E56-8134-191F6D78A672"));

        _sut.Subscribe(_clientSession.Object, directoryName);
        _sut.Subscribe(clientSession.Object, directoryName);

        _sut.UnsubscribeAll(clientSession.Object);

        _watcher.Verify(o => o.RemoveCallback(clientSession.Object.Send), Times.Once);
    }

    [Fact]
    public void Subscribe_MustCreateNewWatcher_WhenForSameDirectoryUnsubscribedAll()
    {
        _sut.Subscribe(_clientSession.Object, directoryName);
        _sut.UnsubscribeAll(_clientSession.Object);

        _sut.Subscribe(_clientSession.Object, directoryName);
        _watcherFactory.Verify(o => o.Create(directoryName, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _watcher.Verify(o => o.AddCallback(_clientSession.Object.Send), Times.Exactly(2));
    }
}