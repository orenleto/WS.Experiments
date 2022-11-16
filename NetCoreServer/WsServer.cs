using System.Net;
using System.Text;

namespace NetCoreServer;

/// <summary>
///     WebSocket server
/// </summary>
/// <remarks> WebSocket server is used to communicate with clients using WebSocket protocol. Thread-safe.</remarks>
public class WsServer : HttpServer, IWebSocket
{
    internal readonly WebSocket WebSocket;

    /// <summary>
    ///     Initialize WebSocket server with a given IP address and port number
    /// </summary>
    /// <param name="address">IP address</param>
    /// <param name="port">Port number</param>
    public WsServer(IPAddress address, int port) : base(address, port)
    {
        WebSocket = new WebSocket(this);
    }

    /// <summary>
    ///     Initialize WebSocket server with a given IP address and port number
    /// </summary>
    /// <param name="address">IP address</param>
    /// <param name="port">Port number</param>
    public WsServer(string address, int port) : base(address, port)
    {
        WebSocket = new WebSocket(this);
    }

    /// <summary>
    ///     Initialize WebSocket server with a given DNS endpoint
    /// </summary>
    /// <param name="endpoint">DNS endpoint</param>
    public WsServer(DnsEndPoint endpoint) : base(endpoint)
    {
        WebSocket = new WebSocket(this);
    }

    /// <summary>
    ///     Initialize WebSocket server with a given IP endpoint
    /// </summary>
    /// <param name="endpoint">IP endpoint</param>
    public WsServer(IPEndPoint endpoint) : base(endpoint)
    {
        WebSocket = new WebSocket(this);
    }

    #region Session management

    public virtual bool CloseAll(int status)
    {
        lock (WebSocket.WsSendLock)
        {
            WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_CLOSE, false, Span<byte>.Empty, status);
            if (!Multicast(WebSocket.WsSendBuffer.AsSpan()))
                return false;

            return base.DisconnectAll();
        }
    }

    #endregion

    #region Multicasting

    public override bool Multicast(ReadOnlySpan<byte> buffer)
    {
        if (!IsStarted)
            return false;

        if (buffer.IsEmpty)
            return true;

        // Multicast data to all WebSocket sessions
        foreach (var session in Sessions.Values)
            if (session is WsSession wsSession)
                if (wsSession.WebSocket.WsHandshaked)
                    wsSession.SendAsync(buffer);

        return true;
    }

    #endregion

    protected override TcpSession CreateSession()
    {
        return new WsSession(this);
    }

    #region WebSocket multicast text methods

    public bool MulticastText(string text)
    {
        return MulticastText(Encoding.UTF8.GetBytes(text));
    }

    public bool MulticastText(ReadOnlySpan<char> text)
    {
        return MulticastText(Encoding.UTF8.GetBytes(text.ToArray()));
    }

    public bool MulticastText(byte[] buffer)
    {
        return MulticastText(buffer.AsSpan());
    }

    public bool MulticastText(byte[] buffer, long offset, long size)
    {
        return MulticastText(buffer.AsSpan((int)offset, (int)size));
    }

    public bool MulticastText(ReadOnlySpan<byte> buffer)
    {
        lock (WebSocket.WsSendLock)
        {
            WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, false, buffer);
            return Multicast(WebSocket.WsSendBuffer.AsSpan());
        }
    }

    #endregion

    #region WebSocket multicast binary methods

    public bool MulticastBinary(string text)
    {
        return MulticastBinary(Encoding.UTF8.GetBytes(text));
    }

    public bool MulticastBinary(ReadOnlySpan<char> text)
    {
        return MulticastBinary(Encoding.UTF8.GetBytes(text.ToArray()));
    }

    public bool MulticastBinary(byte[] buffer)
    {
        return MulticastBinary(buffer.AsSpan());
    }

    public bool MulticastBinary(byte[] buffer, long offset, long size)
    {
        return MulticastBinary(buffer.AsSpan((int)offset, (int)size));
    }

    public bool MulticastBinary(ReadOnlySpan<byte> buffer)
    {
        lock (WebSocket.WsSendLock)
        {
            WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, false, buffer);
            return Multicast(WebSocket.WsSendBuffer.AsSpan());
        }
    }

    #endregion

    #region WebSocket multicast ping methods

    public bool MulticastPing(string text)
    {
        return MulticastPing(Encoding.UTF8.GetBytes(text));
    }

    public bool MulticastPing(ReadOnlySpan<char> text)
    {
        return MulticastPing(Encoding.UTF8.GetBytes(text.ToArray()));
    }

    public bool MulticastPing(byte[] buffer)
    {
        return MulticastPing(buffer.AsSpan());
    }

    public bool MulticastPing(byte[] buffer, long offset, long size)
    {
        return MulticastPing(buffer.AsSpan((int)offset, (int)size));
    }

    public bool MulticastPing(ReadOnlySpan<byte> buffer)
    {
        lock (WebSocket.WsSendLock)
        {
            WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, false, buffer);
            return Multicast(WebSocket.WsSendBuffer.AsSpan());
        }
    }

    #endregion
}