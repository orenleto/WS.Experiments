using System.Runtime.CompilerServices;
using Daemon;

class Program
{
    static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .ConfigureServices((_, services) => { services.AddHostedService<WebSocketServerService>(); })
            .Build()
            .RunAsync();
    }


    // should use a logger but hey, it's a demo and it's free
    public static void ReportException(Exception ex, [CallerMemberName] string location = "(Caller name not set)")
    {
        Console.WriteLine($"\n{location}:\n  Exception {ex.GetType().Name}: {ex.Message}");
        if (ex.InnerException != null) Console.WriteLine($"  Inner Exception {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
    }
}