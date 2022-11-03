namespace Client;

public sealed class Client
{
    public Uri Uri { get; }

    public Client(string addr)
    {
        Uri = new Uri(addr);
    }
}