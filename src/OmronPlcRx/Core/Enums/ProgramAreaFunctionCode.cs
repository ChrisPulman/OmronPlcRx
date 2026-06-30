// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

/// <summary>Represents the p ro gr am ar ea fu nc ti on co de enumeration.</summary>
internal enum ProgramAreaFunctionCode : byte
{
    /// <summary>Represents the r ea d enum value.</summary>
    Read = 0x06,
    /// <summary>Represents the w ri te enum value.</summary>
    Write = 0x07,
    /// <summary>Represents the c le ar enum value.</summary>
    Clear = 0x08,
}
