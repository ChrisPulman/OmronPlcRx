// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmronPlcRx.Core.Responses;
using OmronPlcRx.Core.Results;

namespace OmronPlcRx.Core.Channels;

/// <summary>Represents the t cp ch an ne l type.</summary>
internal sealed class TCPChannel : BaseChannel
{
    /// <summary>Stores the t cp he ad er le ng th value.</summary>
    private const int TcpHeaderLength = 16;

    /// <summary>Stores the c li en t value.</summary>
    private TcpClient? _client;

    /// <summary>Initializes a new instance of the <see cref="TCPChannel"/> class.</summary>
    /// <param name="remoteHost">The r em ot eh os t value.</param>
    /// <param name="port">The p or t value.</param>
    internal TCPChannel(string remoteHost, int port)
        : base(remoteHost, port)
    {
    }

    /// <summary>Represents the e nt cp co mm an dc od e enumeration.</summary>
    internal enum EnTCPCommandCode : byte
    {
        /// <summary>Represents the n od ea dd re ss to pl c enum value.</summary>
        NodeAddressToPLC = 0,
        /// <summary>Represents the n od ea dd re ss fr om pl c enum value.</summary>
        NodeAddressFromPLC = 1,
        /// <summary>Represents the f in sf ra me enum value.</summary>
        FINSFrame = 2,
    }

    /// <summary>Gets the local node id value.</summary>
    internal byte LocalNodeID { get; private set; }

    /// <summary>Gets the remote node id value.</summary>
    internal byte RemoteNodeID { get; private set; }

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
            throw new OmronPLCException("Failed to Re-Connect to Omron PLC '" + RemoteHost + ":" + Port + "' - The underlying Socket Connection was Closed");
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

    protected override Task<SendMessageResult> SendMessageAsync(ReadOnlyMemory<byte> message, int timeout, CancellationToken cancellationToken) => SendMessageAsync1(EnTCPCommandCode.FINSFrame, message, timeout, cancellationToken);

    protected override Task<ReceiveMessageResult> ReceiveMessageAsync(int timeout, CancellationToken cancellationToken) => ReceiveMessageAsync1(EnTCPCommandCode.FINSFrame, timeout, cancellationToken);

    protected override async Task PurgeReceiveBuffer(int timeout, CancellationToken cancellationToken)
    {
        var client = _client;
        if (client is null)
        {
            return;
        }

        try
        {
            if (!client.Connected)
            {
                return;
            }

            if (client.Available == 0)
            {
                await Task.Delay(timeout / 4, cancellationToken);
            }

            var startTimestamp = DateTime.UtcNow;
            var buffer = new byte[2000];

            while (client.Connected && client.Available > 0 && DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds < timeout)
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

    /// <summary>Initializes a new instance of the <see cref="BuildFinsTcpMessage"/> class.</summary>
    /// <param name="command">The c om ma nd value.</param>
    /// <param name="message">The m es sa ge value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static ReadOnlyMemory<byte> BuildFinsTcpMessage(EnTCPCommandCode command, ReadOnlyMemory<byte> message)
    {
        // FINS Message Identifier
        var tcpMessage = new List<byte>
        {
            (byte)'F',
            (byte)'I',
            (byte)'N',
            (byte)'S'
        };

        // Length of Message
        var length = BitConverter.GetBytes(Convert.ToUInt32(4 + 4 + message.Length));
        Array.Reverse(length);
        tcpMessage.AddRange(length); // Command + Error Code + Message Data

        // Command
        tcpMessage.Add(0);
        tcpMessage.Add(0);
        tcpMessage.Add(0);
        tcpMessage.Add((byte)command);

        // Error Code
        tcpMessage.Add(0);
        tcpMessage.Add(0);
        tcpMessage.Add(0);
        tcpMessage.Add(0);

        tcpMessage.AddRange(message.ToArray());

        return tcpMessage.ToArray();
    }

    /// <summary>Initializes a new instance of the <see cref="InitializeClient"/> class.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task InitializeClient(int timeout, CancellationToken cancellationToken)
    {
        _client = new(RemoteHost, Port);

        await _client.ConnectAsync(timeout, cancellationToken);

        try
        {
            // Send Auto-Assign Client Node Request
            var sendResult = await SendMessageAsync1(EnTCPCommandCode.NodeAddressToPLC, new byte[4], timeout, cancellationToken);

            // Receive Client Node ID
            var receiveResult = await ReceiveMessageAsync1(EnTCPCommandCode.NodeAddressFromPLC, timeout, cancellationToken);

            if (receiveResult.Message.Length < 8)
            {
                throw new OmronPLCException("Failed to Negotiate a TCP Connection with Omron PLC '" + RemoteHost + ":" + Port + "' - TCP Negotiation Message Length was too Short");
            }

            var tcpNegotiationMessage = receiveResult.Message.Slice(0, 8).ToArray();

            if (tcpNegotiationMessage[3] == 0 || tcpNegotiationMessage[3] == 255)
            {
                throw new OmronPLCException("Failed to Negotiate a TCP Connection with Omron PLC '" + RemoteHost + ":" + Port + "' - TCP Negotiation Message contained an Invalid Local Node ID");
            }

            LocalNodeID = tcpNegotiationMessage[3];

            if (tcpNegotiationMessage[7] == 0 || tcpNegotiationMessage[7] == 255)
            {
                throw new OmronPLCException("Failed to Negotiate a TCP Connection with Omron PLC '" + RemoteHost + ":" + Port + "' - TCP Negotiation Message contained an Invalid Remote Node ID");
            }

            RemoteNodeID = tcpNegotiationMessage[7];
        }
        catch (OmronPLCException e)
        {
            throw new OmronPLCException("Failed to Negotiate a TCP Connection with Omron PLC '" + RemoteHost + ":" + Port + "'", e);
        }
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

    /// <summary>Initializes a new instance of the <see cref="SendMessageAsync1"/> class.</summary>
    /// <param name="command">The c om ma nd value.</param>
    /// <param name="message">The m es sa ge value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task<SendMessageResult> SendMessageAsync1(EnTCPCommandCode command, ReadOnlyMemory<byte> message, int timeout, CancellationToken cancellationToken)
    {
        var client = _client ?? throw new OmronPLCException("Failed to Send FINS Message to Omron PLC '" + RemoteHost + ":" + Port + "' - The TCP Client is not Initialized");

        var tcpMessage = BuildFinsTcpMessage(command, message);
        var bytes = 0;
        var packets = 0;

        try
        {
            bytes += await client.SendAsync(tcpMessage.ToArray(), timeout, cancellationToken);
            packets++;
        }
        catch (ObjectDisposedException)
        {
            throw new OmronPLCException("Failed to Send FINS Message to Omron PLC '" + RemoteHost + ":" + Port + "' - The underlying Socket Connection was Closed");
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

    /// <summary>Initializes a new instance of the <see cref="ReceiveMessageAsync1"/> class.</summary>
    /// <param name="command">The c om ma nd value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task<ReceiveMessageResult> ReceiveMessageAsync1(EnTCPCommandCode command, int timeout, CancellationToken cancellationToken)
    {
        var bytes = 0;
        var packets = 0;
        Memory<byte> message = default;
        var client = _client ?? throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The TCP Client is not Initialized");

        try
        {
            var receivedData = new List<byte>();
            var startTimestamp = DateTime.UtcNow;

            while (DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds < timeout && receivedData.Count < TcpHeaderLength)
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

            if (receivedData.Count < TcpHeaderLength)
            {
                throw new OmronPLCException("Failed to Receive FINS Message within the Timeout Period from Omron PLC '" + RemoteHost + ":" + Port + "'");
            }

            if (receivedData[0] != 'F' || receivedData[1] != 'I' || receivedData[2] != 'N' || receivedData[3] != 'S')
            {
                throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The TCP Header was Invalid");
            }

            var tcpHeader = receivedData.GetRange(0, TcpHeaderLength).ToArray();

            var lengthBytes = new byte[] { receivedData[7], receivedData[6], receivedData[5], receivedData[4] };
            var tcpMessageDataLength = (int)BitConverter.ToUInt32(lengthBytes, 0) - 8;

            if (tcpMessageDataLength <= 0 || tcpMessageDataLength > short.MaxValue)
            {
                throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The TCP Message Length was Invalid");
            }

            if (receivedData[11] == 3 || receivedData[15] != 0)
            {
                throw receivedData[15] switch
                {
                    1 => new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - Omron TCP Error: The FINS Identifier (ASCII Code) was Invalid."),
                    2 => new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - Omron TCP Error: The Data Length is too Long."),
                    3 => new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - Omron TCP Error: The Command is not Supported."),
                    20 => new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - Omron TCP Error: All Connections are in Use."),
                    21 => new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - Omron TCP Error: The Specified Node is already Connected."),
                    22 => new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - Omron TCP Error: Attempt to Access a Protected Node from an Unspecified IP Address."),
                    23 => new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - Omron TCP Error: The Client FINS Node Address is out of Range."),
                    24 => new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - Omron TCP Error: The same FINS Node Address is being used by the Client and Server."),
                    25 => new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - Omron TCP Error: All the Node Addresses Available for Allocation have been Used."),
                    _ => new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - Omron TCP Error: Unknown Code '" + receivedData[15] + "'"),
                };
            }

            if (receivedData[8] != 0 || receivedData[9] != 0 || receivedData[10] != 0 || receivedData[11] != (byte)command)
            {
                throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The TCP Command Received '" + receivedData[11] + "' did not match Expected Command '" + (byte)command + "'");
            }

            if (command == EnTCPCommandCode.FINSFrame && tcpMessageDataLength < FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength)
            {
                throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The TCP Message Length was too short for a FINS Frame");
            }

            receivedData.RemoveRange(0, TcpHeaderLength);

            if (receivedData.Count < tcpMessageDataLength)
            {
                startTimestamp = DateTime.UtcNow;

                while (DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds < timeout && receivedData.Count < tcpMessageDataLength)
                {
                    var buffer = new byte[4096];
                    var remainingMs = (int)Math.Max(0, timeout - (DateTime.UtcNow - startTimestamp).TotalMilliseconds);

                    if (remainingMs >= 50)
                    {
                        var receivedBytes = await client.ReceiveAsync(buffer, remainingMs, cancellationToken);

                        if (receivedBytes > 0)
                        {
                            receivedData.AddRange(buffer.AsSpan(0, receivedBytes).ToArray());
                        }

                        bytes += receivedBytes;
                        packets++;
                    }
                }
            }

            if (receivedData.Count == 0)
            {
                throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - No Data was Received after TCP Header");
            }

            if (receivedData.Count < tcpMessageDataLength)
            {
                throw new OmronPLCException("Failed to Receive FINS Message within the Timeout Period from Omron PLC '" + RemoteHost + ":" + Port + "'");
            }

            if (command == EnTCPCommandCode.FINSFrame && receivedData[0] != 0xC0 && receivedData[0] != 0xC1)
            {
                throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The FINS Header was Invalid");
            }

            message = receivedData.ToArray();
        }
        catch (ObjectDisposedException)
        {
            throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The underlying Socket Connection was Closed");
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
}
