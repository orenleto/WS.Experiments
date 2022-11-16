namespace Client.Configurations;

public sealed class Client
{
    public Client(string addr)
    {
        Uri = new Uri(addr);
    }

    public Uri Uri { get; }
}