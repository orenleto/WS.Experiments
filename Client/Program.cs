using Castle.DynamicProxy;
using Client.Impl;
using Daemon.Contracts;
using Daemon.Contracts.Interfaces;
using Daemon.Contracts.Payloads.Events;

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
            count++;
            DumpEvent(fsEvent);
            if (count == 100)
            {
                changesReader.Cancel(); // release fileSystemListener on remote machine
            }
        }
        
        static void DumpEvent(FileSystemEvent fileSystemEvent)
        {
        }
    }
    
    public static T Proxy<T>(Configurations.Client client)
    {
        var webSocketTransport = new WebSocketTransport(client.Uri);
        var processingHandler = new ProcessingLoopHandler();
        var proxyInterceptor = new ProxyInterceptor<FileSystemEvent>(webSocketTransport, processingHandler);
        return (T)_generator.CreateInterfaceProxyWithoutTarget(typeof(T), proxyInterceptor);
    }
    
}