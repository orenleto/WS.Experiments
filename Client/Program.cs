using Castle.DynamicProxy;
using Client.Impl;
using Client.Impl.Payloads;
using Client.Interfaces;

namespace Client;

public class Program
{
    private static readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private static readonly ProxyGenerator _generator = new ProxyGenerator();
    public static async Task Main()
    {
        var debugMode = false;
        var token = _cts.Token;
        if (debugMode)
        {
            await Process(token, "ws://localhost:5000/", "/Users/uumka/Desktop/CV/", 0);
        }
        else
        {
            var first = Process(token, "ws://localhost:5000/", "/Users/uumka/Desktop/CV", 0);
            var second = Process(token, "ws://localhost:5000/", "/Users/uumka/Desktop/Tutorials", 2500);
            var third = Process(token, "ws://localhost:5000/", "/Users/uumka/Desktop/CV/\"Non-existant folder\"", 5000);
            var forth = Process(token, "ws://localhost:5000/", "/Users/uumka/Desktop/CV", 7500);
            await Task.WhenAll(first, second, third, forth);
        }
    }

    private static async Task Process(CancellationToken token, string uri, string path, int delay)
    {
        await Task.Delay(delay, token);
        var client = new Configurations.Client(uri);
        var daemon = Proxy<IFileSystemDaemon>(client);

        // absolute path is passed
        var changesReader = await daemon.SubscribeChanges(path);

        var count = 0;
        while (await changesReader.WaitToReadAsync(token))
        {
            var fsEvent = await changesReader.ReadAsync(token);
            DumpEvent(fsEvent, count);
            count++;
            if (count == 10)
            {
                changesReader.Cancel(); // release fileSystemListener on remote machine
            }
        }
        Console.WriteLine(delay * 100 + count);
    }

    private static void DumpEvent(FileSystemEvent fileSystemEvent, int count)
    {
        Console.WriteLine(fileSystemEvent + " ===> " + count);
    }

    private static T Proxy<T>(Configurations.Client client)
    {
        var webSocketTransport = new WebSocketTransport(client.Uri, _cts.Token);
        var proxyInterceptor = new ProxyInterceptor(webSocketTransport);
        return (T)_generator.CreateInterfaceProxyWithoutTarget(typeof(T), proxyInterceptor);
    }
    
}