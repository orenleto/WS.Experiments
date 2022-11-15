using Daemon.Configurations;
using Daemon.Impl;
using Daemon.Interfaces;
using Daemon.IO;
using MediatR;

namespace Daemon;

public class Program
{
    static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddLogging();
                services.AddOptions();
                services.AddMediatR(typeof(Program));
                services.Configure<ServerConfiguration>(hostContext.Configuration.GetSection("Server"));
                services.AddSingleton<ISubscriptionManager, SubscriptionManager>();
                services.AddSingleton<IWatcherFactory, WatcherFactory>();
                services.AddHostedService<WebSocketServerService>();
            })
            .Build()
            .RunAsync();
    }
}