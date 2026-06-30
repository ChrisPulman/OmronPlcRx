// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

/// <summary>Represents the s ta tu sf un ct io nc od e enumeration.</summary>
internal enum StatusFunctionCode : byte
{
    /// <summary>Represents the r ea dc pu un it st at us enum value.</summary>
    ReadCPUUnitStatus = 0x01,
    /// <summary>Represents the r ea dc yc le ti me enum value.</summary>
    ReadCycleTime = 0x20,
}
