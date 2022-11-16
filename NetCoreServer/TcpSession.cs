using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetCoreServer;

/// <summary>
///     TCP session is used to read and write data from the connected TCP client
/// </summary>
/// <remarks>Thread-safe</remarks>
public class TcpSession : IDisposable
{
    /// <summary>
    ///     Initialize the session with a given server
    /// </summary>
    /// <param name="server">TCP server</param>
    public TcpSession(TcpServer server)
    {
        Id = Guid.NewGuid();
        Server = server;
        OptionReceiveBufferSize = server.OptionReceiveBufferSize;
        OptionSendBufferSize = server.OptionSendBufferSize;
    }

    /// <summary>
    ///     Session Id
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    ///     Server
    /// </summary>
    public TcpServer Server { get; }

    /// <summary>
    ///     Socket
    /// </summary>
    public Socket Socket { get; private set; }

    /// <summary>
    ///     Number of bytes pending sent by the session
    /// </summary>
    public long BytesPending { get; private set; }

    /// <summary>
    ///     Number of bytes sending by the session
    /// </summary>
    public long BytesSending { get; private set; }

    /// <summary>
    ///     Number of bytes sent by the session
    /// </summary>
    public long BytesSent { get; private set; }

    /// <summary>
    ///     Number of bytes received by the session
    /// </summary>
    public long BytesReceived { get; private set; }

    /// <summary>
    ///     Option: receive buffer limit
    /// </summary>
    public int OptionReceiveBufferLimit { get; set; } = 0;

    /// <summary>
    ///     Option: receive buffer size
    /// </summary>
    public int OptionReceiveBufferSize { get; set; } = 8192;

    /// <summary>
    ///     Option: send buffer limit
    /// </summary>
    public int OptionSendBufferLimit { get; set; } = 0;

    /// <summary>
    ///     Option: send buffer size
    /// </summary>
    public int OptionSendBufferSize { get; set; } = 8192;

    #region Error handling

    /// <summary>
    ///     Send error notification
    /// </summary>
    /// <param name="error">Socket error code</param>
    private void SendError(SocketError error)
    {
        // Skip disconnect errors
        if (error == SocketError.ConnectionAborted ||
            error == SocketError.ConnectionRefused ||
            error == SocketError.ConnectionReset ||
            error == SocketError.OperationAborted ||
            error == SocketError.Shutdown)
            return;

        OnError(error);
    }

    #endregion

    #region Connect/Disconnect session

    /// <summary>
    ///     Is the session connected?
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    ///     Connect the session
    /// </summary>
    /// <param name="socket">Session socket</param>
    internal void Connect(Socket socket)
    {
        Socket = socket;

        // Update the session socket disposed flag
        IsSocketDisposed = false;

        // Setup buffers
        _receiveBuffer = new Buffer();
        _sendBufferMain = new Buffer();
        _sendBufferFlush = new Buffer();

        // Setup event args
        _receiveEventArg = new SocketAsyncEventArgs();
        _receiveEventArg.Completed += OnAsyncCompleted;
        _sendEventArg = new SocketAsyncEventArgs();
        _sendEventArg.Completed += OnAsyncCompleted;

        // Apply the option: keep alive
        if (Server.OptionKeepAlive)
            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        if (Server.OptionTcpKeepAliveTime >= 0)
            Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, Server.OptionTcpKeepAliveTime);
        if (Server.OptionTcpKeepAliveInterval >= 0)
            Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, Server.OptionTcpKeepAliveInterval);
        if (Server.OptionTcpKeepAliveRetryCount >= 0)
            Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, Server.OptionTcpKeepAliveRetryCount);
        // Apply the option: no delay
        if (Server.OptionNoDelay)
            Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

        // Prepare receive & send buffers
        _receiveBuffer.Reserve(OptionReceiveBufferSize);
        _sendBufferMain.Reserve(OptionSendBufferSize);
        _sendBufferFlush.Reserve(OptionSendBufferSize);

        // Reset statistic
        BytesPending = 0;
        BytesSending = 0;
        BytesSent = 0;
        BytesReceived = 0;

        // Call the session connecting handler
        OnConnecting();

        // Call the session connecting handler in the server
        Server.OnConnectingInternal(this);

        // Update the connected flag
        IsConnected = true;

        // Try to receive something from the client
        TryReceive();

        // Check the socket disposed state: in some rare cases it might be disconnected while receiving!
        if (IsSocketDisposed)
            return;

        // Call the session connected handler
        OnConnected();

        // Call the session connected handler in the server
        Server.OnConnectedInternal(this);

        // Call the empty send buffer handler
        if (_sendBufferMain.IsEmpty)
            OnEmpty();
    }

    /// <summary>
    ///     Disconnect the session
    /// </summary>
    /// <returns>'true' if the section was successfully disconnected, 'false' if the section is already disconnected</returns>
    public virtual bool Disconnect()
    {
        if (!IsConnected)
            return false;

        // Reset event args
        _receiveEventArg.Completed -= OnAsyncCompleted;
        _sendEventArg.Completed -= OnAsyncCompleted;

        // Call the session disconnecting handler
        OnDisconnecting();

        // Call the session disconnecting handler in the server
        Server.OnDisconnectingInternal(this);

        try
        {
            try
            {
                // Shutdown the socket associated with the client
                Socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
            }

            // Close the session socket
            Socket.Close();

            // Dispose the session socket
            Socket.Dispose();

            // Dispose event arguments
            _receiveEventArg.Dispose();
            _sendEventArg.Dispose();

            // Update the session socket disposed flag
            IsSocketDisposed = true;
        }
        catch (ObjectDisposedException)
        {
        }

        // Update the connected flag
        IsConnected = false;

        // Update sending/receiving flags
        _receiving = false;
        _sending = false;

        // Clear send/receive buffers
        ClearBuffers();

        // Call the session disconnected handler
        OnDisconnected();

        // Call the session disconnected handler in the server
        Server.OnDisconnectedInternal(this);

        // Unregister session
        Server.UnregisterSession(Id);

        return true;
    }

    #endregion

    #region Send/Recieve data

    // Receive buffer
    private bool _receiving;
    private Buffer _receiveBuffer;

    private SocketAsyncEventArgs _receiveEventArg;

    // Send buffer
    private readonly object _sendLock = new();
    private bool _sending;
    private Buffer _sendBufferMain;
    private Buffer _sendBufferFlush;
    private SocketAsyncEventArgs _sendEventArg;
    private long _sendBufferFlushOffset;

    /// <summary>
    ///     Send data to the client (synchronous)
    /// </summary>
    /// <param name="buffer">Buffer to send</param>
    /// <returns>Size of sent data</returns>
    public virtual long Send(byte[] buffer)
    {
        return Send(buffer.AsSpan());
    }

    /// <summary>
    ///     Send data to the client (synchronous)
    /// </summary>
    /// <param name="buffer">Buffer to send</param>
    /// <param name="offset">Buffer offset</param>
    /// <param name="size">Buffer size</param>
    /// <returns>Size of sent data</returns>
    public virtual long Send(byte[] buffer, long offset, long size)
    {
        return Send(buffer.AsSpan((int)offset, (int)size));
    }

    /// <summary>
    ///     Send data to the client (synchronous)
    /// </summary>
    /// <param name="buffer">Buffer to send as a span of bytes</param>
    /// <returns>Size of sent data</returns>
    public virtual long Send(ReadOnlySpan<byte> buffer)
    {
        if (!IsConnected)
            return 0;

        if (buffer.IsEmpty)
            return 0;

        // Sent data to the client
        long sent = Socket.Send(buffer, SocketFlags.None, out var ec);
        if (sent > 0)
        {
            // Update statistic
            BytesSent += sent;
            Interlocked.Add(ref Server._bytesSent, sent);

            // Call the buffer sent handler
            OnSent(sent, BytesPending + BytesSending);
        }

        // Check for socket error
        if (ec != SocketError.Success)
        {
            SendError(ec);
            Disconnect();
        }

        return sent;
    }

    /// <summary>
    ///     Send text to the client (synchronous)
    /// </summary>
    /// <param name="text">Text string to send</param>
    /// <returns>Size of sent data</returns>
    public virtual long Send(string text)
    {
        return Send(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    ///     Send text to the client (synchronous)
    /// </summary>
    /// <param name="text">Text to send as a span of characters</param>
    /// <returns>Size of sent data</returns>
    public virtual long Send(ReadOnlySpan<char> text)
    {
        return Send(Encoding.UTF8.GetBytes(text.ToArray()));
    }

    /// <summary>
    ///     Send data to the client (asynchronous)
    /// </summary>
    /// <param name="buffer">Buffer to send</param>
    /// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
    public virtual bool SendAsync(byte[] buffer)
    {
        return SendAsync(buffer.AsSpan());
    }

    /// <summary>
    ///     Send data to the client (asynchronous)
    /// </summary>
    /// <param name="buffer">Buffer to send</param>
    /// <param name="offset">Buffer offset</param>
    /// <param name="size">Buffer size</param>
    /// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
    public virtual bool SendAsync(byte[] buffer, long offset, long size)
    {
        return SendAsync(buffer.AsSpan((int)offset, (int)size));
    }

    /// <summary>
    ///     Send data to the client (asynchronous)
    /// </summary>
    /// <param name="buffer">Buffer to send as a span of bytes</param>
    /// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
    public virtual bool SendAsync(ReadOnlySpan<byte> buffer)
    {
        if (!IsConnected)
            return false;

        if (buffer.IsEmpty)
            return true;

        lock (_sendLock)
        {
            // Check the send buffer limit
            if (_sendBufferMain.Size + buffer.Length > OptionSendBufferLimit && OptionSendBufferLimit > 0)
            {
                SendError(SocketError.NoBufferSpaceAvailable);
                return false;
            }

            // Fill the main send buffer
            _sendBufferMain.Append(buffer);

            // Update statistic
            BytesPending = _sendBufferMain.Size;

            // Avoid multiple send handlers
            if (_sending)
                return true;
            _sending = true;

            // Try to send the main buffer
            TrySend();
        }

        return true;
    }

    /// <summary>
    ///     Send text to the client (asynchronous)
    /// </summary>
    /// <param name="text">Text string to send</param>
    /// <returns>'true' if the text was successfully sent, 'false' if the session is not connected</returns>
    public virtual bool SendAsync(string text)
    {
        return SendAsync(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    ///     Send text to the client (asynchronous)
    /// </summary>
    /// <param name="text">Text to send as a span of characters</param>
    /// <returns>'true' if the text was successfully sent, 'false' if the session is not connected</returns>
    public virtual bool SendAsync(ReadOnlySpan<char> text)
    {
        return SendAsync(Encoding.UTF8.GetBytes(text.ToArray()));
    }

    /// <summary>
    ///     Receive data from the client (synchronous)
    /// </summary>
    /// <param name="buffer">Buffer to receive</param>
    /// <returns>Size of received data</returns>
    public virtual long Receive(byte[] buffer)
    {
        return Receive(buffer, 0, buffer.Length);
    }

    /// <summary>
    ///     Receive data from the client (synchronous)
    /// </summary>
    /// <param name="buffer">Buffer to receive</param>
    /// <param name="offset">Buffer offset</param>
    /// <param name="size">Buffer size</param>
    /// <returns>Size of received data</returns>
    public virtual long Receive(byte[] buffer, long offset, long size)
    {
        if (!IsConnected)
            return 0;

        if (size == 0)
            return 0;

        // Receive data from the client
        long received = Socket.Receive(buffer, (int)offset, (int)size, SocketFlags.None, out var ec);
        if (received > 0)
        {
            // Update statistic
            BytesReceived += received;
            Interlocked.Add(ref Server._bytesReceived, received);

            // Call the buffer received handler
            OnReceived(buffer, 0, received);
        }

        // Check for socket error
        if (ec != SocketError.Success)
        {
            SendError(ec);
            Disconnect();
        }

        return received;
    }

    /// <summary>
    ///     Receive text from the client (synchronous)
    /// </summary>
    /// <param name="size">Text size to receive</param>
    /// <returns>Received text</returns>
    public virtual string Receive(long size)
    {
        var buffer = new byte[size];
        var length = Receive(buffer);
        return Encoding.UTF8.GetString(buffer, 0, (int)length);
    }

    /// <summary>
    ///     Receive data from the client (asynchronous)
    /// </summary>
    public virtual void ReceiveAsync()
    {
        // Try to receive data from the client
        TryReceive();
    }

    /// <summary>
    ///     Try to receive new data
    /// </summary>
    private void TryReceive()
    {
        if (_receiving)
            return;

        if (!IsConnected)
            return;

        var process = true;

        while (process)
        {
            process = false;

            try
            {
                // Async receive with the receive handler
                _receiving = true;
                _receiveEventArg.SetBuffer(_receiveBuffer.Data, 0, (int)_receiveBuffer.Capacity);
                if (!Socket.ReceiveAsync(_receiveEventArg))
                    process = ProcessReceive(_receiveEventArg);
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>
    ///     Try to send pending data
    /// </summary>
    private void TrySend()
    {
        if (!IsConnected)
            return;

        var empty = false;
        var process = true;

        while (process)
        {
            process = false;

            lock (_sendLock)
            {
                // Is previous socket send in progress?
                if (_sendBufferFlush.IsEmpty)
                {
                    // Swap flush and main buffers
                    _sendBufferFlush = Interlocked.Exchange(ref _sendBufferMain, _sendBufferFlush);
                    _sendBufferFlushOffset = 0;

                    // Update statistic
                    BytesPending = 0;
                    BytesSending += _sendBufferFlush.Size;

                    // Check if the flush buffer is empty
                    if (_sendBufferFlush.IsEmpty)
                    {
                        // Need to call empty send buffer handler
                        empty = true;

                        // End sending process
                        _sending = false;
                    }
                }
                else
                {
                    return;
                }
            }

            // Call the empty send buffer handler
            if (empty)
            {
                OnEmpty();
                return;
            }

            try
            {
                // Async write with the write handler
                _sendEventArg.SetBuffer(_sendBufferFlush.Data, (int)_sendBufferFlushOffset, (int)(_sendBufferFlush.Size - _sendBufferFlushOffset));
                if (!Socket.SendAsync(_sendEventArg))
                    process = ProcessSend(_sendEventArg);
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>
    ///     Clear send/receive buffers
    /// </summary>
    private void ClearBuffers()
    {
        lock (_sendLock)
        {
            // Clear send buffers
            _sendBufferMain.Clear();
            _sendBufferFlush.Clear();
            _sendBufferFlushOffset = 0;

            // Update statistic
            BytesPending = 0;
            BytesSending = 0;
        }
    }

    #endregion

    #region IO processing

    /// <summary>
    ///     This method is called whenever a receive or send operation is completed on a socket
    /// </summary>
    private void OnAsyncCompleted(object sender, SocketAsyncEventArgs e)
    {
        if (IsSocketDisposed)
            return;

        // Determine which type of operation just completed and call the associated handler
        switch (e.LastOperation)
        {
            case SocketAsyncOperation.Receive:
                if (ProcessReceive(e))
                    TryReceive();
                break;
            case SocketAsyncOperation.Send:
                if (ProcessSend(e))
                    TrySend();
                break;
            default:
                throw new ArgumentException("The last operation completed on the socket was not a receive or send");
        }
    }

    /// <summary>
    ///     This method is invoked when an asynchronous receive operation completes
    /// </summary>
    private bool ProcessReceive(SocketAsyncEventArgs e)
    {
        if (!IsConnected)
            return false;

        long size = e.BytesTransferred;

        // Received some data from the client
        if (size > 0)
        {
            // Update statistic
            BytesReceived += size;
            Interlocked.Add(ref Server._bytesReceived, size);

            // Call the buffer received handler
            OnReceived(_receiveBuffer.Data, 0, size);

            // If the receive buffer is full increase its size
            if (_receiveBuffer.Capacity == size)
            {
                // Check the receive buffer limit
                if (2 * size > OptionReceiveBufferLimit && OptionReceiveBufferLimit > 0)
                {
                    SendError(SocketError.NoBufferSpaceAvailable);
                    Disconnect();
                    return false;
                }

                _receiveBuffer.Reserve(2 * size);
            }
        }

        _receiving = false;

        // Try to receive again if the session is valid
        if (e.SocketError == SocketError.Success)
        {
            // If zero is returned from a read operation, the remote end has closed the connection
            if (size > 0)
                return true;
            Disconnect();
        }
        else
        {
            SendError(e.SocketError);
            Disconnect();
        }

        return false;
    }

    /// <summary>
    ///     This method is invoked when an asynchronous send operation completes
    /// </summary>
    private bool ProcessSend(SocketAsyncEventArgs e)
    {
        if (!IsConnected)
            return false;

        long size = e.BytesTransferred;

        // Send some data to the client
        if (size > 0)
        {
            // Update statistic
            BytesSending -= size;
            BytesSent += size;
            Interlocked.Add(ref Server._bytesSent, size);

            // Increase the flush buffer offset
            _sendBufferFlushOffset += size;

            // Successfully send the whole flush buffer
            if (_sendBufferFlushOffset == _sendBufferFlush.Size)
            {
                // Clear the flush buffer
                _sendBufferFlush.Clear();
                _sendBufferFlushOffset = 0;
            }

            // Call the buffer sent handler
            OnSent(size, BytesPending + BytesSending);
        }

        // Try to send again if the session is valid
        if (e.SocketError == SocketError.Success)
        {
            return true;
        }

        SendError(e.SocketError);
        Disconnect();
        return false;
    }

    #endregion

    #region Session handlers

    /// <summary>
    ///     Handle client connecting notification
    /// </summary>
    protected virtual void OnConnecting()
    {
    }

    /// <summary>
    ///     Handle client connected notification
    /// </summary>
    protected virtual void OnConnected()
    {
    }

    /// <summary>
    ///     Handle client disconnecting notification
    /// </summary>
    protected virtual void OnDisconnecting()
    {
    }

    /// <summary>
    ///     Handle client disconnected notification
    /// </summary>
    protected virtual void OnDisconnected()
    {
    }

    /// <summary>
    ///     Handle buffer received notification
    /// </summary>
    /// <param name="buffer">Received buffer</param>
    /// <param name="offset">Received buffer offset</param>
    /// <param name="size">Received buffer size</param>
    /// <remarks>
    ///     Notification is called when another chunk of buffer was received from the client
    /// </remarks>
    protected virtual void OnReceived(byte[] buffer, long offset, long size)
    {
    }

    /// <summary>
    ///     Handle buffer sent notification
    /// </summary>
    /// <param name="sent">Size of sent buffer</param>
    /// <param name="pending">Size of pending buffer</param>
    /// <remarks>
    ///     Notification is called when another chunk of buffer was sent to the client.
    ///     This handler could be used to send another buffer to the client for instance when the pending size is zero.
    /// </remarks>
    protected virtual void OnSent(long sent, long pending)
    {
    }

    /// <summary>
    ///     Handle empty send buffer notification
    /// </summary>
    /// <remarks>
    ///     Notification is called when the send buffer is empty and ready for a new data to send.
    ///     This handler could be used to send another buffer to the client.
    /// </remarks>
    protected virtual void OnEmpty()
    {
    }

    /// <summary>
    ///     Handle error notification
    /// </summary>
    /// <param name="error">Socket error code</param>
    protected virtual void OnError(SocketError error)
    {
    }

    #endregion

    #region IDisposable implementation

    /// <summary>
    ///     Disposed flag
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    ///     Session socket disposed flag
    /// </summary>
    public bool IsSocketDisposed { get; private set; } = true;

    // Implement IDisposable.
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposingManagedResources)
    {
        // The idea here is that Dispose(Boolean) knows whether it is
        // being called to do explicit cleanup (the Boolean is true)
        // versus being called due to a garbage collection (the Boolean
        // is false). This distinction is useful because, when being
        // disposed explicitly, the Dispose(Boolean) method can safely
        // execute code using reference type fields that refer to other
        // objects knowing for sure that these other objects have not been
        // finalized or disposed of yet. When the Boolean is false,
        // the Dispose(Boolean) method should not execute code that
        // refer to reference type fields because those objects may
        // have already been finalized."

        if (!IsDisposed)
        {
            if (disposingManagedResources)
                // Dispose managed resources here...
                Disconnect();

            // Dispose unmanaged resources here...

            // Set large fields to null here...

            // Mark as disposed.
            IsDisposed = true;
        }
    }

    #endregion
}

/// <summary>
///     TCP server is used to connect, disconnect and manage TCP sessions
/// </summary>
/// <remarks>Thread-safe</remarks>
public class TcpServer : IDisposable
{
    /// <summary>
    ///     Initialize TCP server with a given IP address and port number
    /// </summary>
    /// <param name="address">IP address</param>
    /// <param name="port">Port number</param>
    public TcpServer(IPAddress address, int port) : this(new IPEndPoint(address, port))
    {
    }

    /// <summary>
    ///     Initialize TCP server with a given IP address and port number
    /// </summary>
    /// <param name="address">IP address</param>
    /// <param name="port">Port number</param>
    public TcpServer(string address, int port) : this(new IPEndPoint(IPAddress.Parse(address), port))
    {
    }

    /// <summary>
    ///     Initialize TCP server with a given DNS endpoint
    /// </summary>
    /// <param name="endpoint">DNS endpoint</param>
    public TcpServer(DnsEndPoint endpoint) : this(endpoint, endpoint.Host, endpoint.Port)
    {
    }

    /// <summary>
    ///     Initialize TCP server with a given IP endpoint
    /// </summary>
    /// <param name="endpoint">IP endpoint</param>
    public TcpServer(IPEndPoint endpoint) : this(endpoint, endpoint.Address.ToString(), endpoint.Port)
    {
    }

    /// <summary>
    ///     Initialize TCP server with a given endpoint, address and port
    /// </summary>
    /// <param name="endpoint">Endpoint</param>
    /// <param name="address">Server address</param>
    /// <param name="port">Server port</param>
    private TcpServer(EndPoint endpoint, string address, int port)
    {
        Id = Guid.NewGuid();
        Address = address;
        Port = port;
        Endpoint = endpoint;
    }

    /// <summary>
    ///     Server Id
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    ///     TCP server address
    /// </summary>
    public string Address { get; }

    /// <summary>
    ///     TCP server port
    /// </summary>
    public int Port { get; }

    /// <summary>
    ///     Endpoint
    /// </summary>
    public EndPoint Endpoint { get; private set; }

    /// <summary>
    ///     Number of sessions connected to the server
    /// </summary>
    public long ConnectedSessions => Sessions.Count;

    /// <summary>
    ///     Number of bytes pending sent by the server
    /// </summary>
    public long BytesPending => _bytesPending;

    /// <summary>
    ///     Number of bytes sent by the server
    /// </summary>
    public long BytesSent => _bytesSent;

    /// <summary>
    ///     Number of bytes received by the server
    /// </summary>
    public long BytesReceived => _bytesReceived;

    /// <summary>
    ///     Option: acceptor backlog size
    /// </summary>
    /// <remarks>
    ///     This option will set the listening socket's backlog size
    /// </remarks>
    public int OptionAcceptorBacklog { get; set; } = 1024;

    /// <summary>
    ///     Option: dual mode socket
    /// </summary>
    /// <remarks>
    ///     Specifies whether the Socket is a dual-mode socket used for both IPv4 and IPv6.
    ///     Will work only if socket is bound on IPv6 address.
    /// </remarks>
    public bool OptionDualMode { get; set; }

    /// <summary>
    ///     Option: keep alive
    /// </summary>
    /// <remarks>
    ///     This option will setup SO_KEEPALIVE if the OS support this feature
    /// </remarks>
    public bool OptionKeepAlive { get; set; }

    /// <summary>
    ///     Option: TCP keep alive time
    /// </summary>
    /// <remarks>
    ///     The number of seconds a TCP connection will remain alive/idle before keepalive probes are sent to the remote
    /// </remarks>
    public int OptionTcpKeepAliveTime { get; set; } = -1;

    /// <summary>
    ///     Option: TCP keep alive interval
    /// </summary>
    /// <remarks>
    ///     The number of seconds a TCP connection will wait for a keepalive response before sending another keepalive probe
    /// </remarks>
    public int OptionTcpKeepAliveInterval { get; set; } = -1;

    /// <summary>
    ///     Option: TCP keep alive retry count
    /// </summary>
    /// <remarks>
    ///     The number of TCP keep alive probes that will be sent before the connection is terminated
    /// </remarks>
    public int OptionTcpKeepAliveRetryCount { get; set; } = -1;

    /// <summary>
    ///     Option: no delay
    /// </summary>
    /// <remarks>
    ///     This option will enable/disable Nagle's algorithm for TCP protocol
    /// </remarks>
    public bool OptionNoDelay { get; set; }

    /// <summary>
    ///     Option: reuse address
    /// </summary>
    /// <remarks>
    ///     This option will enable/disable SO_REUSEADDR if the OS support this feature
    /// </remarks>
    public bool OptionReuseAddress { get; set; }

    /// <summary>
    ///     Option: enables a socket to be bound for exclusive access
    /// </summary>
    /// <remarks>
    ///     This option will enable/disable SO_EXCLUSIVEADDRUSE if the OS support this feature
    /// </remarks>
    public bool OptionExclusiveAddressUse { get; set; }

    /// <summary>
    ///     Option: receive buffer size
    /// </summary>
    public int OptionReceiveBufferSize { get; set; } = 8192;

    /// <summary>
    ///     Option: send buffer size
    /// </summary>
    public int OptionSendBufferSize { get; set; } = 8192;

    #region Session factory

    /// <summary>
    ///     Create TCP session factory method
    /// </summary>
    /// <returns>TCP session</returns>
    protected virtual TcpSession CreateSession()
    {
        return new TcpSession(this);
    }

    #endregion

    #region Error handling

    /// <summary>
    ///     Send error notification
    /// </summary>
    /// <param name="error">Socket error code</param>
    private void SendError(SocketError error)
    {
        // Skip disconnect errors
        if (error == SocketError.ConnectionAborted ||
            error == SocketError.ConnectionRefused ||
            error == SocketError.ConnectionReset ||
            error == SocketError.OperationAborted ||
            error == SocketError.Shutdown)
            return;

        OnError(error);
    }

    #endregion

    #region Start/Stop server

    // Server acceptor
    private Socket _acceptorSocket;
    private SocketAsyncEventArgs _acceptorEventArg;

    // Server statistic
    internal long _bytesPending;
    internal long _bytesSent;
    internal long _bytesReceived;

    /// <summary>
    ///     Is the server started?
    /// </summary>
    public bool IsStarted { get; private set; }

    /// <summary>
    ///     Is the server accepting new clients?
    /// </summary>
    public bool IsAccepting { get; private set; }

    /// <summary>
    ///     Create a new socket object
    /// </summary>
    /// <remarks>
    ///     Method may be override if you need to prepare some specific socket object in your implementation.
    /// </remarks>
    /// <returns>Socket object</returns>
    protected virtual Socket CreateSocket()
    {
        return new Socket(Endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
    }

    /// <summary>
    ///     Start the server
    /// </summary>
    /// <returns>'true' if the server was successfully started, 'false' if the server failed to start</returns>
    public virtual bool Start()
    {
        Debug.Assert(!IsStarted, "TCP server is already started!");
        if (IsStarted)
            return false;

        // Setup acceptor event arg
        _acceptorEventArg = new SocketAsyncEventArgs();
        _acceptorEventArg.Completed += OnAsyncCompleted;

        // Create a new acceptor socket
        _acceptorSocket = CreateSocket();

        // Update the acceptor socket disposed flag
        IsSocketDisposed = false;

        // Apply the option: reuse address
        _acceptorSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, OptionReuseAddress);
        // Apply the option: exclusive address use
        _acceptorSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, OptionExclusiveAddressUse);
        // Apply the option: dual mode (this option must be applied before listening)
        if (_acceptorSocket.AddressFamily == AddressFamily.InterNetworkV6)
            _acceptorSocket.DualMode = OptionDualMode;

        // Bind the acceptor socket to the endpoint
        _acceptorSocket.Bind(Endpoint);
        // Refresh the endpoint property based on the actual endpoint created
        Endpoint = _acceptorSocket.LocalEndPoint;

        // Call the server starting handler
        OnStarting();

        // Start listen to the acceptor socket with the given accepting backlog size
        _acceptorSocket.Listen(OptionAcceptorBacklog);

        // Reset statistic
        _bytesPending = 0;
        _bytesSent = 0;
        _bytesReceived = 0;

        // Update the started flag
        IsStarted = true;

        // Call the server started handler
        OnStarted();

        // Perform the first server accept
        IsAccepting = true;
        StartAccept(_acceptorEventArg);

        return true;
    }

    /// <summary>
    ///     Stop the server
    /// </summary>
    /// <returns>'true' if the server was successfully stopped, 'false' if the server is already stopped</returns>
    public virtual bool Stop()
    {
        Debug.Assert(IsStarted, "TCP server is not started!");
        if (!IsStarted)
            return false;

        // Stop accepting new clients
        IsAccepting = false;

        // Reset acceptor event arg
        _acceptorEventArg.Completed -= OnAsyncCompleted;

        // Call the server stopping handler
        OnStopping();

        try
        {
            // Close the acceptor socket
            _acceptorSocket.Close();

            // Dispose the acceptor socket
            _acceptorSocket.Dispose();

            // Dispose event arguments
            _acceptorEventArg.Dispose();

            // Update the acceptor socket disposed flag
            IsSocketDisposed = true;
        }
        catch (ObjectDisposedException)
        {
        }

        // Disconnect all sessions
        DisconnectAll();

        // Update the started flag
        IsStarted = false;

        // Call the server stopped handler
        OnStopped();

        return true;
    }

    /// <summary>
    ///     Restart the server
    /// </summary>
    /// <returns>'true' if the server was successfully restarted, 'false' if the server failed to restart</returns>
    public virtual bool Restart()
    {
        if (!Stop())
            return false;

        while (IsStarted)
            Thread.Yield();

        return Start();
    }

    #endregion

    #region Accepting clients

    /// <summary>
    ///     Start accept a new client connection
    /// </summary>
    private void StartAccept(SocketAsyncEventArgs e)
    {
        // Socket must be cleared since the context object is being reused
        e.AcceptSocket = null;

        // Async accept a new client connection
        if (!_acceptorSocket.AcceptAsync(e))
            ProcessAccept(e);
    }

    /// <summary>
    ///     Process accepted client connection
    /// </summary>
    private void ProcessAccept(SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success)
        {
            // Create a new session to register
            var session = CreateSession();

            // Register the session
            RegisterSession(session);

            // Connect new session
            session.Connect(e.AcceptSocket);
        }
        else
        {
            SendError(e.SocketError);
        }

        // Accept the next client connection
        if (IsAccepting)
            StartAccept(e);
    }

    /// <summary>
    ///     This method is the callback method associated with Socket.AcceptAsync()
    ///     operations and is invoked when an accept operation is complete
    /// </summary>
    private void OnAsyncCompleted(object sender, SocketAsyncEventArgs e)
    {
        if (IsSocketDisposed)
            return;

        ProcessAccept(e);
    }

    #endregion

    #region Session management

    // Server sessions
    protected readonly ConcurrentDictionary<Guid, TcpSession> Sessions = new();

    /// <summary>
    ///     Disconnect all connected sessions
    /// </summary>
    /// <returns>'true' if all sessions were successfully disconnected, 'false' if the server is not started</returns>
    public virtual bool DisconnectAll()
    {
        if (!IsStarted)
            return false;

        // Disconnect all sessions
        foreach (var session in Sessions.Values)
            session.Disconnect();

        return true;
    }

    /// <summary>
    ///     Find a session with a given Id
    /// </summary>
    /// <param name="id">Session Id</param>
    /// <returns>Session with a given Id or null if the session it not connected</returns>
    public TcpSession FindSession(Guid id)
    {
        // Try to find the required session
        return Sessions.TryGetValue(id, out var result) ? result : null;
    }

    /// <summary>
    ///     Register a new session
    /// </summary>
    /// <param name="session">Session to register</param>
    internal void RegisterSession(TcpSession session)
    {
        // Register a new session
        Sessions.TryAdd(session.Id, session);
    }

    /// <summary>
    ///     Unregister session by Id
    /// </summary>
    /// <param name="id">Session Id</param>
    internal void UnregisterSession(Guid id)
    {
        // Unregister session by Id
        Sessions.TryRemove(id, out var _);
    }

    #endregion

    #region Multicasting

    /// <summary>
    ///     Multicast data to all connected sessions
    /// </summary>
    /// <param name="buffer">Buffer to multicast</param>
    /// <returns>'true' if the data was successfully multicasted, 'false' if the data was not multicasted</returns>
    public virtual bool Multicast(byte[] buffer)
    {
        return Multicast(buffer.AsSpan());
    }

    /// <summary>
    ///     Multicast data to all connected clients
    /// </summary>
    /// <param name="buffer">Buffer to multicast</param>
    /// <param name="offset">Buffer offset</param>
    /// <param name="size">Buffer size</param>
    /// <returns>'true' if the data was successfully multicasted, 'false' if the data was not multicasted</returns>
    public virtual bool Multicast(byte[] buffer, long offset, long size)
    {
        return Multicast(buffer.AsSpan((int)offset, (int)size));
    }

    /// <summary>
    ///     Multicast data to all connected clients
    /// </summary>
    /// <param name="buffer">Buffer to send as a span of bytes</param>
    /// <returns>'true' if the data was successfully multicasted, 'false' if the data was not multicasted</returns>
    public virtual bool Multicast(ReadOnlySpan<byte> buffer)
    {
        if (!IsStarted)
            return false;

        if (buffer.IsEmpty)
            return true;

        // Multicast data to all sessions
        foreach (var session in Sessions.Values)
            session.SendAsync(buffer);

        return true;
    }

    /// <summary>
    ///     Multicast text to all connected clients
    /// </summary>
    /// <param name="text">Text string to multicast</param>
    /// <returns>'true' if the text was successfully multicasted, 'false' if the text was not multicasted</returns>
    public virtual bool Multicast(string text)
    {
        return Multicast(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    ///     Multicast text to all connected clients
    /// </summary>
    /// <param name="text">Text to multicast as a span of characters</param>
    /// <returns>'true' if the text was successfully multicasted, 'false' if the text was not multicasted</returns>
    public virtual bool Multicast(ReadOnlySpan<char> text)
    {
        return Multicast(Encoding.UTF8.GetBytes(text.ToArray()));
    }

    #endregion

    #region Server handlers

    /// <summary>
    ///     Handle server starting notification
    /// </summary>
    protected virtual void OnStarting()
    {
    }

    /// <summary>
    ///     Handle server started notification
    /// </summary>
    protected virtual void OnStarted()
    {
    }

    /// <summary>
    ///     Handle server stopping notification
    /// </summary>
    protected virtual void OnStopping()
    {
    }

    /// <summary>
    ///     Handle server stopped notification
    /// </summary>
    protected virtual void OnStopped()
    {
    }

    /// <summary>
    ///     Handle session connecting notification
    /// </summary>
    /// <param name="session">Connecting session</param>
    protected virtual void OnConnecting(TcpSession session)
    {
    }

    /// <summary>
    ///     Handle session connected notification
    /// </summary>
    /// <param name="session">Connected session</param>
    protected virtual void OnConnected(TcpSession session)
    {
    }

    /// <summary>
    ///     Handle session disconnecting notification
    /// </summary>
    /// <param name="session">Disconnecting session</param>
    protected virtual void OnDisconnecting(TcpSession session)
    {
    }

    /// <summary>
    ///     Handle session disconnected notification
    /// </summary>
    /// <param name="session">Disconnected session</param>
    protected virtual void OnDisconnected(TcpSession session)
    {
    }

    /// <summary>
    ///     Handle error notification
    /// </summary>
    /// <param name="error">Socket error code</param>
    protected virtual void OnError(SocketError error)
    {
    }

    internal void OnConnectingInternal(TcpSession session)
    {
        OnConnecting(session);
    }

    internal void OnConnectedInternal(TcpSession session)
    {
        OnConnected(session);
    }

    internal void OnDisconnectingInternal(TcpSession session)
    {
        OnDisconnecting(session);
    }

    internal void OnDisconnectedInternal(TcpSession session)
    {
        OnDisconnected(session);
    }

    #endregion

    #region IDisposable implementation

    /// <summary>
    ///     Disposed flag
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    ///     Acceptor socket disposed flag
    /// </summary>
    public bool IsSocketDisposed { get; private set; } = true;

    // Implement IDisposable.
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposingManagedResources)
    {
        // The idea here is that Dispose(Boolean) knows whether it is
        // being called to do explicit cleanup (the Boolean is true)
        // versus being called due to a garbage collection (the Boolean
        // is false). This distinction is useful because, when being
        // disposed explicitly, the Dispose(Boolean) method can safely
        // execute code using reference type fields that refer to other
        // objects knowing for sure that these other objects have not been
        // finalized or disposed of yet. When the Boolean is false,
        // the Dispose(Boolean) method should not execute code that
        // refer to reference type fields because those objects may
        // have already been finalized."

        if (!IsDisposed)
        {
            if (disposingManagedResources)
                // Dispose managed resources here...
                Stop();

            // Dispose unmanaged resources here...

            // Set large fields to null here...

            // Mark as disposed.
            IsDisposed = true;
        }
    }

    #endregion
}