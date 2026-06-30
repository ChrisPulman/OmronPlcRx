// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
#if REACTIVE_SHIM
using OmronPlcRx.Reactive.Core.Responses;
using OmronPlcRx.Reactive.Core.Results;
#else
using OmronPlcRx.Core.Responses;
using OmronPlcRx.Core.Results;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Channels;
#else
namespace OmronPlcRx.Core.Channels;
#endif

/// <summary>Represents the u dp ch an ne l type.</summary>
internal sealed class UDPChannel : BaseChannel
{
    /// <summary>Stores the c li en t value.</summary>
    private UdpClient? _client;

    /// <summary>Initializes a new instance of the <see cref="UDPChannel"/> class.</summary>
    /// <param name="remoteHost">The r em ot eh os t value.</param>
    /// <param name="port">The p or t value.</param>
    internal UDPChannel(string remoteHost, int port)
        : base(remoteHost, port)
    {
    }

    public override void Dispose()
    {
        try
        {
            _client?.Dispose();
        }
        finally
        {
            _client = null;
            base.Dispose();
        }
    }

    internal override async Task InitializeAsync(int timeout, CancellationToken cancellationToken)
    {
        if (!Semaphore.Wait(0, cancellationToken))
        {
            await Semaphore.WaitAsync(cancellationToken);
        }

        try
        {
            DestroyClient();

            await InitializeClient(timeout, cancellationToken);
        }
        finally
        {
            _ = Semaphore.Release();
        }
    }

    protected override async Task DestroyAndInitializeClient(int timeout, CancellationToken cancellationToken)
    {
        DestroyClient();

        try
        {
            await InitializeClient(timeout, cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            throw new OmronPLCException("Failed to Re-Connect to Omron PLC '" + RemoteHost + ":" + Port + "' - The underlying Socket Connection has been Closed");
        }
        catch (TimeoutException)
        {
            throw new OmronPLCException("Failed to Re-Connect within the Timeout Period to Omron PLC '" + RemoteHost + ":" + Port + "'");
        }
        catch (System.Net.Sockets.SocketException e)
        {
            throw new OmronPLCException("Failed to Re-Connect to Omron PLC '" + RemoteHost + ":" + Port + "'", e);
        }
    }

    protected override async Task<SendMessageResult> SendMessageAsync(ReadOnlyMemory<byte> message, int timeout, CancellationToken cancellationToken)
    {
        var bytes = 0;
        var packets = 0;
        var client = _client ?? throw new OmronPLCException("Failed to Send FINS Message to Omron PLC '" + RemoteHost + ":" + Port + "' - The UDP Client is not Initialized");

        try
        {
            // OmronPlcRx.Sockets.UdpClient expects byte[] and int timeout
            bytes += await client.SendAsync(message.ToArray(), timeout, cancellationToken);
            packets++;
        }
        catch (ObjectDisposedException)
        {
            throw new OmronPLCException("Failed to Send FINS Message to Omron PLC '" + RemoteHost + ":" + Port + "' - The underlying Socket Connection has been Closed");
        }
        catch (TimeoutException)
        {
            throw new OmronPLCException("Failed to Send FINS Message within the Timeout Period to Omron PLC '" + RemoteHost + ":" + Port + "'");
        }
        catch (System.Net.Sockets.SocketException e)
        {
            throw new OmronPLCException("Failed to Send FINS Message to Omron PLC '" + RemoteHost + ":" + Port + "'", e);
        }

        return new SendMessageResult
        {
            Bytes = bytes,
            Packets = packets,
        };
    }

    protected override async Task<ReceiveMessageResult> ReceiveMessageAsync(int timeout, CancellationToken cancellationToken)
    {
        var bytes = 0;
        var packets = 0;
        Memory<byte> message = default;
        var client = _client ?? throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The UDP Client is not Initialized");

        try
        {
            var receivedData = new List<byte>();
            var startTimestamp = DateTime.UtcNow;

            while (DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds < timeout && receivedData.Count < FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength)
            {
                var buffer = new byte[4096];
                var remainingMs = (int)Math.Max(0, timeout - (DateTime.UtcNow - startTimestamp).TotalMilliseconds);

                if (remainingMs >= 50)
                {
                    var receivedBytes = await client.ReceiveAsync(buffer, remainingMs, cancellationToken);

                    if (receivedBytes > 0)
                    {
                        receivedData.AddRange(buffer.AsSpan(0, receivedBytes).ToArray());

                        bytes += receivedBytes;
                        packets++;
                    }
                }
            }

            if (receivedData.Count == 0)
            {
                throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - No Data was Received");
            }

            if (receivedData.Count < FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength)
            {
                throw new OmronPLCException("Failed to Receive FINS Message within the Timeout Period from Omron PLC '" + RemoteHost + ":" + Port + "'");
            }

            if (receivedData[0] != 0xC0 && receivedData[0] != 0xC1)
            {
                throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The FINS Header was Invalid");
            }

            message = receivedData.ToArray();
        }
        catch (ObjectDisposedException)
        {
            throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The underlying Socket Connection has been Closed");
        }
        catch (TimeoutException)
        {
            throw new OmronPLCException("Failed to Receive FINS Message within the Timeout Period from Omron PLC '" + RemoteHost + ":" + Port + "'");
        }
        catch (System.Net.Sockets.SocketException e)
        {
            throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "'", e);
        }

        return new ReceiveMessageResult
        {
            Bytes = bytes,
            Packets = packets,
            Message = message,
        };
    }

    protected override async Task PurgeReceiveBuffer(int timeout, CancellationToken cancellationToken)
    {
        var client = _client;
        if (client is null)
        {
            return;
        }

        try
        {
            if (client.Available == 0)
            {
                await Task.Delay(timeout / 4, cancellationToken);
            }

            var startTimestamp = DateTime.UtcNow;
            var buffer = new byte[2000];

            while (client.Available > 0 && DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds < timeout)
            {
                try
                {
                    await client.ReceiveAsync(buffer, timeout, cancellationToken);
                }
                catch (TimeoutException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (System.Net.Sockets.SocketException)
                {
                    return;
                }
            }
        }
        catch (TimeoutException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (System.Net.Sockets.SocketException)
        {
        }
    }

    /// <summary>Initializes a new instance of the <see cref="InitializeClient"/> class.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task InitializeClient(int timeout, CancellationToken cancellationToken)
    {
        _client = new(RemoteHost, Port);

        return Task.CompletedTask;
    }

    /// <summary>Initializes a new instance of the <see cref="DestroyClient"/> class.</summary>
    private void DestroyClient()
    {
        try
        {
            _client?.Dispose();
        }
        finally
        {
            _client = null;
        }
    }
}
