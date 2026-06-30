// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

/// <summary>Represents the d eb ug gi ng fu nc ti on co de enumeration.</summary>
internal enum DebuggingFunctionCode : byte
{
    /// <summary>Represents the f or ce bi ts enum value.</summary>
    ForceBits = 0x01,
    /// <summary>Represents the c le ar fo rc ed bi ts enum value.</summary>
    ClearForcedBits = 0x02,
}
