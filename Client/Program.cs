namespace Client;

public class Program
{
    public static async Task Main()
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        
        var client = new Client("ws://localhost:5000/");
        var daemon = Proxy<IFileSystemDaemon>(client);
        
        // absolute path is passed
        var changesReader = await daemon.SubscribeChanges("/Users/uumka/Desktop/CV");
        
        var count = 0;
        while (await changesReader.WaitToReadAsync(token))
        {
            var fsEvent = await changesReader.ReadAsync(token);
            DumpEvent(fsEvent);
            count++;
            if (count == 100)
            {
                changesReader.Cancel(); // release fileSystemListener on remote machine
            }
        }
    }

    private static void DumpEvent(FileSystemEvent fileSystemEvent)
    {
        Console.WriteLine(fileSystemEvent);
    }

    private static IFileSystemDaemon Proxy<T>(Client client) => new MyProxyClass(client); 
    /*
    public static T Proxy<T>(Client client)
    {
        // в этом месте нужно наэмитить сетевую обёртку по интерфейсу (см. класс MyProxyClass)
        throw new NotImplementedException();
    }
    */
}