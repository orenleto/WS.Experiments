using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using Daemon.Contracts.Payloads;
using Daemon.Contracts.Payloads.Events;
using Daemon.Handlers;
using Daemon.Impl;
using Daemon.Interfaces;
using FluentResults;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetCoreServer;
using Xunit;

namespace Daemon.UnitTests;

public class ClientSessionTests
{
    private readonly Mock<IMediator> _mediator;
    private readonly Mock<ISubscriptionManager> _subscriptionManager;
    private readonly SimpleSession _sut;

    public ClientSessionTests()
    {
        _mediator = new Mock<IMediator>();
        _subscriptionManager = new Mock<ISubscriptionManager>();
        _sut = new SimpleSession(new WsServer("127.0.0.1", 9771), _mediator.Object, _subscriptionManager.Object, NullLogger<SimpleSession>.Instance);
    }

    [Fact]
    public void OnWsReceived_MustSendSuccessPayload_WhenSubscribingIsSuccessful()
    {
        var rawData = JsonSerializer.SerializeToUtf8Bytes(new { Method = "SubscribeChanges-String", Directory = "./SomeDirectory" });
        _mediator
            .Setup(o => o.Send(It.Is<SubscribeChanges.Command>(c => c.Directory == "./SomeDirectory"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        _sut.OnWsReceived(rawData, 0, rawData.Length);

        Assert.Equal(1, _sut.Payloads.Count);
        Assert.Contains(_sut.Payloads, payload => payload is SuccessPayload);
        _mediator.Verify(o => o.Send(It.Is<SubscribeChanges.Command>(c => c.Directory == "./SomeDirectory"), It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public void OnWsReceived_MustSendErrorPayload_WhenCommandIsFailed()
    {
        var rawData = JsonSerializer.SerializeToUtf8Bytes(new { Method = "SubscribeChanges-String", Directory = "./SomeDirectory" });
        _mediator
            .Setup(o => o.Send(It.Is<SubscribeChanges.Command>(c => c.Directory == "./SomeDirectory"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Fail result"));

        _sut.OnWsReceived(rawData, 0, rawData.Length);

        Assert.Equal(1, _sut.Payloads.Count);
        Assert.Contains(_sut.Payloads, payload => payload is ErrorPayload);
        _mediator.Verify(o => o.Send(It.Is<SubscribeChanges.Command>(c => c.Directory == "./SomeDirectory"), It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public void OnWsReceived_MustSendExceptionPayload_WhenMessageIsNotRequestInheritor()
    {
        var rawData = JsonSerializer.SerializeToUtf8Bytes(new { Method = "SubscribeChanges-StringInt", Directory = "./SomeDirectory" });

        _sut.OnWsReceived(rawData, 0, rawData.Length);

        Assert.Equal(1, _sut.Payloads.Count);
        Assert.Contains(_sut.Payloads, payload => payload is ExceptionPayload);
        _mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public void OnWsDisconnecting_MustUnsubscribeClientFromAll()
    {
        _subscriptionManager.Setup(o => o.UnsubscribeAll(_sut));
        _sut.OnWsDisconnecting();

        _subscriptionManager.Verify(o => o.UnsubscribeAll(_sut));
        _subscriptionManager.VerifyNoOtherCalls();
    }

    [Theory]
    [ClassData(typeof(FileSystemEventTestData))]
    public void Send_MustSendFileSystemEvent_WhenReceiveFileSystemEventArgs(EventArgs eventArgs, FileSystemEvent expected)
    {
        _sut.Send(eventArgs);

        Assert.Equal(1, _sut.Payloads.Count);
        Assert.Contains(_sut.Payloads, payload => payload is FileSystemEvent fse
                                                  && fse.Type == expected.Type
                                                  && fse.Name == expected.Name
                                                  && fse.FullPath == expected.FullPath
                                                  && fse.OldName == expected.OldName);
    }

    [Fact]
    public void Send_MustSendExceptionPayload_WhenReceiveNotFileSystemEventArgs()
    {
        var emptyEventArgs = EventArgs.Empty;
        _sut.Send(emptyEventArgs);

        Assert.Equal(1, _sut.Payloads.Count);
        Assert.Contains(_sut.Payloads, payload => payload is ExceptionPayload);
    }

    public class FileSystemEventTestData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[]
            {
                new FileSystemEventArgs(WatcherChangeTypes.All, "./some/directory", "text.json"),
                new FileSystemEvent { ChangeType = WatcherChangeTypes.All, FullPath = "./some/directory/text.json", Name = "text.json" }
            };
            yield return new object[]
            {
                new RenamedEventArgs(WatcherChangeTypes.Renamed, "./some/directory", "text.json", "old_text.json"),
                new FileSystemEvent
                {
                    ChangeType = WatcherChangeTypes.Renamed, FullPath = "./some/directory/text.json", Name = "text.json", OldName = "old_text.json"
                }
            };
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }


    private class SimpleSession : ClientSession
    {
        public SimpleSession(WsServer server,
            IMediator mediator,
            ISubscriptionManager subscriptionManager,
            ILogger<ClientSession> logger
        ) : base(server, mediator, subscriptionManager, logger)
        {
        }

        public List<Payload> Payloads { get; } = new();

        public override bool SendAsync(ReadOnlySpan<byte> buffer)
        {
            var skip = buffer.Length <= 126 ? 2
                : buffer.Length <= 65539 ? 4
                : 10;

            Payloads.Add(JsonSerializer.Deserialize<Payload>(buffer.Slice(skip)));
            return true;
        }
    }
}