// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Types;

/// <summary>
/// Unsigned 16-bit BCD numeric wrapper.
/// </summary>
public readonly record struct BcdU16
{
    /// <summary>Initializes a new instance of the <see cref="BcdU16"/> struct.</summary>
    /// <param name="value">The unsigned value.</param>
    public BcdU16(ushort value) => Value = value;

    /// <summary>Gets the numeric value.</summary>
    public ushort Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();
}
