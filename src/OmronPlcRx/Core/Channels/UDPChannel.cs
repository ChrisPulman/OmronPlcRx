// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmronPlcRx.Core.Responses;
using OmronPlcRx.Core.Results;

namespace OmronPlcRx.Core.Channels;

internal class UDPChannel : BaseChannel
{
    private UdpClient? _client;

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
        catch
        {
        }
        finally
        {
            _client = null;
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
            Semaphore.Release();
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
            throw new OmronException("Failed to Re-Connect to Omron PLC '" + RemoteHost + ":" + Port + "' - The underlying Socket Connection has been Closed");
        }
        catch (TimeoutException)
        {
            throw new OmronException("Failed to Re-Connect within the Timeout Period to Omron PLC '" + RemoteHost + ":" + Port + "'");
        }
        catch (System.Net.Sockets.SocketException e)
        {
            throw new OmronException("Failed to Re-Connect to Omron PLC '" + RemoteHost + ":" + Port + "'", e);
        }
    }

    protected override async Task<SendMessageResult> SendMessageAsync(ReadOnlyMemory<byte> message, int timeout, CancellationToken cancellationToken)
    {
        var result = new SendMessageResult
        {
            Bytes = 0,
            Packets = 0,
        };

        try
        {
            // OmronPlcRx.Sockets.UdpClient expects byte[] and int timeout
            result.Bytes += await _client.SendAsync(message.ToArray(), timeout, cancellationToken);
            result.Packets++;
        }
        catch (ObjectDisposedException)
        {
            throw new OmronException("Failed to Send FINS Message to Omron PLC '" + RemoteHost + ":" + Port + "' - The underlying Socket Connection has been Closed");
        }
        catch (TimeoutException)
        {
            throw new OmronException("Failed to Send FINS Message within the Timeout Period to Omron PLC '" + RemoteHost + ":" + Port + "'");
        }
        catch (System.Net.Sockets.SocketException e)
        {
            throw new OmronException("Failed to Send FINS Message to Omron PLC '" + RemoteHost + ":" + Port + "'", e);
        }

        return result;
    }

    protected override async Task<ReceiveMessageResult> ReceiveMessageAsync(int timeout, CancellationToken cancellationToken)
    {
        var result = new ReceiveMessageResult
        {
            Bytes = 0,
            Packets = 0,
            Message = default(Memory<byte>),
        };

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
                    var receivedBytes = await _client.ReceiveAsync(buffer, remainingMs, cancellationToken);

                    if (receivedBytes > 0)
                    {
                        receivedData.AddRange(buffer.AsSpan(0, receivedBytes).ToArray());

                        result.Bytes += receivedBytes;
                        result.Packets++;
                    }
                }
            }

            if (receivedData.Count == 0)
            {
                throw new OmronException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - No Data was Received");
            }

            if (receivedData.Count < FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength)
            {
                throw new OmronException("Failed to Receive FINS Message within the Timeout Period from Omron PLC '" + RemoteHost + ":" + Port + "'");
            }

            if (receivedData[0] != 0xC0 && receivedData[0] != 0xC1)
            {
                throw new OmronException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The FINS Header was Invalid");
            }

            result.Message = receivedData.ToArray();
        }
        catch (ObjectDisposedException)
        {
            throw new OmronException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The underlying Socket Connection has been Closed");
        }
        catch (TimeoutException)
        {
            throw new OmronException("Failed to Receive FINS Message within the Timeout Period from Omron PLC '" + RemoteHost + ":" + Port + "'");
        }
        catch (System.Net.Sockets.SocketException e)
        {
            throw new OmronException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "'", e);
        }

        return result;
    }

    protected override async Task PurgeReceiveBuffer(int timeout, CancellationToken cancellationToken)
    {
        try
        {
            if (_client?.Available == 0)
            {
                await Task.Delay(timeout / 4, cancellationToken);
            }

            var startTimestamp = DateTime.UtcNow;
            var buffer = new byte[2000];

            while (_client?.Available > 0 && DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds < timeout)
            {
                try
                {
                    await _client.ReceiveAsync(buffer, timeout, cancellationToken);
                }
                catch
                {
                    return;
                }
            }
        }
        catch
        {
        }
    }

    private Task InitializeClient(int timeout, CancellationToken cancellationToken)
    {
        _client = new UdpClient(RemoteHost, Port);

        return Task.CompletedTask;
    }

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
