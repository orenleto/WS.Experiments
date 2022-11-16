using System;
using System.Collections.Generic;
using System.IO;
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

public class DaemonTests
{
    private static readonly IPAddress _ipAddress = IPAddress.Loopback;
    private static readonly int _port = 28933;

    [Fact]
    public async Task SubscribeChanges_MustReceiveAllDirectoryChanges()
    {
        var listenEventSlim = new ManualResetEventSlim();
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var events = new List<FileSystemEvent>();

        if (!Directory.Exists("./IntegrationTest"))
            Directory.CreateDirectory("./IntegrationTest");
        foreach (var file in Directory.EnumerateFiles("./IntegrationTest"))
            File.Delete(file);

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
        var serviceProvider = services.BuildServiceProvider();

        var service = serviceProvider.GetService<IHostedService>();
        Assert.NotNull(service);

        await service!.StartAsync(token);

        var daemon = Client.Program.Proxy<IFileSystemDaemon>(new Client.Configurations.Client($"ws://{_ipAddress.ToString()}:{_port}/"));
        var changesReader = await daemon.SubscribeChanges("./IntegrationTest");

        _ = Task.Run(async () =>
        {
            listenEventSlim.Wait();
            using (var fs = File.CreateText("./IntegrationTest/test_file.txt"))
            {
                await fs.WriteAsync("lorem ipsum dolor sit amet");
                await fs.FlushAsync();
            }
            
            using (var fs = File.CreateText("./IntegrationTest/test_file.json"))
            {
                await fs.WriteAsync("{\"message\": \"lorem ipsum dolor sit amet\"}");
                await fs.FlushAsync();
            }

            using (var fs = File.Create("./IntegrationTest/test_file.bin"))
            {
                var raw = new byte[256];
                Random.Shared.NextBytes(raw);
                await fs.WriteAsync(raw, token);
                await fs.FlushAsync(token);
            }
        });

        while (await changesReader.WaitToReadAsync(token))
        {
            var fsEvent = await changesReader.ReadAsync(token);
            if (fsEvent.ChangeType == WatcherChangeTypes.All)
                listenEventSlim.Set();
            
            events.Add(fsEvent);
            if (events.Count >= 7)
                changesReader.Cancel();
        }

        await service.StopAsync(token);

        Assert.Contains(events, @event => @event.ChangeType == WatcherChangeTypes.All && @event.FullPath == "./IntegrationTest");
        Assert.Contains(events, @event => @event.ChangeType == WatcherChangeTypes.Created && @event.Name == "test_file.txt");
        Assert.Contains(events, @event => @event.ChangeType == WatcherChangeTypes.Changed && @event.Name == "test_file.txt");
        Assert.Contains(events, @event => @event.ChangeType == WatcherChangeTypes.Created && @event.Name == "test_file.json");
        Assert.Contains(events, @event => @event.ChangeType == WatcherChangeTypes.Changed && @event.Name == "test_file.json");
        Assert.Contains(events, @event => @event.ChangeType == WatcherChangeTypes.Created && @event.Name == "test_file.bin");
        Assert.Contains(events, @event => @event.ChangeType == WatcherChangeTypes.Changed && @event.Name == "test_file.bin");
    }
}