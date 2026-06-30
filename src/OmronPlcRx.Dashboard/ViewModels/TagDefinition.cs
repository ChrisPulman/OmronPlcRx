// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using ReactiveUI;

namespace OmronPlcRxDashboard.ViewModels;

/// <summary>Represents a PLC tag definition and its current value.</summary>
/// <remarks>Initializes a new instance of the <see cref="TagDefinition"/> class.</remarks>
        /// <param name="name">The name value.</param>
        /// <param name="address">The address value.</param>
        /// <param name="valueType">The value type value.</param>
public sealed class TagDefinition(string name, string address, Type valueType) : ReactiveObject
{

    /// <summary>Gets the name value.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the address value.</summary>
    public string Address { get; } = address;

    /// <summary>Gets the value type value.</summary>
    public Type ValueType { get; } = valueType;
    /// <summary>Gets or sets the value value.</summary>
    public object? Value
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}
