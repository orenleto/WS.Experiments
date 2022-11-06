using Daemon.Configurations;
using Daemon.Impl;
using Daemon.Impl.Requests;
using Daemon.Interfaces;
using MediatR;

class Program
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
                services.AddOptions();
                services.AddMediatR(typeof(Request));
                services.Configure<ServerConfiguration>(hostContext.Configuration.GetSection("Server"));
                services.AddSingleton<ISubscriptionManager, SubscriptionManager>();
                services.AddHostedService<WebSocketServerService>();
            })
            .Build()
            .RunAsync();
    }
}