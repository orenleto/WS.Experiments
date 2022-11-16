using System.Net.Sockets;
using System.Text;

namespace NetCoreServer;

/// <summary>
///     WebSocket session
/// </summary>
/// <remarks> WebSocket session is used to read and write data from the connected WebSocket client. Thread-safe.</remarks>
public class WsSession : HttpSession, IWebSocket
{
    internal readonly WebSocket WebSocket;

    public WsSession(WsServer server) : base(server)
    {
        WebSocket = new WebSocket(this);
    }

    // WebSocket connection methods
    public virtual bool Close(int status)
    {
        SendCloseAsync(status, Span<byte>.Empty);
        base.Disconnect();
        return true;
    }

    #region WebSocket send text methods

    public long SendText(string text)
    {
        return SendText(Encoding.UTF8.GetBytes(text));
    }

    public long SendText(ReadOnlySpan<char> text)
    {
        return SendText(Encoding.UTF8.GetBytes(text.ToArray()));
    }

    public long SendText(byte[] buffer)
    {
        return SendText(buffer.AsSpan());
    }

    public long SendText(byte[] buffer, long offset, long size)
    {
        return SendText(buffer.AsSpan((int)offset, (int)size));
    }

    public long SendText(ReadOnlySpan<byte> buffer)
    {
        lock (WebSocket.WsSendLock)
        {
            WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, false, buffer);
            return Send(WebSocket.WsSendBuffer.AsSpan());
        }
    }

    public bool SendTextAsync(string text)
    {
        return SendTextAsync(Encoding.UTF8.GetBytes(text));
    }

    public bool SendTextAsync(ReadOnlySpan<char> text)
    {
        return SendTextAsync(Encoding.UTF8.GetBytes(text.ToArray()));
    }

    public bool SendTextAsync(byte[] buffer)
    {
        return SendTextAsync(buffer.AsSpan());
    }

    public bool SendTextAsync(byte[] buffer, long offset, long size)
    {
        return SendTextAsync(buffer.AsSpan((int)offset, (int)size));
    }

    public bool SendTextAsync(ReadOnlySpan<byte> buffer)
    {
        lock (WebSocket.WsSendLock)
        {
            WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_TEXT, false, buffer);
            return SendAsync(WebSocket.WsSendBuffer.AsSpan());
        }
    }

    #endregion

    #region WebSocket send binary methods

    public long SendBinary(string text)
    {
        return SendBinary(Encoding.UTF8.GetBytes(text));
    }

    public long SendBinary(ReadOnlySpan<char> text)
    {
        return SendBinary(Encoding.UTF8.GetBytes(text.ToArray()));
    }

    public long SendBinary(byte[] buffer)
    {
        return SendBinary(buffer.AsSpan());
    }

    public long SendBinary(byte[] buffer, long offset, long size)
    {
        return SendBinary(buffer.AsSpan((int)offset, (int)size));
    }

    public long SendBinary(ReadOnlySpan<byte> buffer)
    {
        lock (WebSocket.WsSendLock)
        {
            WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, false, buffer);
            return base.Send(WebSocket.WsSendBuffer.AsSpan());
        }
    }

    public bool SendBinaryAsync(string text)
    {
        return SendBinaryAsync(Encoding.UTF8.GetBytes(text));
    }

    public bool SendBinaryAsync(ReadOnlySpan<char> text)
    {
        return SendBinaryAsync(Encoding.UTF8.GetBytes(text.ToArray()));
    }

    public bool SendBinaryAsync(byte[] buffer)
    {
        return SendBinaryAsync(buffer.AsSpan());
    }

    public bool SendBinaryAsync(byte[] buffer, long offset, long size)
    {
        return SendBinaryAsync(buffer.AsSpan((int)offset, (int)size));
    }

    public bool SendBinaryAsync(ReadOnlySpan<byte> buffer)
    {
        lock (WebSocket.WsSendLock)
        {
            WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_BINARY, false, buffer);
            return base.SendAsync(WebSocket.WsSendBuffer.AsSpan());
        }
    }

    #endregion

    #region WebSocket send close methods

    public long SendClose(int status, string text)
    {
        return SendClose(status, Encoding.UTF8.GetBytes(text));
    }

    public long SendClose(int status, ReadOnlySpan<char> text)
    {
        return SendClose(status, Encoding.UTF8.GetBytes(text.ToArray()));
    }

    public long SendClose(int status, byte[] buffer)
    {
        return SendClose(status, buffer.AsSpan());
    }

    public long SendClose(int status, byte[] buffer, long offset, long size)
    {
        return SendClose(status, buffer.AsSpan((int)offset, (int)size));
    }

    public long SendClose(int status, ReadOnlySpan<byte> buffer)
    {
        lock (WebSocket.WsSendLock)
        {
            WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_CLOSE, false, buffer, status);
            return base.Send(WebSocket.WsSendBuffer.AsSpan());
        }
    }

    public bool SendCloseAsync(int status, string text)
    {
        return SendCloseAsync(status, Encoding.UTF8.GetBytes(text));
    }

    public bool SendCloseAsync(int status, ReadOnlySpan<char> text)
    {
        return SendCloseAsync(status, Encoding.UTF8.GetBytes(text.ToArray()));
    }

    public bool SendCloseAsync(int status, byte[] buffer)
    {
        return SendCloseAsync(status, buffer.AsSpan());
    }

    public bool SendCloseAsync(int status, byte[] buffer, long offset, long size)
    {
        return SendCloseAsync(status, buffer.AsSpan((int)offset, (int)size));
    }

    public bool SendCloseAsync(int status, ReadOnlySpan<byte> buffer)
    {
        lock (WebSocket.WsSendLock)
        {
            WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_CLOSE, false, buffer, status);
            return base.SendAsync(WebSocket.WsSendBuffer.AsSpan());
        }
    }

    #endregion

    #region WebSocket send ping methods

    public long SendPing(string text)
    {
        return SendPing(Encoding.UTF8.GetBytes(text));
    }

    public long SendPing(ReadOnlySpan<char> text)
    {
        return SendPing(Encoding.UTF8.GetBytes(text.ToArray()));
    }

    public long SendPing(byte[] buffer)
    {
        return SendPing(buffer.AsSpan());
    }

    public long SendPing(byte[] buffer, long offset, long size)
    {
        return SendPing(buffer.AsSpan((int)offset, (int)size));
    }

    public long SendPing(ReadOnlySpan<byte> buffer)
    {
        lock (WebSocket.WsSendLock)
        {
            WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, false, buffer);
            return base.Send(WebSocket.WsSendBuffer.AsSpan());
        }
    }

    public bool SendPingAsync(string text)
    {
        return SendPingAsync(Encoding.UTF8.GetBytes(text));
    }

    public bool SendPingAsync(ReadOnlySpan<char> text)
    {
        return SendPingAsync(Encoding.UTF8.GetBytes(text.ToArray()));
    }

    public bool SendPingAsync(byte[] buffer)
    {
        return SendPingAsync(buffer.AsSpan());
    }

    public bool SendPingAsync(byte[] buffer, long offset, long size)
    {
        return SendPingAsync(buffer.AsSpan((int)offset, (int)size));
    }

    public bool SendPingAsync(ReadOnlySpan<byte> buffer)
    {
        lock (WebSocket.WsSendLock)
        {
            WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PING, false, buffer);
            return base.SendAsync(WebSocket.WsSendBuffer.AsSpan());
        }
    }

    #endregion

    #region WebSocket send pong methods

    public long SendPong(string text)
    {
        return SendPong(Encoding.UTF8.GetBytes(text));
    }

    public long SendPong(ReadOnlySpan<char> text)
    {
        return SendPong(Encoding.UTF8.GetBytes(text.ToArray()));
    }

    public long SendPong(byte[] buffer)
    {
        return SendPong(buffer.AsSpan());
    }

    public long SendPong(byte[] buffer, long offset, long size)
    {
        return SendPong(buffer.AsSpan((int)offset, (int)size));
    }

    public long SendPong(ReadOnlySpan<byte> buffer)
    {
        lock (WebSocket.WsSendLock)
        {
            WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, false, buffer);
            return base.Send(WebSocket.WsSendBuffer.AsSpan());
        }
    }

    public bool SendPongAsync(string text)
    {
        return SendPongAsync(Encoding.UTF8.GetBytes(text));
    }

    public bool SendPongAsync(ReadOnlySpan<char> text)
    {
        return SendPongAsync(Encoding.UTF8.GetBytes(text.ToArray()));
    }

    public bool SendPongAsync(byte[] buffer)
    {
        return SendPongAsync(buffer.AsSpan());
    }

    public bool SendPongAsync(byte[] buffer, long offset, long size)
    {
        return SendPongAsync(buffer.AsSpan((int)offset, (int)size));
    }

    public bool SendPongAsync(ReadOnlySpan<byte> buffer)
    {
        lock (WebSocket.WsSendLock)
        {
            WebSocket.PrepareSendFrame(WebSocket.WS_FIN | WebSocket.WS_PONG, false, buffer);
            return base.SendAsync(WebSocket.WsSendBuffer.AsSpan());
        }
    }

    #endregion

    #region WebSocket receive methods

    public string ReceiveText()
    {
        var result = new Buffer();

        if (!WebSocket.WsHandshaked)
            return result.ExtractString(0, result.Data.Length);

        var cache = new Buffer();

        // Receive WebSocket frame data
        while (!WebSocket.WsFinalReceived)
        {
            while (!WebSocket.WsFrameReceived)
            {
                var required = WebSocket.RequiredReceiveFrameSize();
                cache.Resize(required);
                var received = base.Receive(cache.Data, 0, required);
                if (received != required)
                    return result.ExtractString(0, result.Data.Length);
                WebSocket.PrepareReceiveFrame(cache.Data, 0, received);
            }

            if (!WebSocket.WsFinalReceived)
                WebSocket.PrepareReceiveFrame(null, 0, 0);
        }

        // Copy WebSocket frame data
        result.Append(WebSocket.WsReceiveFinalBuffer);
        WebSocket.PrepareReceiveFrame(null, 0, 0);
        return result.ExtractString(0, result.Data.Length);
    }

    public Buffer ReceiveBinary()
    {
        var result = new Buffer();

        if (!WebSocket.WsHandshaked)
            return result;

        var cache = new Buffer();

        // Receive WebSocket frame data
        while (!WebSocket.WsFinalReceived)
        {
            while (!WebSocket.WsFrameReceived)
            {
                var required = WebSocket.RequiredReceiveFrameSize();
                cache.Resize(required);
                var received = base.Receive(cache.Data, 0, required);
                if (received != required)
                    return result;
                WebSocket.PrepareReceiveFrame(cache.Data, 0, received);
            }

            if (!WebSocket.WsFinalReceived)
                WebSocket.PrepareReceiveFrame(null, 0, 0);
        }

        // Copy WebSocket frame data
        result.Append(WebSocket.WsReceiveFinalBuffer);
        WebSocket.PrepareReceiveFrame(null, 0, 0);
        return result;
    }

    #endregion

    #region Session handlers

    protected override void OnDisconnecting()
    {
        if (WebSocket.WsHandshaked)
            OnWsDisconnecting();
    }

    protected override void OnDisconnected()
    {
        // Disconnect WebSocket
        if (WebSocket.WsHandshaked)
        {
            WebSocket.WsHandshaked = false;
            OnWsDisconnected();
        }

        // Reset WebSocket upgrade HTTP request and response
        Request.Clear();
        Response.Clear();

        // Clear WebSocket send/receive buffers
        WebSocket.ClearWsBuffers();

        // Initialize new WebSocket random nonce
        WebSocket.InitWsNonce();
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        // Check for WebSocket handshaked status
        if (WebSocket.WsHandshaked)
        {
            // Prepare receive frame
            WebSocket.PrepareReceiveFrame(buffer, offset, size);
            return;
        }

        base.OnReceived(buffer, offset, size);
    }

    protected override void OnReceivedRequestHeader(HttpRequest request)
    {
        // Check for WebSocket handshaked status
        if (WebSocket.WsHandshaked)
            return;

        // Try to perform WebSocket upgrade
        if (!WebSocket.PerformServerUpgrade(request, Response))
        {
            base.OnReceivedRequestHeader(request);
        }
    }

    protected override void OnReceivedRequest(HttpRequest request)
    {
        // Check for WebSocket handshaked status
        if (WebSocket.WsHandshaked)
        {
            // Prepare receive frame from the remaining request body
            var body = Request.Body;
            var data = Encoding.UTF8.GetBytes(body);
            WebSocket.PrepareReceiveFrame(data, 0, data.Length);
            return;
        }

        base.OnReceivedRequest(request);
    }

    protected override void OnReceivedRequestError(HttpRequest request, string error)
    {
        // Check for WebSocket handshaked status
        if (WebSocket.WsHandshaked)
        {
            OnError(new SocketError());
            return;
        }

        base.OnReceivedRequestError(request, error);
    }

    #endregion

    #region Web socket handlers

    public virtual void OnWsConnecting(HttpRequest request)
    {
    }

    public virtual void OnWsConnected(HttpResponse response)
    {
    }

    public virtual bool OnWsConnecting(HttpRequest request, HttpResponse response)
    {
        return true;
    }

    public virtual void OnWsConnected(HttpRequest request)
    {
    }

    public virtual void OnWsDisconnecting()
    {
    }

    public virtual void OnWsDisconnected()
    {
    }

    public virtual void OnWsReceived(byte[] buffer, long offset, long size)
    {
    }

    public virtual void OnWsClose(byte[] buffer, long offset, long size, int status = 1000)
    {
        Close(status);
    }

    public virtual void OnWsPing(byte[] buffer, long offset, long size)
    {
        SendPongAsync(buffer, offset, size);
    }

    public virtual void OnWsPong(byte[] buffer, long offset, long size)
    {
    }

    public virtual void OnWsError(string error)
    {
        OnError(SocketError.SocketError);
    }

    public virtual void OnWsError(SocketError error)
    {
        OnError(error);
    }

    public void SendUpgrade(HttpResponse response)
    {
        SendResponseAsync(response);
    }

    #endregion
}