// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

/// <summary>Represents the o pe ra ti ng mo de fu nc ti on co de enumeration.</summary>
internal enum OperatingModeFunctionCode : byte
{
    /// <summary>Represents the r un mo de enum value.</summary>
    RunMode = 0x01,
    /// <summary>Represents the s to pm od e enum value.</summary>
    StopMode = 0x02,
}
