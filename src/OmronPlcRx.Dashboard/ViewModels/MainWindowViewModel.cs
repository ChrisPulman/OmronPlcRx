// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license.
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using OmronPlcRx.Enums;
using PlcLib = global::OmronPlcRx;
using OmronPlcRxDashboard.ViewModels;

namespace OmronPlcRxDashboard.ViewModels;

/// <summary>
/// Main window view model coordinating connection and tags.
/// </summary>
public sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
    private PlcLib.IOmronPlcRx? _plc;
    private readonly SourceList<TagDefinition> _tags = new();
    private readonly CompositeDisposable _disposables = new();

    public MainWindowViewModel()
    {
        _tags.Connect().Bind(out var ro).Subscribe().DisposeWith(_disposables);
        Tags = ro;
        ConnectionMethods = Enum.GetValues(typeof(ConnectionMethod));
        var canConnect = this.WhenAnyValue(v => v.IsConnected).Select(c => !c);
        var canDisconnect = this.WhenAnyValue(v => v.IsConnected);
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync, canConnect);
        DisconnectCommand = ReactiveCommand.Create(Disconnect, canDisconnect);
        AddTagCommand = ReactiveCommand.CreateFromTask(AddTagAsync, this.WhenAnyValue(v => v.IsConnected));
        WriteTagCommand = ReactiveCommand.CreateFromTask<TagDefinition>(WriteTagAsync, this.WhenAnyValue(v => v.IsConnected));
    }

    /// <summary>Gets enum values for binding connection method.</summary>
    public Array ConnectionMethods { get; }

    /// <summary>Gets connection settings.</summary>
    public ConnectionSettings Settings { get; } = new();

    /// <summary>Gets observable tag collection.</summary>
    public ReadOnlyObservableCollection<TagDefinition> Tags { get; }

    private bool _isConnected;
    /// <summary>Gets a value indicating whether a PLC connection is active.</summary>
    public bool IsConnected { get => _isConnected; private set => this.RaiseAndSetIfChanged(ref _isConnected, value); }

    private string _status = "Idle";
    /// <summary>Gets status text.</summary>
    public string Status { get => _status; private set => this.RaiseAndSetIfChanged(ref _status, value); }

    private PLCType? _plcType;
    public PLCType? PLCType { get => _plcType; private set => this.RaiseAndSetIfChanged(ref _plcType, value); }

    private string? _controllerModel;
    public string? ControllerModel { get => _controllerModel; private set => this.RaiseAndSetIfChanged(ref _controllerModel, value); }

    private string? _controllerVersion;
    public string? ControllerVersion { get => _controllerVersion; private set => this.RaiseAndSetIfChanged(ref _controllerVersion, value); }

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<Unit, Unit> AddTagCommand { get; }
    public ReactiveCommand<TagDefinition, Unit> WriteTagCommand { get; }

    private async Task ConnectAsync()
    {
        Disconnect();
        try
        {
            _plc = new PlcLib.OmronPlcRx(Settings.LocalNodeId, Settings.RemoteNodeId, Settings.Method, Settings.Host, Settings.Port, Settings.Timeout, Settings.Retries, TimeSpan.FromMilliseconds(Settings.PollMs));
            _plc.Errors.Subscribe(e => Status = e?.Message ?? string.Empty).DisposeWith(_disposables);
            IsConnected = true;
            await Task.Delay(1000);
            PLCType = _plc.PLCType;
            ControllerModel = _plc.ControllerModel;
            ControllerVersion = _plc.ControllerVersion;
            Status = "Connected";
        }
        catch (Exception ex)
        {
            Status = "Connect failed: " + ex.Message;
        }
    }

    private void Disconnect()
    {
        if (_plc is not null)
        {
            _plc.Dispose();
            _plc = null;
        }
        IsConnected = false;
        PLCType = null;
        ControllerModel = null;
        ControllerVersion = null;
        _tags.Clear();
        Status = "Disconnected";
    }

    private async Task AddTagAsync()
    {
        if (_plc is null) return;
        var allowed = new[] { typeof(bool), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(float), typeof(double), typeof(string) };
        var vm = new AddTagViewModel(allowed);
        var dlg = new Views.AddTagDialog { DataContext = vm };
        if (dlg.ShowDialog() == true)
        {
            var tagDef = new TagDefinition(vm.Name.Trim(), vm.Address.Trim(), vm.SelectedType);
            _tags.Add(tagDef);
            RegisterTag(tagDef);
        }

        await Task.CompletedTask;
    }

    private void RegisterTag(TagDefinition tag)
    {
        if (_plc is null) return;
        try
        {
            var t = tag.ValueType;
            // Register
            _plc.GetType().GetMethod("AddUpdateTagItem")?.MakeGenericMethod(t)
                .Invoke(_plc, new object[] { tag.Address, tag.Name });

            // Observe
            var observeMethod = _plc.GetType().GetMethod("Observe")?.MakeGenericMethod(t);
            var observableObj = observeMethod?.Invoke(_plc, new object?[] { tag.Name });
            if (observableObj != null)
            {
                SubscribeGeneric(tag, t, observableObj);
            }
        }
        catch (Exception ex)
        {
            Status = "Add tag failed: " + ex.Message;
        }
    }

    private void SubscribeGeneric(TagDefinition tag, Type valueType, object observableObj)
    {
        var helper = typeof(MainWindowViewModel).GetMethod(nameof(SubscribeCore), BindingFlags.NonPublic | BindingFlags.Instance);
        var g = helper?.MakeGenericMethod(valueType);
        g?.Invoke(this, new object?[] { tag, observableObj });
    }

    private void SubscribeCore<T>(TagDefinition tag, object observableObj)
    {
        if (observableObj is IObservable<T?> obs)
        {
            obs.Subscribe(v => tag.Value = v is null ? null : (object)v).DisposeWith(_disposables);
        }
    }

    private Task WriteTagAsync(TagDefinition? tag)
    {
        if (tag is null || _plc is null) return Task.CompletedTask;
        try
        {
            var t = tag.ValueType;
            var method = _plc.GetType().GetMethod("Value", new[] { typeof(string), t });
            if (method != null)
            {
                var converted = TypeDescriptor.GetConverter(t).ConvertFromString(tag.Value?.ToString() ?? string.Empty);
                method.Invoke(_plc, new object?[] { tag.Name, converted });
            }
        }
        catch (Exception ex)
        {
            Status = "Write failed: " + ex.Message;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Disconnect();
        _disposables.Dispose();
        _tags.Dispose();
    }
}
