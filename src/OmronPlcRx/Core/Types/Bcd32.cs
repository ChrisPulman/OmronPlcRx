// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Types;

/// <summary>
/// Signed 32-bit BCD numeric wrapper.
/// </summary>
public readonly record struct Bcd32
{
    /// <summary>Initializes a new instance of the <see cref="Bcd32"/> struct.</summary>
    /// <param name="value">The signed value.</param>
    public Bcd32(int value) => Value = value;

    /// <summary>Gets the numeric value.</summary>
    public int Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();
}
