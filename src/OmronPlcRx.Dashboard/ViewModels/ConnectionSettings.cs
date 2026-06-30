// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO.Ports;
using OmronPlcRx;
using OmronPlcRx.Enums;
using ReactiveUI;

namespace OmronPlcRxDashboard.ViewModels;

public sealed class ConnectionSettings : ReactiveObject
{

    private byte _remoteNodeId = 1;

    private string _host = "192.168.2.220";

    private int _timeout = 2000;

    private int _pollMs = 200;

    private OmronSerialProtocol _serialProtocol = OmronSerialProtocol.HostLinkFins;

    private int _dataBits = 7;

    private StopBits _stopBits = StopBits.Two;

    private bool _rtsEnable;

    private int _maximumFrameLength = 1004;
    public byte LocalNodeId { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
= 11;
    public byte RemoteNodeId { get => _remoteNodeId; set => this.RaiseAndSetIfChanged(ref _remoteNodeId, value); }
    public ConnectionMethod Method
    {
        get => field;
        set
        {
            var previous = field;
            _ = this.RaiseAndSetIfChanged(ref field, value);
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
= ConnectionMethod.TCP;

    public string Host { get => _host; set => this.RaiseAndSetIfChanged(ref _host, value); }
    public int Port { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
= 9600;
    public int Timeout { get => _timeout; set => this.RaiseAndSetIfChanged(ref _timeout, value); }
    public int Retries { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
= 1;
    public int PollMs { get => _pollMs; set => this.RaiseAndSetIfChanged(ref _pollMs, value); }
    public string SerialPortName { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
= "COM3";

    public OmronSerialProtocol SerialProtocol
    {
        get => _serialProtocol;
        set
        {
            var previous = _serialProtocol;
            _ = this.RaiseAndSetIfChanged(ref _serialProtocol, value);
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
                RtsEnable = true;
                DtrEnable = false;
                RemoteNodeId = 0;
                MaximumFrameLength = 1004;
            }

            this.RaisePropertyChanged(nameof(IsHostLinkFins));
        }
    }
    public int BaudRate { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
= 9600;
    public int DataBits { get => _dataBits; set => this.RaiseAndSetIfChanged(ref _dataBits, value); }
    public Parity Parity { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
= Parity.Even;
    public StopBits StopBits { get => _stopBits; set => this.RaiseAndSetIfChanged(ref _stopBits, value); }
    public Handshake Handshake { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
= Handshake.None;
    public bool RtsEnable { get => _rtsEnable; set => this.RaiseAndSetIfChanged(ref _rtsEnable, value); }
    public bool DtrEnable { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public byte HostLinkUnitNumber { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public byte ResponseWaitTime { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public OmronHostLinkFinsFrameMode FrameMode { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
= OmronHostLinkFinsFrameMode.Direct;
    public int MaximumFrameLength { get => _maximumFrameLength; set => this.RaiseAndSetIfChanged(ref _maximumFrameLength, value); }

    public bool IsSerial => Method == ConnectionMethod.Serial;

    public bool IsHostLinkFins => SerialProtocol == OmronSerialProtocol.HostLinkFins;

    public OmronSerialOptions ToSerialOptions() => new(SerialPortName)
    {
        Protocol = SerialProtocol,
        BaudRate = BaudRate,
        DataBits = DataBits,
        Parity = Parity,
        StopBits = StopBits,
        Handshake = Handshake,
        RtsEnable = RtsEnable,
        DtrEnable = DtrEnable,
        HostLinkUnitNumber = HostLinkUnitNumber,
        ResponseWaitTime = ResponseWaitTime,
        FrameMode = FrameMode,
        MaximumFrameLength = MaximumFrameLength,
    };
}
