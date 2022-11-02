using Daemon;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        services.AddHostedService<DaemonService>();
    })
    .Build();

await host.RunAsync();