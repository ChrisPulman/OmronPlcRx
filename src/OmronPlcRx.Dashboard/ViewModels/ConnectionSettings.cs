// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license.
using System.IO.Ports;
using OmronPlcRx;
using OmronPlcRx.Enums;
using ReactiveUI;

namespace OmronPlcRxDashboard.ViewModels;

public sealed class ConnectionSettings : ReactiveObject
{
    private byte _localNodeId = 11;
    private byte _remoteNodeId = 1;
    private ConnectionMethod _method = ConnectionMethod.TCP;
    private string _host = "192.168.2.220";
    private int _port = 9600;
    private int _timeout = 2000;
    private int _retries = 1;
    private int _pollMs = 200;
    private string _serialPortName = "COM3";
    private OmronSerialProtocol _serialProtocol = OmronSerialProtocol.HostLinkFins;
    private int _baudRate = 9600;
    private int _dataBits = 7;
    private Parity _parity = Parity.Even;
    private StopBits _stopBits = StopBits.Two;
    private Handshake _handshake = Handshake.None;
    private byte _hostLinkUnitNumber;
    private byte _responseWaitTime;
    private OmronHostLinkFinsFrameMode _frameMode = OmronHostLinkFinsFrameMode.Direct;
    private int _maximumFrameLength = 1004;

    public byte LocalNodeId { get => _localNodeId; set => this.RaiseAndSetIfChanged(ref _localNodeId, value); }
    public byte RemoteNodeId { get => _remoteNodeId; set => this.RaiseAndSetIfChanged(ref _remoteNodeId, value); }

    public ConnectionMethod Method
    {
        get => _method;
        set
        {
            var previous = _method;
            this.RaiseAndSetIfChanged(ref _method, value);
            if (previous == value)
            {
                return;
            }

            if (value == ConnectionMethod.Serial && _remoteNodeId == 1)
            {
                RemoteNodeId = 0;
            }
            else if (value != ConnectionMethod.Serial && _remoteNodeId == 0)
            {
                RemoteNodeId = 1;
            }

            this.RaisePropertyChanged(nameof(IsSerial));
        }
    }

    public string Host { get => _host; set => this.RaiseAndSetIfChanged(ref _host, value); }
    public int Port { get => _port; set => this.RaiseAndSetIfChanged(ref _port, value); }
    public int Timeout { get => _timeout; set => this.RaiseAndSetIfChanged(ref _timeout, value); }
    public int Retries { get => _retries; set => this.RaiseAndSetIfChanged(ref _retries, value); }
    public int PollMs { get => _pollMs; set => this.RaiseAndSetIfChanged(ref _pollMs, value); }
    public string SerialPortName { get => _serialPortName; set => this.RaiseAndSetIfChanged(ref _serialPortName, value); }

    public OmronSerialProtocol SerialProtocol
    {
        get => _serialProtocol;
        set
        {
            var previous = _serialProtocol;
            this.RaiseAndSetIfChanged(ref _serialProtocol, value);
            if (previous == value)
            {
                return;
            }

            if (value == OmronSerialProtocol.Toolbus)
            {
                BaudRate = 115200;
                DataBits = 8;
                Parity = Parity.None;
                StopBits = StopBits.One;
                Handshake = Handshake.None;
                RemoteNodeId = 0;
                MaximumFrameLength = 1004;
            }
        }
    }

    public int BaudRate { get => _baudRate; set => this.RaiseAndSetIfChanged(ref _baudRate, value); }
    public int DataBits { get => _dataBits; set => this.RaiseAndSetIfChanged(ref _dataBits, value); }
    public Parity Parity { get => _parity; set => this.RaiseAndSetIfChanged(ref _parity, value); }
    public StopBits StopBits { get => _stopBits; set => this.RaiseAndSetIfChanged(ref _stopBits, value); }
    public Handshake Handshake { get => _handshake; set => this.RaiseAndSetIfChanged(ref _handshake, value); }
    public byte HostLinkUnitNumber { get => _hostLinkUnitNumber; set => this.RaiseAndSetIfChanged(ref _hostLinkUnitNumber, value); }
    public byte ResponseWaitTime { get => _responseWaitTime; set => this.RaiseAndSetIfChanged(ref _responseWaitTime, value); }
    public OmronHostLinkFinsFrameMode FrameMode { get => _frameMode; set => this.RaiseAndSetIfChanged(ref _frameMode, value); }
    public int MaximumFrameLength { get => _maximumFrameLength; set => this.RaiseAndSetIfChanged(ref _maximumFrameLength, value); }
    public bool IsSerial => Method == ConnectionMethod.Serial;

    public OmronSerialOptions ToSerialOptions() => new(SerialPortName)
    {
        Protocol = SerialProtocol,
        BaudRate = BaudRate,
        DataBits = DataBits,
        Parity = Parity,
        StopBits = StopBits,
        Handshake = Handshake,
        HostLinkUnitNumber = HostLinkUnitNumber,
        ResponseWaitTime = ResponseWaitTime,
        FrameMode = FrameMode,
        MaximumFrameLength = MaximumFrameLength,
    };
}
