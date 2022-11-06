namespace Client;

public class Program
{
    public static async Task Main()
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var first = Process(token, "ws://localhost:5000/", "/Users/uumka/Desktop/CV", 0);
        var second = Process(token, "ws://localhost:5000/", "/Users/uumka/Desktop/Tutorials", 2500);
        var third = Process(token, "ws://localhost:5000/", "/Users/uumka/Desktop/CV/\"Non-existant folder\"", 5000);
        var forth = Process(token, "ws://localhost:5000/", "/Users/uumka/Desktop/CV", 7500);
        await Task.WhenAll(first, second, third, forth);
    }

    private static async Task Process(CancellationToken token, string uri, string path, int delay)
    {
        await Task.Delay(delay, token);
        var client = new Client(uri);
        var daemon = Proxy<IFileSystemDaemon>(client);

        // absolute path is passed
        var changesReader = await daemon.SubscribeChanges(path);

        var count = 0;
        while (await changesReader.WaitToReadAsync(token))
        {
            var fsEvent = await changesReader.ReadAsync(token);
            DumpEvent(fsEvent);
            count++;
            if (count == 10)
            {
                changesReader.Cancel(); // release fileSystemListener on remote machine
            }
        }

        Console.WriteLine(delay * 100 + count);
    }

    private static void DumpEvent(FileSystemEvent fileSystemEvent)
    {
        Console.WriteLine(fileSystemEvent);
    }

    private static IFileSystemDaemon Proxy<T>(Client client)
    {
        var myProxy = new MyProxyClass(client);
        myProxy.Connect();
        return myProxy;
    }

    /*
    public static T Proxy<T>(Client client)
    {
        // в этом месте нужно наэмитить сетевую обёртку по интерфейсу (см. класс MyProxyClass)
        throw new NotImplementedException();
    }
    */
}