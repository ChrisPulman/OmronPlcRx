// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace OmronPlcRx;

/// <summary>Marks a field for PLC reactive stream source generation.</summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class PlcTagAttribute(string address) : Attribute
{
    /// <summary>Gets the PLC address to register for the generated stream.</summary>
    public string Address { get; } = address;

    /// <summary>Gets or sets the PLC tag name. When omitted, the generated property name is used.</summary>
    public string? TagName { get; set; }

    /// <summary>Gets or sets a value indicating whether the generated bind method should register the tag.</summary>
    public bool Register { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the generated bind method should observe the tag.</summary>
    public bool Observe { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether a generated write helper should be emitted.</summary>
    public bool Writable { get; set; }
}
