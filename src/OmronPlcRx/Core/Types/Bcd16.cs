// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Types;

/// <summary>
/// Signed 16-bit BCD numeric wrapper.
/// </summary>
public readonly record struct Bcd16
{
    /// <summary>Initializes a new instance of the <see cref="Bcd16"/> struct.</summary>
    /// <param name="value">The signed value.</param>
    public Bcd16(short value) => Value = value;

    /// <summary>Gets the numeric value.</summary>
    public short Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();
}
