// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OmronPlcRx.Core;

/// <summary>
/// UDP socket client wrapper providing send and receive operations with timeout and cancellation support across multiple target frameworks.
/// </summary>
internal class UdpClient : IDisposable
{
    private readonly Socket _socket;
    private readonly string _remoteHost;
    private readonly int _remotePort;
    private bool _disposed;

    public UdpClient(string host, int port)
    {
        _remoteHost = host ?? throw new ArgumentNullException(nameof(host));

        if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "The Port Number specified is outside the valid Range of IPEndPoint.MinPort or IPEndPoint.MaxPort");
        }

        _remotePort = port;

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.Connect(_remoteHost, _remotePort);
    }

    public UdpClient(IPAddress address, int port)
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

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.Connect(_remoteHost, _remotePort);
    }

    public int Available => _disposed ? 0 : _socket.Available;

    public Socket? Socket => _disposed ? null : _socket;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
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
        Func<AsyncCallback, object, IAsyncResult> beginSend = (callback, state) => _socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, callback, state);

        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            return await Task.Factory.FromAsync(beginSend, _socket.EndSend, null).ConfigureAwait(false);
        }

        using (var sendCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var sendTask = Task.Factory.FromAsync(beginSend, _socket.EndSend, null);

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
        Func<AsyncCallback, object, IAsyncResult> beginReceive = (callback, state) => _socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, callback, state);

        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            return await Task.Factory.FromAsync(beginReceive, _socket.EndReceive, null).ConfigureAwait(false);
        }

        using (var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var receiveTask = Task.Factory.FromAsync(beginReceive, _socket.EndReceive, null);

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
