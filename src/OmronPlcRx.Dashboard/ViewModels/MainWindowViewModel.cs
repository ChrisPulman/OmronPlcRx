// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Reflection;
using OmronPlcRx.Enums;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using PlcLib = global::OmronPlcRx;

namespace OmronPlcRxDashboard.ViewModels;

/// <summary>Main window view model coordinating connection and tags.</summary>
public sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
    private readonly ObservableCollection<TagDefinition> _tags = [];
    private readonly MultipleDisposable _disposables = new();
    private PlcLib.IOmronPlcRx? _plc;
    private bool _isConnected;
    private string _status = "Idle";
    private PLCType? _plcType;
    private string? _controllerModel;
    private string? _controllerVersion;

    /// <summary>Initializes a new instance of the <see cref="MainWindowViewModel"/> class.</summary>
    public MainWindowViewModel()
    {
        Tags = new(_tags);
        ConnectionMethods = Enum.GetValues<ConnectionMethod>();
        SerialProtocols = Enum.GetValues<PlcLib.OmronSerialProtocol>();
        SerialParities = Enum.GetValues<Parity>();
        SerialStopBits = Enum.GetValues<StopBits>();
        SerialHandshakes = Enum.GetValues<Handshake>();
        SerialFrameModes = Enum.GetValues<PlcLib.OmronHostLinkFinsFrameMode>();

        var canConnect = this.WhenAnyValue(v => v.IsConnected).Select(static isConnected => !isConnected);
        var canDisconnect = this.WhenAnyValue(v => v.IsConnected);
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync, canConnect);
        DisconnectCommand = ReactiveCommand.Create(Disconnect, canDisconnect);
        AddTagCommand = ReactiveCommand.CreateFromTask(AddTagAsync, this.WhenAnyValue(v => v.IsConnected));
        WriteTagCommand = ReactiveCommand.CreateFromTask<TagDefinition>(WriteTagAsync, this.WhenAnyValue(v => v.IsConnected));
    }

    /// <summary>Gets enum values for binding connection method.</summary>
    public Array ConnectionMethods { get; }

    /// <summary>Gets serial protocol values for binding serial settings.</summary>
    public Array SerialProtocols { get; }

    /// <summary>Gets parity values for binding serial settings.</summary>
    public Array SerialParities { get; }

    /// <summary>Gets stop-bit values for binding serial settings.</summary>
    public Array SerialStopBits { get; }

    /// <summary>Gets handshake values for binding serial settings.</summary>
    public Array SerialHandshakes { get; }

    /// <summary>Gets Host Link FINS frame modes for binding serial settings.</summary>
    public Array SerialFrameModes { get; }

    /// <summary>Gets connection settings.</summary>
    public ConnectionSettings Settings { get; } = new();

    /// <summary>Gets observable tag collection.</summary>
    public ReadOnlyObservableCollection<TagDefinition> Tags { get; }

    /// <summary>Gets a value indicating whether a PLC connection is active.</summary>
    public bool IsConnected
    {
        get => _isConnected;
        private set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    /// <summary>Gets status text.</summary>
    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    /// <summary>Gets the connected PLC type.</summary>
    public PLCType? PLCType
    {
        get => _plcType;
        private set => this.RaiseAndSetIfChanged(ref _plcType, value);
    }

    /// <summary>Gets the connected controller model.</summary>
    public string? ControllerModel
    {
        get => _controllerModel;
        private set => this.RaiseAndSetIfChanged(ref _controllerModel, value);
    }

    /// <summary>Gets the connected controller version.</summary>
    public string? ControllerVersion
    {
        get => _controllerVersion;
        private set => this.RaiseAndSetIfChanged(ref _controllerVersion, value);
    }

    /// <summary>Gets the connect command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ConnectCommand { get; }

    /// <summary>Gets the disconnect command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> DisconnectCommand { get; }

    /// <summary>Gets the add tag command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> AddTagCommand { get; }

    /// <summary>Gets the write tag command.</summary>
    public ReactiveCommand<TagDefinition, RxVoid> WriteTagCommand { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Disconnect();
        _disposables.Dispose();
    }

    private async Task ConnectAsync()
    {
        Disconnect();
        try
        {
            _plc = Settings.Method == ConnectionMethod.Serial
                ? new PlcLib.OmronPlcRx(Settings.LocalNodeId, Settings.RemoteNodeId, Settings.ToSerialOptions(), Settings.Timeout, Settings.Retries, TimeSpan.FromMilliseconds(Settings.PollMs))
                : new PlcLib.OmronPlcRx(Settings.LocalNodeId, Settings.RemoteNodeId, Settings.Method, Settings.Host, Settings.Port, Settings.Timeout, Settings.Retries, TimeSpan.FromMilliseconds(Settings.PollMs));
            _plc.Errors.SubscribeSafe(error => Status = error?.Message ?? string.Empty, error => Status = error.Message).DisposeWith(_disposables);
            IsConnected = true;
            await Task.Delay(1000).ConfigureAwait(true);
            PLCType = _plc.PLCType;
            ControllerModel = _plc.ControllerModel;
            ControllerVersion = _plc.ControllerVersion;
            Status = Settings.Method == ConnectionMethod.Serial
                ? $"Connected via serial {Settings.SerialPortName} ({Settings.SerialProtocol})"
                : "Connected";
        }
        catch (Exception ex)
        {
            Status = "Connect failed: " + ex.Message;
        }
    }

    private void Disconnect()
    {
        _plc?.Dispose();
        _plc = null;
        IsConnected = false;
        PLCType = null;
        ControllerModel = null;
        ControllerVersion = null;
        _tags.Clear();
        Status = "Disconnected";
    }

    private async Task AddTagAsync()
    {
        if (_plc is null)
        {
            return;
        }

        var allowed = new[] { typeof(bool), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(float), typeof(double), typeof(string) };
        var vm = new AddTagViewModel(allowed);
        var dlg = new Views.AddTagDialog { DataContext = vm };
        if (dlg.ShowDialog() == true)
        {
            var tagDef = new TagDefinition(vm.Name.Trim(), vm.Address.Trim(), vm.SelectedType);
            _tags.Add(tagDef);
            RegisterTag(tagDef);
        }

        await Task.CompletedTask.ConfigureAwait(true);
    }

    private void RegisterTag(TagDefinition tag)
    {
        if (_plc is null)
        {
            return;
        }

        try
        {
            var valueType = tag.ValueType;
            _plc.GetType().GetMethod("AddUpdateTagItem")?.MakeGenericMethod(valueType)
                .Invoke(_plc, [tag.Address, tag.Name]);

            var observeMethod = _plc.GetType().GetMethod("Observe")?.MakeGenericMethod(valueType);
            var observable = observeMethod?.Invoke(_plc, [tag.Name]);
            if (observable is not null)
            {
                SubscribeGeneric(tag, valueType, observable);
            }
        }
        catch (Exception ex)
        {
            Status = "Add tag failed: " + ex.Message;
        }
    }

    private void SubscribeGeneric(TagDefinition tag, Type valueType, object observable)
    {
        var helper = typeof(MainWindowViewModel).GetMethod(nameof(SubscribeCore), BindingFlags.NonPublic | BindingFlags.Instance);
        var genericHelper = helper?.MakeGenericMethod(valueType);
        genericHelper?.Invoke(this, [tag, observable]);
    }

    private void SubscribeCore<T>(TagDefinition tag, object observable)
    {
        if (observable is IObservable<T?> source)
        {
            source.SubscribeSafe(value => tag.Value = value is null ? null : (object)value, error => Status = error.Message).DisposeWith(_disposables);
        }
    }

    private Task WriteTagAsync(TagDefinition? tag)
    {
        if (tag is null || _plc is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            var valueType = tag.ValueType;
            var method = _plc.GetType().GetMethod("Value", [typeof(string), valueType]);
            if (method is not null)
            {
                var converted = TypeDescriptor.GetConverter(valueType).ConvertFromString(tag.Value?.ToString() ?? string.Empty);
                _ = method.Invoke(_plc, [tag.Name, converted]);
            }
        }
        catch (Exception ex)
        {
            Status = "Write failed: " + ex.Message;
        }

        return Task.CompletedTask;
    }
}
