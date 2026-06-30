// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using ReactiveUI;

namespace OmronPlcRxDashboard.ViewModels;

/// <summary>Represents a PLC tag definition and its current value.</summary>
/// <remarks>Initializes a new instance of the <see cref="TagDefinition"/> class.</remarks>
public sealed class TagDefinition(string name, string address, Type valueType) : ReactiveObject
{

    /// <summary>Gets the logical tag name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the PLC address (e.g. D100, D100.0, D200[20]).</summary>
    public string Address { get; } = address;

    /// <summary>Gets the underlying value type.</summary>
    public Type ValueType { get; } = valueType;
    /// <summary>Gets or sets the current value.</summary>
    public object? Value
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}
