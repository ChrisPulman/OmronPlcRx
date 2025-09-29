// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Types;

/// <summary>
/// Unsigned 32-bit BCD numeric wrapper.
/// </summary>
public readonly record struct BcdU32
{
    /// <summary>Initializes a new instance of the <see cref="BcdU32"/> struct.</summary>
    /// <param name="value">The unsigned value.</param>
    public BcdU32(uint value) => Value = value;

    /// <summary>Gets the numeric value.</summary>
    public uint Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();
}
