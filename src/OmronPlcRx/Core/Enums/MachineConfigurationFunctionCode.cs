// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

/// <summary>Represents the m ac hi ne co nf ig ur at io nf un ct io nc od e enumeration.</summary>
internal enum MachineConfigurationFunctionCode : byte
{
    /// <summary>Represents the r ea dc pu un it da ta enum value.</summary>
    ReadCPUUnitData = 0x01,
    /// <summary>Represents the r ea dc on ne ct io nd at a enum value.</summary>
    ReadConnectionData = 0x02,
}
