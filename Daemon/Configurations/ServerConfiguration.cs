using System.Net;

namespace Daemon.Configurations;

public class ServerConfiguration
{
    public string IpAddress { get; set; } = IPAddress.Loopback.ToString();
    public int Port { get; set; } = 5000;

}