using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Daemon.Configurations;
using Daemon.Contracts.Interfaces;
using Daemon.Contracts.Payloads.Events;
using Daemon.Impl;
using Daemon.Interfaces;
using Daemon.IO;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Daemon.IntegrationTests;

public class DaemonTests : IDisposable
{
    private const string txtFileName = "test_file.txt";
    private const string jsonFileName = "test_file.json";
    private const string binFileName = "test_file.bin";
    
    private static readonly IPAddress _ipAddress = IPAddress.Loopback;
    private static readonly int _port = 28933;
    private static readonly ArraySegment<byte> _randomBytes;
    
    private readonly ServiceProvider _serviceProvider;
    private readonly IHostedService _service;

    static DaemonTests()
    {
        var raw = new byte[256];
        Random.Shared.NextBytes(raw);
        _randomBytes = raw;
    }

    public DaemonTests()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddMediatR(typeof(Program));
        services.AddSingleton<ISubscriptionManager, SubscriptionManager>();
        services.AddSingleton<IWatcherFactory, WatcherFactory>();
        services.AddHostedService<WebSocketServerService>();
        services.Configure<ServerConfiguration>(c =>
        {
            c.IpAddress = _ipAddress.ToString();
            c.Port = _port;
        });
        _serviceProvider = services.BuildServiceProvider();
        _service = _serviceProvider.GetService<IHostedService>();
        _service.StartAsync(CancellationToken.None);
    }
    
    [Fact]
    public async Task SubscribeChanges_MustReceiveAllDirectoryChanges()
    {
        var testDirectory = $"./IntegrationTest/{nameof(SubscribeChanges_MustReceiveAllDirectoryChanges)}";
        PrepareDirectory(testDirectory);
        
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var events = new List<FileSystemEvent>();
        
        var daemon = Client.Program.Proxy<IFileSystemDaemon>(new Client.Configurations.Client($"ws://{_ipAddress.ToString()}:{_port}/"));
        var changesReader = await daemon.SubscribeChanges(testDirectory);
        
        await changesReader.WaitToReadAsync(token); // Receive subscribing accept

        await CreateTxtFile(testDirectory);
        await CreateJsonFile(testDirectory);
        await CreateBinFile(testDirectory);

        while (await changesReader.WaitToReadAsync(token))
        {
            var fsEvent = await changesReader.ReadAsync(token);
            events.Add(fsEvent);
            if (events.Count >= 7)
                changesReader.Cancel();
        }

        Assert.Contains(events, @event => @event.ChangeType == WatcherChangeTypes.All && @event.FullPath == testDirectory);
        Assert.Contains(events, @event => @event.ChangeType == WatcherChangeTypes.Created && @event.Name == txtFileName);
        Assert.Contains(events, @event => @event.ChangeType == WatcherChangeTypes.Created && @event.Name == jsonFileName);
        Assert.Contains(events, @event => @event.ChangeType == WatcherChangeTypes.Created && @event.Name == binFileName);
    }
    
    
    [Fact]
    public async Task SubscribeChanges_MustDeliverEventToEverySubscriber()
    {
        var testDirectory = $"./IntegrationTest/{nameof(SubscribeChanges_MustDeliverEventToEverySubscriber)}";
        PrepareDirectory(testDirectory);
        
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var firstListenerEvents = new List<FileSystemEvent>();
        var secondListenerEvents = new List<FileSystemEvent>();

        var firstDaemon = Client.Program.Proxy<IFileSystemDaemon>(new Client.Configurations.Client($"ws://{_ipAddress.ToString()}:{_port}/"));
        var firstChangesReader = await firstDaemon.SubscribeChanges(testDirectory);

        var firstSubscribed = await firstChangesReader.WaitToReadAsync(token);
        Assert.True(firstSubscribed);
        var firstSubscribeConfirmation = await firstChangesReader.ReadAsync(token);
        Assert.Equal(WatcherChangeTypes.All, firstSubscribeConfirmation.ChangeType);
        Assert.Equal(testDirectory, firstSubscribeConfirmation.FullPath);

        await CreateTxtFile(testDirectory);
        await firstChangesReader.WaitToReadAsync(token);
        
        var secondDaemon = Client.Program.Proxy<IFileSystemDaemon>(new Client.Configurations.Client($"ws://{_ipAddress.ToString()}:{_port}/"));
        var secondChangesReader = await secondDaemon.SubscribeChanges(testDirectory);
        var secondSubscribed = await secondChangesReader.WaitToReadAsync(token);
        Assert.True(secondSubscribed);
        var secondSubscribeConfirmation = await secondChangesReader.ReadAsync(token);
        Assert.Equal(WatcherChangeTypes.All, secondSubscribeConfirmation.ChangeType);
        Assert.Equal(testDirectory, secondSubscribeConfirmation.FullPath);

        await CreateJsonFile(testDirectory);
        await secondChangesReader.WaitToReadAsync(token);
        
        while (firstListenerEvents.Count < 4 && await firstChangesReader.WaitToReadAsync(token))
        {
            var fsEvent = await firstChangesReader.ReadAsync(token);
            firstListenerEvents.Add(fsEvent);
        }
        await firstChangesReader.Cancel();
        
        await CreateBinFile(testDirectory);
        while (secondListenerEvents.Count < 4 && await secondChangesReader.WaitToReadAsync(token))
        {
            var fsEvent = await secondChangesReader.ReadAsync(token);
            secondListenerEvents.Add(fsEvent);
        }

        await secondChangesReader.Cancel();
        while (await firstChangesReader.WaitToReadAsync(token))
        {
            var fsEvent = await firstChangesReader.ReadAsync(token);
            firstListenerEvents.Add(fsEvent);
        }
        while (await secondChangesReader.WaitToReadAsync(token))
        {
            var fsEvent = await secondChangesReader.ReadAsync(token);
            secondListenerEvents.Add(fsEvent);
        }
        
        Assert.NotEmpty(firstListenerEvents);
        Assert.Equal(new[] {txtFileName, jsonFileName}, firstListenerEvents.Select(e => e.Name).Distinct());
        
        Assert.NotEmpty(secondListenerEvents);
        Assert.Equal(new[] {jsonFileName, binFileName}, secondListenerEvents.Select(e => e.Name).Distinct());
    }

    private static async Task CreateTxtFile(string path)
    {
        await using var fs = File.CreateText($"{path}/{txtFileName}");
        await fs.WriteAsync("lorem ipsum dolor sit amet");
        await fs.FlushAsync();
    }
    private static async Task CreateJsonFile(string path)
    {
        await using var fs = File.CreateText($"{path}/{jsonFileName}");
        await fs.WriteAsync("{\"message\": \"lorem ipsum dolor sit amet\"}");
        await fs.FlushAsync();
    }
    private static async Task CreateBinFile(string path)
    { 
        await using var fs = File.Create($"{path}/{binFileName}");
        await fs.WriteAsync(_randomBytes);
        await fs.FlushAsync();
    }

    private static void PrepareDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        foreach (var file in Directory.EnumerateFiles(path))
            File.Delete(file);
    }
    
    public async void Dispose()
    {
        await _service.StopAsync(CancellationToken.None);
        await _serviceProvider.DisposeAsync();
    }
}