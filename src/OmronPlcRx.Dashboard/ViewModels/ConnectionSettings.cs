// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license.
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

    public byte LocalNodeId { get => _localNodeId; set => this.RaiseAndSetIfChanged(ref _localNodeId, value); }
    public byte RemoteNodeId { get => _remoteNodeId; set => this.RaiseAndSetIfChanged(ref _remoteNodeId, value); }
    public ConnectionMethod Method { get => _method; set => this.RaiseAndSetIfChanged(ref _method, value); }
    public string Host { get => _host; set => this.RaiseAndSetIfChanged(ref _host, value); }
    public int Port { get => _port; set => this.RaiseAndSetIfChanged(ref _port, value); }
    public int Timeout { get => _timeout; set => this.RaiseAndSetIfChanged(ref _timeout, value); }
    public int Retries { get => _retries; set => this.RaiseAndSetIfChanged(ref _retries, value); }
    public int PollMs { get => _pollMs; set => this.RaiseAndSetIfChanged(ref _pollMs, value); }
}
