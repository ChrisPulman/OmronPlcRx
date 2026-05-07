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
    private HostLinkFinsFrameCodec? _hostLinkCodec;
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
        if (_port == null)
        {
            throw new OmronPLCException($"The serial channel for '{RemoteHost}' is not initialized.");
        }

        ClearReceiveQueue();
        _port.DiscardInBuffer();
        var bytes = _options.Protocol == OmronSerialProtocol.Toolbus
            ? ToolbusFinsFrameCodec.EncodeRequest(message).ToArray()
            : Encoding.ASCII.GetBytes(GetHostLinkCodec().EncodeRequest(message));

        if (bytes.Length > _options.MaximumFrameLength)
        {
            throw new OmronPLCException($"The serial FINS request length {bytes.Length} exceeds the configured maximum frame length {_options.MaximumFrameLength}.");
        }

        _port.Write(bytes, 0, bytes.Length);
        return Task.FromResult(new SendMessageResult
        {
            Bytes = bytes.Length,
            Packets = 1,
        });
    }

    protected override Task<ReceiveMessageResult> ReceiveMessageAsync(int timeout, CancellationToken cancellationToken) => _options.Protocol == OmronSerialProtocol.Toolbus
        ? ReceiveToolbusMessageAsync(timeout, cancellationToken)
        : ReceiveHostLinkMessageAsync(timeout, cancellationToken);

    protected override Task PurgeReceiveBuffer(int timeout, CancellationToken cancellationToken)
    {
        ClearReceiveQueue();
        _port?.DiscardInBuffer();
        return Task.CompletedTask;
    }

    private async Task<ReceiveMessageResult> ReceiveHostLinkMessageAsync(int timeout, CancellationToken cancellationToken)
    {
        var received = new List<byte>();
        var startTimestamp = DateTime.UtcNow;
        while (DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds < timeout)
        {
            while (_receivedBytes.TryDequeue(out var value))
            {
                received.Add(value);
                if (received.Count > _options.MaximumFrameLength)
                {
                    throw new OmronPLCException($"The serial Host Link FINS response exceeded the configured maximum frame length {_options.MaximumFrameLength}.");
                }

                if (received.Count >= 2 && received[received.Count - 2] == (byte)'*' && received[received.Count - 1] == 0x0D)
                {
                    var frame = Encoding.ASCII.GetString(received.ToArray());
                    return new ReceiveMessageResult
                    {
                        Bytes = received.Count,
                        Packets = 1,
                        Message = GetHostLinkCodec().DecodeResponse(frame),
                    };
                }
            }

            if (!await WaitForSerialDataAsync(startTimestamp, timeout, cancellationToken).ConfigureAwait(false))
            {
                break;
            }
        }

        if (received.Count == 0)
        {
            throw new OmronPLCException($"Failed to Receive Host Link FINS Message from Omron PLC serial port '{RemoteHost}' - No Data was Received");
        }

        throw new OmronPLCException($"Failed to Receive Host Link FINS Message within the Timeout Period from Omron PLC serial port '{RemoteHost}'");
    }

    private async Task<ReceiveMessageResult> ReceiveToolbusMessageAsync(int timeout, CancellationToken cancellationToken)
    {
        var received = new List<byte>();
        var startTimestamp = DateTime.UtcNow;
        while (DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds < timeout)
        {
            while (_receivedBytes.TryDequeue(out var value))
            {
                received.Add(value);

                var frameStart = received.IndexOf(0xAB);
                if (frameStart < 0)
                {
                    received.Clear();
                    continue;
                }

                if (frameStart > 0)
                {
                    received.RemoveRange(0, frameStart);
                }

                if (received.Count > _options.MaximumFrameLength)
                {
                    throw new OmronPLCException($"The serial Toolbus FINS response exceeded the configured maximum frame length {_options.MaximumFrameLength}.");
                }

                if (received.Count < 3)
                {
                    continue;
                }

                var declaredLength = (received[1] << 8) | received[2];
                var totalLength = declaredLength + 3;
                if (declaredLength < 2 || totalLength > _options.MaximumFrameLength)
                {
                    throw new OmronPLCException($"The serial Toolbus FINS response declared invalid frame length {declaredLength}.");
                }

                if (received.Count >= totalLength)
                {
                    var frame = received.GetRange(0, totalLength).ToArray();
                    return new ReceiveMessageResult
                    {
                        Bytes = totalLength,
                        Packets = 1,
                        Message = ToolbusFinsFrameCodec.DecodeResponse(frame),
                    };
                }
            }

            if (!await WaitForSerialDataAsync(startTimestamp, timeout, cancellationToken).ConfigureAwait(false))
            {
                break;
            }
        }

        if (received.Count == 0)
        {
            throw new OmronPLCException($"Failed to Receive Toolbus FINS Message from Omron PLC serial port '{RemoteHost}' - No Data was Received");
        }

        throw new OmronPLCException($"Failed to Receive Toolbus FINS Message within the Timeout Period from Omron PLC serial port '{RemoteHost}'");
    }

    private async Task InitializeClient(int timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _hostLinkCodec = _options.Protocol == OmronSerialProtocol.HostLinkFins ? new HostLinkFinsFrameCodec(_options) : null;
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
        if (_options.Protocol == OmronSerialProtocol.Toolbus)
        {
            await SynchronizeToolbusAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SynchronizeToolbusAsync(int timeout, CancellationToken cancellationToken)
    {
        if (_port == null)
        {
            throw new OmronPLCException($"The Toolbus serial channel for '{RemoteHost}' is not initialized.");
        }

        ClearReceiveQueue();
        _port.DiscardInBuffer();
        var sync = ToolbusFinsFrameCodec.SynchronizationFrame.ToArray();
        var received = new List<byte>();
        var startTimestamp = DateTime.UtcNow;
        var nextSync = DateTime.MinValue;
        while (DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds < timeout)
        {
            if (DateTime.UtcNow >= nextSync)
            {
                _port.Write(sync, 0, sync.Length);
                nextSync = DateTime.UtcNow.AddMilliseconds(250);
            }

            while (_receivedBytes.TryDequeue(out var value))
            {
                received.Add(value);
                if (received.Count > _options.MaximumFrameLength)
                {
                    received.RemoveAt(0);
                }

                for (var i = 0; i <= received.Count - sync.Length; i++)
                {
                    if (received[i] == sync[0] && received[i + 1] == sync[1])
                    {
                        ClearReceiveQueue();
                        return;
                    }
                }
            }

            await WaitForSerialDataAsync(startTimestamp, timeout, cancellationToken).ConfigureAwait(false);
        }

        throw new OmronPLCException($"Failed to synchronize Toolbus serial port '{RemoteHost}' within the timeout period.");
    }

    private async Task<bool> WaitForSerialDataAsync(DateTime startTimestamp, int timeout, CancellationToken cancellationToken)
    {
        var remaining = timeout - (int)DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds;
        if (remaining <= 0)
        {
            return false;
        }

        if (!await _receivedSignal.WaitAsync(Math.Min(remaining, 50), cancellationToken).ConfigureAwait(false))
        {
            await Task.Yield();
        }

        return true;
    }

    private HostLinkFinsFrameCodec GetHostLinkCodec() => _hostLinkCodec ?? throw new OmronPLCException($"The serial Host Link FINS codec for '{RemoteHost}' is not initialized.");

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

        while (_receivedSignal.Wait(0))
        {
        }
    }
}
