// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license.
using System;
using ReactiveUI;

namespace OmronPlcRxDashboard.ViewModels;

/// <summary>
/// Represents a PLC tag definition and its current value.
/// </summary>
/// <remarks>Initializes a new instance of the <see cref="TagDefinition"/> class.</remarks>
public sealed class TagDefinition(string name, string address, Type valueType) : ReactiveObject
{
    private object? _value;

    /// <summary>Gets the logical tag name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the PLC address (e.g. D100, D100.0, D200[20]).</summary>
    public string Address { get; } = address;

    /// <summary>Gets the underlying value type.</summary>
    public Type ValueType { get; } = valueType;

    /// <summary>Gets or sets the current value.</summary>
    public object? Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }
}
