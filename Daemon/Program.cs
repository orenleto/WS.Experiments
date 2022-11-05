using Daemon;
using Daemon.Configurations;

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
                services.Configure<ServerConfiguration>(hostContext.Configuration.GetSection("Server"));
                services.AddSingleton<ISubscriptionManager, SubscriptionManager>();
                services.AddHostedService<WebSocketServerService>();
            })
            .Build()
            .RunAsync();
    }
}