// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using OmronPlcRx.Core.Requests;
using OmronPlcRx.Core.Responses;
using OmronPlcRx.Core.Results;

namespace OmronPlcRx.Core.Channels;

/// <summary>Represents the b as ec ha nn el type.</summary>
internal abstract class BaseChannel : IDisposable
{
    /// <summary>Stores the r eq ue st id value.</summary>
    private byte _requestId;

    /// <summary>Initializes a new instance of the <see cref="BaseChannel"/> class.</summary>
    /// <param name="remoteHost">The r em ot eh os t value.</param>
    /// <param name="port">The p or t value.</param>
    internal BaseChannel(string remoteHost, int port)
    {
        RemoteHost = remoteHost;
        Port = port;
        Semaphore = new(1, 1);
    }

    /// <summary>Gets the remote host value.</summary>
    internal string RemoteHost { get; }

    /// <summary>Gets the port value.</summary>
    internal int Port { get; }

    /// <summary>Gets the semaphore value.</summary>
    protected SemaphoreSlim Semaphore { get; }

    /// <summary>Initializes a new instance of the <see cref="Dispose"/> class.</summary>
    public virtual void Dispose() => Semaphore?.Dispose();

    /// <summary>Initializes a new instance of the <see cref="InitializeAsync"/> class.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal abstract Task InitializeAsync(int timeout, CancellationToken cancellationToken);

    /// <summary>Initializes a new instance of the <see cref="ProcessRequestAsync"/> class.</summary>
    /// <param name="request">The r eq ue st value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="retries">The r et ri es value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal async Task<ProcessRequestResult> ProcessRequestAsync(FINSRequest request, int timeout, int retries, CancellationToken cancellationToken)
    {
        var attempts = 0;
        var responseMessage = default(Memory<byte>);
        var bytesSent = 0;
        var packetsSent = 0;
        var bytesReceived = 0;
        var packetsReceived = 0;
        var startTimestamp = DateTime.UtcNow;

        while (attempts <= retries)
        {
            if (!Semaphore.Wait(0, cancellationToken))
            {
                await Semaphore.WaitAsync(cancellationToken);
            }

            try
            {
                if (attempts > 0)
                {
                    await DestroyAndInitializeClient(timeout, cancellationToken);
                }

                // Build the Request into a Message we can Send
                var requestMessage = request.BuildMessage(GetNextRequestId());

                // Send the Message
                var sendResult = await SendMessageAsync(requestMessage, timeout, cancellationToken);

                bytesSent += sendResult.Bytes;
                packetsSent += sendResult.Packets;

                // Receive a Response
                var receiveResult = await ReceiveMessageAsync(timeout, cancellationToken);

                bytesReceived += receiveResult.Bytes;
                packetsReceived += receiveResult.Packets;
                responseMessage = receiveResult.Message;

                break;
            }
            catch (Exception) when (attempts < retries)
            {
            }
            finally
            {
                _ = Semaphore.Release();
            }

            // Increment the Attempts
            attempts++;
        }

        try
        {
            return new ProcessRequestResult
            {
                BytesSent = bytesSent,
                PacketsSent = packetsSent,
                BytesReceived = bytesReceived,
                PacketsReceived = packetsReceived,
                Duration = DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds,
                Response = FINSResponse.CreateNew(responseMessage, request),
            };
        }
        catch (FINSException e)
        {
            if (e.Message.Contains("Service ID") && responseMessage.Length >= 9 && responseMessage.Span[9] != request.ServiceID)
            {
                if (!Semaphore.Wait(0, cancellationToken))
                {
                    await Semaphore.WaitAsync(cancellationToken);
                }

                try
                {
                    await PurgeReceiveBuffer(timeout, cancellationToken);
                }
                catch (TimeoutException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (OmronPLCException)
                {
                }
                finally
                {
                    _ = Semaphore.Release();
                }
            }

            throw new OmronPLCException("Received a FINS Error Response from Omron PLC '" + RemoteHost + ":" + Port + "'", e);
        }
    }

    /// <summary>Initializes a new instance of the <see cref="DestroyAndInitializeClient"/> class.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task DestroyAndInitializeClient(int timeout, CancellationToken cancellationToken);

    /// <summary>Initializes a new instance of the <see cref="SendMessageAsync"/> class.</summary>
    /// <param name="message">The m es sa ge value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task<SendMessageResult> SendMessageAsync(ReadOnlyMemory<byte> message, int timeout, CancellationToken cancellationToken);

    /// <summary>Initializes a new instance of the <see cref="ReceiveMessageAsync"/> class.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task<ReceiveMessageResult> ReceiveMessageAsync(int timeout, CancellationToken cancellationToken);

    /// <summary>Initializes a new instance of the <see cref="PurgeReceiveBuffer"/> class.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task PurgeReceiveBuffer(int timeout, CancellationToken cancellationToken);

    /// <summary>Initializes a new instance of the <see cref="GetNextRequestId"/> class.</summary>
    /// <returns>The result produced by the operation.</returns>
    private byte GetNextRequestId()
    {
        if (_requestId == byte.MaxValue)
        {
            _requestId = byte.MinValue;
        }
        else
        {
            _requestId++;
        }

        return _requestId;
    }
}
