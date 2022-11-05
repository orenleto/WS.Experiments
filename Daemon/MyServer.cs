using System.Net;
using System.Net.Sockets;
using Daemon.NetCoreServer;

namespace Daemon;

public class MyServer : WsServer
{
    private readonly SubscriptionManager _subscriptionManager;

    public MyServer(IPAddress address, int port) : base(address, port)
    {
        _subscriptionManager = new SubscriptionManager();
    }
    
    protected override TcpSession CreateSession() { return new ClientSession(this, _subscriptionManager); }

    protected override void OnError(SocketError error)
    {
        Console.WriteLine($"MyServer WebSocket server caught an error with code {error}");
    }

    protected override void Dispose(bool disposingManagedResources)
    {
        Console.WriteLine("MyServer WebSocket serverdisposed");
        _subscriptionManager.Dispose();
        base.Dispose(disposingManagedResources);
    }
}