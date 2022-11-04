using Daemon.Middlewares;

namespace Daemon;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // register our custom middleware since we use the IMiddleware factory approach
        services.AddTransient<WebSocketMiddleware>();
        //services.AddHostedService<DaemonService>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // enable websocket support
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(120),
        });

        // add our custom middleware to the pipeline
        app.UseMiddleware<WebSocketMiddleware>();
    }
}