// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CP.IO.Ports;
using OmronPlcRx.Core.Results;

namespace OmronPlcRx.Core.Channels;

internal sealed class SerialHostLinkFinsChannel : BaseChannel
{
    private readonly OmronSerialOptions _options;
    private readonly ConcurrentQueue<byte> _receivedBytes = new();
    private readonly SemaphoreSlim _receivedSignal = new(0, int.MaxValue);
    private SerialPortRx? _port;
    private HostLinkFinsFrameCodec? _codec;
    private IDisposable? _receivedSubscription;

    internal SerialHostLinkFinsChannel(OmronSerialOptions options)
        : base(options?.PortName ?? throw new ArgumentNullException(nameof(options)), 0)
    {
        _options = options;
        _options.Validate();
    }

    public override void Dispose()
    {
        try
        {
            _receivedSubscription?.Dispose();
            _port?.Close();
            _port?.Dispose();
            _receivedSignal.Dispose();
        }
        catch
        {
        }
        finally
        {
            _receivedSubscription = null;
            _port = null;
        }

        base.Dispose();
    }

    internal override async Task InitializeAsync(int timeout, CancellationToken cancellationToken)
    {
        if (!Semaphore.Wait(0, cancellationToken))
        {
            await Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            DestroyClient();
            await InitializeClient(timeout, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    protected override async Task DestroyAndInitializeClient(int timeout, CancellationToken cancellationToken)
    {
        DestroyClient();
        await InitializeClient(timeout, cancellationToken).ConfigureAwait(false);
    }

    protected override Task<SendMessageResult> SendMessageAsync(ReadOnlyMemory<byte> message, int timeout, CancellationToken cancellationToken)
    {
        if (_port == null || _codec == null)
        {
            throw new OmronPLCException($"The serial Host Link FINS channel for '{RemoteHost}' is not initialized.");
        }

        ClearReceiveQueue();
        _port.DiscardInBuffer();
        var frame = _codec.EncodeRequest(message);
        var bytes = Encoding.ASCII.GetBytes(frame);
        _port.Write(bytes, 0, bytes.Length);
        return Task.FromResult(new SendMessageResult
        {
            Bytes = bytes.Length,
            Packets = 1,
        });
    }

    protected override async Task<ReceiveMessageResult> ReceiveMessageAsync(int timeout, CancellationToken cancellationToken)
    {
        if (_codec == null)
        {
            throw new OmronPLCException($"The serial Host Link FINS channel for '{RemoteHost}' is not initialized.");
        }

        var received = new List<byte>();
        var startTimestamp = DateTime.UtcNow;
        while (DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds < timeout)
        {
            while (_receivedBytes.TryDequeue(out var value))
            {
                received.Add(value);
                if (received.Count >= 2 && received[received.Count - 2] == (byte)'*' && received[received.Count - 1] == 0x0D)
                {
                    var frame = Encoding.ASCII.GetString(received.ToArray());
                    return new ReceiveMessageResult
                    {
                        Bytes = received.Count,
                        Packets = 1,
                        Message = _codec.DecodeResponse(frame),
                    };
                }
            }

            var remaining = timeout - (int)DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds;
            if (remaining <= 0)
            {
                break;
            }

            if (!await _receivedSignal.WaitAsync(Math.Min(remaining, 50), cancellationToken).ConfigureAwait(false))
            {
                await Task.Yield();
            }
        }

        if (received.Count == 0)
        {
            throw new OmronPLCException($"Failed to Receive Host Link FINS Message from Omron PLC serial port '{RemoteHost}' - No Data was Received");
        }

        throw new OmronPLCException($"Failed to Receive Host Link FINS Message within the Timeout Period from Omron PLC serial port '{RemoteHost}'");
    }

    protected override Task PurgeReceiveBuffer(int timeout, CancellationToken cancellationToken)
    {
        ClearReceiveQueue();
        _port?.DiscardInBuffer();
        return Task.CompletedTask;
    }

    private async Task InitializeClient(int timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _codec = new HostLinkFinsFrameCodec(_options);
        _port = new SerialPortRx(_options.PortName, _options.BaudRate, _options.DataBits, _options.Parity, _options.StopBits, _options.Handshake)
        {
            EnableAutoDataReceive = true,
            ReadTimeout = timeout,
            WriteTimeout = timeout,
            ReceivedBytesThreshold = 1,
            NewLine = "\r",
        };

        _receivedSubscription = _port.DataReceivedBytes.Subscribe(value =>
        {
            _receivedBytes.Enqueue(value);
            _receivedSignal.Release();
        });

        await _port.Open().ConfigureAwait(false);
    }

    private void DestroyClient()
    {
        try
        {
            _receivedSubscription?.Dispose();
            _port?.Close();
            _port?.Dispose();
        }
        finally
        {
            _receivedSubscription = null;
            _port = null;
            ClearReceiveQueue();
        }
    }

    private void ClearReceiveQueue()
    {
        while (_receivedBytes.TryDequeue(out _))
        {
        }
    }
}
