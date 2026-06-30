// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

/// <summary>Represents the p ar am et er ar ea fu nc ti on co de enumeration.</summary>
internal enum ParameterAreaFunctionCode : byte
{
    /// <summary>Represents the r ea d enum value.</summary>
    Read = 0x01,
    /// <summary>Represents the w ri te enum value.</summary>
    Write = 0x02,
    /// <summary>Represents the f il l enum value.</summary>
    Fill = 0x03,
}
