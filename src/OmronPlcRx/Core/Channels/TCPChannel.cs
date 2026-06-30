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

    /// <summary>Reads until the receive buffer reaches a target byte count or times out.</summary>
    /// <param name="client">The TCP client.</param>
    /// <param name="state">The receive state.</param>
    /// <param name="targetLength">The target byte count.</param>
    /// <param name="timeout">The timeout in milliseconds.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task ReadUntilCountAsync(TcpClient client, TcpReceiveState state, int targetLength, int timeout, CancellationToken cancellationToken)
    {
        var startTimestamp = DateTime.UtcNow;
        while (DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds < timeout && state.Data.Count < targetLength)
        {
            var remainingMs = (int)Math.Max(0, timeout - (DateTime.UtcNow - startTimestamp).TotalMilliseconds);
            if (remainingMs < 50)
            {
                return;
            }

            await ReadPacketAsync(client, state, remainingMs, cancellationToken);
        }
    }

    /// <summary>Reads one TCP packet into the receive state.</summary>
    /// <param name="client">The TCP client.</param>
    /// <param name="state">The receive state.</param>
    /// <param name="timeout">The timeout in milliseconds.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task ReadPacketAsync(TcpClient client, TcpReceiveState state, int timeout, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var receivedBytes = await client.ReceiveAsync(buffer, timeout, cancellationToken);
        if (receivedBytes <= 0)
        {
            return;
        }

        state.Data.AddRange(buffer.AsSpan(0, receivedBytes).ToArray());
        state.Bytes += receivedBytes;
        state.Packets++;
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
        var client = _client ?? throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The TCP Client is not Initialized");

        try
        {
            return await ReceiveTcpMessageAsync(client, command, timeout, cancellationToken);
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
    }

    /// <summary>Receives a complete TCP message.</summary>
    /// <param name="client">The TCP client.</param>
    /// <param name="command">The expected command.</param>
    /// <param name="timeout">The timeout in milliseconds.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task<ReceiveMessageResult> ReceiveTcpMessageAsync(TcpClient client, EnTCPCommandCode command, int timeout, CancellationToken cancellationToken)
    {
        var state = new TcpReceiveState();
        await ReadUntilCountAsync(client, state, TcpHeaderLength, timeout, cancellationToken);
        ValidateTcpHeader(state.Data);
        var tcpMessageDataLength = GetTcpMessageDataLength(state.Data);
        ThrowIfTcpError(state.Data);
        ValidateTcpCommand(state.Data, command);
        ValidateFinsFrameLength(command, tcpMessageDataLength);
        state.Data.RemoveRange(0, TcpHeaderLength);
        await ReadUntilCountAsync(client, state, tcpMessageDataLength, timeout, cancellationToken);
        ValidateTcpPayload(state.Data, command, tcpMessageDataLength);

        return new ReceiveMessageResult
        {
            Bytes = state.Bytes,
            Packets = state.Packets,
            Message = state.Data.ToArray(),
        };
    }

    /// <summary>Validates the fixed TCP header.</summary>
    /// <param name="receivedData">The received bytes.</param>
    private void ValidateTcpHeader(List<byte> receivedData)
    {
        var error = receivedData.Count switch
        {
            0 => "Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - No Data was Received",
            < TcpHeaderLength => "Failed to Receive FINS Message within the Timeout Period from Omron PLC '" + RemoteHost + ":" + Port + "'",
            _ when receivedData[0] != 'F' || receivedData[1] != 'I' || receivedData[2] != 'N' || receivedData[3] != 'S' => "Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The TCP Header was Invalid",
            _ => null,
        };

        _ = error is null ? 0 : throw new OmronPLCException(error);
    }

    /// <summary>Gets the TCP message data length from the header.</summary>
    /// <param name="receivedData">The received bytes.</param>
    /// <returns>The TCP message data length.</returns>
    private int GetTcpMessageDataLength(List<byte> receivedData)
    {
        var lengthBytes = new byte[] { receivedData[7], receivedData[6], receivedData[5], receivedData[4] };
        var tcpMessageDataLength = (int)BitConverter.ToUInt32(lengthBytes, 0) - 8;
        if (tcpMessageDataLength <= 0 || tcpMessageDataLength > short.MaxValue)
        {
            throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The TCP Message Length was Invalid");
        }

        return tcpMessageDataLength;
    }

    /// <summary>Throws when the TCP header contains an Omron TCP error.</summary>
    /// <param name="receivedData">The received bytes.</param>
    private void ThrowIfTcpError(List<byte> receivedData)
    {
        if (receivedData[11] != 3 && receivedData[15] == 0)
        {
            return;
        }

        throw CreateTcpErrorException(receivedData[15]);
    }

    /// <summary>Creates an Omron TCP error exception.</summary>
    /// <param name="errorCode">The Omron TCP error code.</param>
    /// <returns>The exception for the TCP error.</returns>
    private OmronPLCException CreateTcpErrorException(byte errorCode) => errorCode switch
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
        _ => new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - Omron TCP Error: Unknown Code '" + errorCode + "'"),
    };

    /// <summary>Validates the TCP command bytes.</summary>
    /// <param name="receivedData">The received bytes.</param>
    /// <param name="command">The expected command.</param>
    private void ValidateTcpCommand(List<byte> receivedData, EnTCPCommandCode command)
    {
        if (receivedData[8] == 0 && receivedData[9] == 0 && receivedData[10] == 0 && receivedData[11] == (byte)command)
        {
            return;
        }

        throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The TCP Command Received '" + receivedData[11] + "' did not match Expected Command '" + (byte)command + "'");
    }

    /// <summary>Validates the minimum FINS frame length.</summary>
    /// <param name="command">The TCP command.</param>
    /// <param name="tcpMessageDataLength">The TCP message data length.</param>
    private void ValidateFinsFrameLength(EnTCPCommandCode command, int tcpMessageDataLength)
    {
        if (command != EnTCPCommandCode.FINSFrame || tcpMessageDataLength >= FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength)
        {
            return;
        }

        throw new OmronPLCException("Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The TCP Message Length was too short for a FINS Frame");
    }

    /// <summary>Validates the TCP payload.</summary>
    /// <param name="receivedData">The received bytes.</param>
    /// <param name="command">The TCP command.</param>
    /// <param name="tcpMessageDataLength">The TCP message data length.</param>
    private void ValidateTcpPayload(List<byte> receivedData, EnTCPCommandCode command, int tcpMessageDataLength)
    {
        var error = receivedData.Count switch
        {
            0 => "Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - No Data was Received after TCP Header",
            var count when count < tcpMessageDataLength => "Failed to Receive FINS Message within the Timeout Period from Omron PLC '" + RemoteHost + ":" + Port + "'",
            _ when command == EnTCPCommandCode.FINSFrame && receivedData[0] != 0xC0 && receivedData[0] != 0xC1 => "Failed to Receive FINS Message from Omron PLC '" + RemoteHost + ":" + Port + "' - The FINS Header was Invalid",
            _ => null,
        };

        _ = error is null ? 0 : throw new OmronPLCException(error);
    }

    /// <summary>Tracks received TCP data and counters.</summary>
    private sealed class TcpReceiveState
    {
        /// <summary>Gets the received bytes.</summary>
        public List<byte> Data { get; } = [];

        /// <summary>Gets or sets the total received byte count.</summary>
        public int Bytes { get; set; }

        /// <summary>Gets or sets the packet count.</summary>
        public int Packets { get; set; }
    }
}
