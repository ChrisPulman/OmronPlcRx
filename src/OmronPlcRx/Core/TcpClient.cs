// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OmronPlcRx.Core;

/// <summary>
/// TCP socket client wrapper providing connect, send and receive operations with timeout and cancellation support across multiple target frameworks.
/// </summary>
internal class TcpClient : IDisposable
{
    private readonly Socket _socket;
    private readonly string _remoteHost;
    private readonly int _remotePort;
    private bool _disposed;

    public TcpClient(string host, int port)
    {
        _remoteHost = host ?? throw new ArgumentNullException(nameof(host));

        if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "The Port Number specified is outside the valid Range of IPEndPoint.MinPort or IPEndPoint.MaxPort");
        }

        _remotePort = port;

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            LingerState = new LingerOption(true, 0),
        };
    }

    public TcpClient(IPAddress address, int port)
    {
        if (address == null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        _remoteHost = address.ToString();

        if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "The Port Number specified is outside the valid Range of IPEndPoint.MinPort or IPEndPoint.MaxPort");
        }

        _remotePort = port;

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            LingerState = new LingerOption(true, 0),
        };
    }

    internal TcpClient(Socket acceptedSocket)
    {
        if (acceptedSocket == null)
        {
            throw new ArgumentNullException(nameof(acceptedSocket));
        }

        if (acceptedSocket.LingerState == null)
        {
            acceptedSocket.LingerState = new LingerOption(true, 0);
        }
        else if (!acceptedSocket.LingerState.Enabled || acceptedSocket.LingerState.LingerTime != 0)
        {
            acceptedSocket.LingerState.Enabled = true;
            acceptedSocket.LingerState.LingerTime = 0;
        }

        if (acceptedSocket.RemoteEndPoint is IPEndPoint)
        {
            var dnsEndPoint = acceptedSocket.RemoteEndPoint as IPEndPoint;

            _remoteHost = dnsEndPoint?.Address.ToString() ?? string.Empty;
            _remotePort = dnsEndPoint?.Port ?? IPEndPoint.MinPort;
        }
        else if (acceptedSocket.RemoteEndPoint is DnsEndPoint)
        {
            var dnsEndPoint = acceptedSocket.RemoteEndPoint as DnsEndPoint;

            _remoteHost = dnsEndPoint?.Host ?? string.Empty;
            _remotePort = dnsEndPoint?.Port ?? IPEndPoint.MinPort;
        }
        else
        {
            _remoteHost = string.Empty;
            _remotePort = IPEndPoint.MinPort;
        }

        _socket = acceptedSocket;
    }

    public int Available => _disposed ? 0 : _socket.Available;

    public bool Connected => !_disposed && _socket.Connected;

    public Socket? Socket => _disposed ? null : _socket;

    public bool NoDelay
    {
        get
        {
            if (!_disposed)
            {
                return _socket.NoDelay;
            }

            return false;
        }

        set
        {
            if (!_disposed)
            {
                _socket.NoDelay = value;
            }
        }
    }

    public LingerOption? LingerState
    {
        get
        {
            if (!_disposed)
            {
                return _socket.LingerState;
            }

            return null;
        }

        set
        {
            if (!_disposed && value != null)
            {
                _socket.LingerState = value;
            }
        }
    }

    public bool KeepAliveEnabled
    {
        get
        {
            if (!_disposed)
            {
                var value = _socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive);

                if (value != null && value is bool)
                {
                    return Convert.ToBoolean(value);
                }
            }

            return false;
        }

        set
        {
            if (!_disposed)
            {
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, value);
            }
        }
    }

#if NET6_0_OR_GREATER
    public int KeepAliveInternal
    {
        get
        {
            if (!_disposed)
            {
                var value = _socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval);

                if (value != null && value is int)
                {
                    return Convert.ToInt32(value);
                }
            }

            return 0;
        }

        set
        {
            if (!_disposed)
            {
                _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, value);
            }
        }
    }

    public int KeepAliveDelay
    {
        get
        {
            if (!_disposed)
            {
                var value = _socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime);

                if (value != null && value is int)
                {
                    return Convert.ToInt32(value);
                }
            }

            return 0;
        }

        set
        {
            if (!_disposed)
            {
                _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, value);
            }
        }
    }

    public int KeepAliveRetryCount
    {
        get
        {
            if (!_disposed)
            {
                var value = _socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount);

                if (value != null && value is int)
                {
                    return Convert.ToInt32(value);
                }
            }

            return 0;
        }

        set
        {
            if (!_disposed)
            {
                _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, value);
            }
        }
    }
#endif

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public Task ConnectAsync(int timeout, CancellationToken cancellationToken) => ConnectAsync(TimeSpan.FromMilliseconds(timeout), cancellationToken);

    public async Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

#if NET6_0_OR_GREATER
        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            await _socket.ConnectAsync(_remoteHost, _remotePort, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var connectValueTask = _socket.ConnectAsync(_remoteHost, _remotePort, connectCts.Token);

        if (connectValueTask.IsCompleted || connectValueTask.IsCanceled || cancellationToken.IsCancellationRequested)
        {
            await connectValueTask.ConfigureAwait(false);
            return;
        }

        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var connectTask = connectValueTask.AsTask();
        var delayTask = Task.Delay(timeout, delayCts.Token);

        if (connectTask == await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false))
        {
            delayCts.Cancel();

            try
            {
                await delayTask.ConfigureAwait(false);
            }
            catch
            {
            }

            await connectTask.ConfigureAwait(false);
            return;
        }

        connectCts.Cancel();

        try
        {
            await connectTask.ConfigureAwait(false);
        }
        catch
        {
        }

        await delayTask.ConfigureAwait(false);

        throw new TimeoutException("Failed to Connect to the Remote Host '" + _remoteHost + ":" + _remotePort.ToString() + "' within the Timeout Period");
#else
        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            await Task.Factory.FromAsync(_socket.BeginConnect, _socket.EndConnect, _remoteHost, _remotePort, null).ConfigureAwait(false);
            return;
        }

        using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var connectTask = Task.Factory.FromAsync(_socket.BeginConnect, _socket.EndConnect, _remoteHost, _remotePort, null);

            if (connectTask.IsCompleted || connectTask.IsCanceled || cancellationToken.IsCancellationRequested)
            {
                await connectTask.ConfigureAwait(false);
                return;
            }

            using (var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var delayTask = Task.Delay(timeout, delayCts.Token);

                if (connectTask == await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false))
                {
                    delayCts.Cancel();

                    try
                    {
                        await delayTask.ConfigureAwait(false);
                    }
                    catch
                    {
                    }

                    await connectTask.ConfigureAwait(false);
                    return;
                }

                connectCts.Cancel();

                try
                {
                    await connectTask.ConfigureAwait(false);
                }
                catch
                {
                }

                await delayTask.ConfigureAwait(false);

                throw new TimeoutException("Failed to Connect to the Remote Host '" + _remoteHost + ":" + _remotePort.ToString() + "' within the Timeout Period");
            }
        }
#endif
    }

    public Task<int> SendAsync(byte[] buffer, CancellationToken cancellationToken) => SendAsync(buffer, Timeout.InfiniteTimeSpan, cancellationToken);

    public Task<int> SendAsync(byte[] buffer, int timeout, CancellationToken cancellationToken) => SendAsync(buffer, TimeSpan.FromMilliseconds(timeout), cancellationToken);

#if NET6_0_OR_GREATER
    public Task<int> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => SendAsync(buffer, Timeout.InfiniteTimeSpan, cancellationToken);

    public Task<int> SendAsync(ReadOnlyMemory<byte> buffer, int timeout, CancellationToken cancellationToken) => SendAsync(buffer, TimeSpan.FromMilliseconds(timeout), cancellationToken);

    public async Task<int> SendAsync(ReadOnlyMemory<byte> buffer, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            return await _socket.SendAsync(buffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
        }

        using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sendValueTask = _socket.SendAsync(buffer, SocketFlags.None, sendCts.Token);

        if (sendValueTask.IsCompleted || sendValueTask.IsCanceled || cancellationToken.IsCancellationRequested)
        {
            return await sendValueTask.ConfigureAwait(false);
        }

        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sendTask = sendValueTask.AsTask();
        var delayTask = Task.Delay(timeout, delayCts.Token);

        if (sendTask == await Task.WhenAny(sendTask, delayTask).ConfigureAwait(false))
        {
            delayCts.Cancel();

            try
            {
                await delayTask.ConfigureAwait(false);
            }
            catch
            {
            }

            return await sendTask.ConfigureAwait(false);
        }

        sendCts.Cancel();

        try
        {
            await sendTask.ConfigureAwait(false);
        }
        catch
        {
        }

        await delayTask.ConfigureAwait(false);

        throw new TimeoutException("Failed to Send to the Remote Host '" + _remoteHost + ":" + _remotePort.ToString() + "' within the Timeout Period");
    }
#endif

    public async Task<int> SendAsync(byte[] buffer, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

#if NET6_0_OR_GREATER
        return await SendAsync(buffer.AsMemory(), timeout, cancellationToken).ConfigureAwait(false);
#else
        Func<AsyncCallback, object, IAsyncResult> begin = (callback, state) => _socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, callback, state);

        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            return await Task.Factory.FromAsync(begin, _socket.EndSend, null).ConfigureAwait(false);
        }

        using (var sendCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var sendTask = Task.Factory.FromAsync(begin, _socket.EndSend, null);

            if (sendTask.IsCompleted || sendTask.IsCanceled || cancellationToken.IsCancellationRequested)
            {
                return await sendTask.ConfigureAwait(false);
            }

            using (var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var delayTask = Task.Delay(timeout, delayCts.Token);

                if (sendTask == await Task.WhenAny(sendTask, delayTask).ConfigureAwait(false))
                {
                    delayCts.Cancel();

                    try
                    {
                        await delayTask.ConfigureAwait(false);
                    }
                    catch
                    {
                    }

                    return await sendTask.ConfigureAwait(false);
                }

                sendCts.Cancel();

                try
                {
                    await sendTask.ConfigureAwait(false);
                }
                catch
                {
                }

                await delayTask.ConfigureAwait(false);

                throw new TimeoutException("Failed to Send to the Remote Host '" + _remoteHost + ":" + _remotePort.ToString() + "' within the Timeout Period");
            }
        }
#endif
    }

    public Task<int> ReceiveAsync(byte[] buffer, CancellationToken cancellationToken) => ReceiveAsync(buffer, Timeout.InfiniteTimeSpan, cancellationToken);

    public Task<int> ReceiveAsync(byte[] buffer, int timeout, CancellationToken cancellationToken) => ReceiveAsync(buffer, TimeSpan.FromMilliseconds(timeout), cancellationToken);

#if NET6_0_OR_GREATER
    public Task<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ReceiveAsync(buffer, Timeout.InfiniteTimeSpan, cancellationToken);

    public Task<int> ReceiveAsync(Memory<byte> buffer, int timeout, CancellationToken cancellationToken) => ReceiveAsync(buffer, TimeSpan.FromMilliseconds(timeout), cancellationToken);

    public async Task<int> ReceiveAsync(Memory<byte> buffer, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            return await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
        }

        using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiveValueTask = _socket.ReceiveAsync(buffer, SocketFlags.None, receiveCts.Token);

        if (receiveValueTask.IsCompleted || receiveValueTask.IsCanceled || cancellationToken.IsCancellationRequested)
        {
            return await receiveValueTask.ConfigureAwait(false);
        }

        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiveTask = receiveValueTask.AsTask();
        var delayTask = Task.Delay(timeout, delayCts.Token);

        if (receiveTask == await Task.WhenAny(receiveTask, delayTask).ConfigureAwait(false))
        {
            delayCts.Cancel();

            try
            {
                await delayTask.ConfigureAwait(false);
            }
            catch
            {
            }

            return await receiveTask.ConfigureAwait(false);
        }

        receiveCts.Cancel();

        try
        {
            await receiveTask.ConfigureAwait(false);
        }
        catch
        {
        }

        await delayTask.ConfigureAwait(false);

        throw new TimeoutException("Failed to Receive from the Remote Host '" + _remoteHost + ":" + _remotePort.ToString() + "' within the Timeout Period");
    }
#endif

    public async Task<int> ReceiveAsync(byte[] buffer, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

#if NET6_0_OR_GREATER
        return await ReceiveAsync(buffer.AsMemory(), timeout, cancellationToken).ConfigureAwait(false);
#else
        Func<AsyncCallback, object, IAsyncResult> begin = (callback, state) => _socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, callback, state);

        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            return await Task.Factory.FromAsync(begin, _socket.EndReceive, null).ConfigureAwait(false);
        }

        using (var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var receiveTask = Task.Factory.FromAsync(begin, _socket.EndReceive, null);

            if (receiveTask.IsCompleted || receiveTask.IsCanceled || cancellationToken.IsCancellationRequested)
            {
                return await receiveTask.ConfigureAwait(false);
            }

            using (var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var delayTask = Task.Delay(timeout, delayCts.Token);

                if (receiveTask == await Task.WhenAny(receiveTask, delayTask).ConfigureAwait(false))
                {
                    delayCts.Cancel();

                    try
                    {
                        await delayTask.ConfigureAwait(false);
                    }
                    catch
                    {
                    }

                    return await receiveTask.ConfigureAwait(false);
                }

                receiveCts.Cancel();

                try
                {
                    await receiveTask.ConfigureAwait(false);
                }
                catch
                {
                }

                await delayTask.ConfigureAwait(false);

                throw new TimeoutException("Failed to Receive from the Remote Host '" + _remoteHost + ":" + _remotePort.ToString() + "' within the Timeout Period");
            }
        }
#endif
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            if (_socket != null)
            {
                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                }
                finally
                {
                    _socket.Dispose();
                }
            }
        }

        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
