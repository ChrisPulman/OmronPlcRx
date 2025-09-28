// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using OmronPlcRx.Core.Requests;
using OmronPlcRx.Core.Responses;
using OmronPlcRx.Core.Results;

namespace OmronPlcRx.Core.Channels;

internal abstract class BaseChannel : IDisposable
{
    private byte _requestId;

    internal BaseChannel(string remoteHost, int port)
    {
        RemoteHost = remoteHost;
        Port = port;
        Semaphore = new SemaphoreSlim(1, 1);
    }

    internal string RemoteHost { get; }

    internal int Port { get; }

    protected SemaphoreSlim Semaphore { get; }

    public virtual void Dispose() => Semaphore?.Dispose();

    internal abstract Task InitializeAsync(int timeout, CancellationToken cancellationToken);

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
            catch (Exception)
            {
                if (attempts >= retries)
                {
                    throw;
                }
            }
            finally
            {
                Semaphore.Release();
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
                catch
                {
                }
                finally
                {
                    Semaphore.Release();
                }
            }

            throw new OmronPLCException("Received a FINS Error Response from Omron PLC '" + RemoteHost + ":" + Port + "'", e);
        }
    }

    protected abstract Task DestroyAndInitializeClient(int timeout, CancellationToken cancellationToken);

    protected abstract Task<SendMessageResult> SendMessageAsync(ReadOnlyMemory<byte> message, int timeout, CancellationToken cancellationToken);

    protected abstract Task<ReceiveMessageResult> ReceiveMessageAsync(int timeout, CancellationToken cancellationToken);

    protected abstract Task PurgeReceiveBuffer(int timeout, CancellationToken cancellationToken);

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
