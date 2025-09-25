// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

internal enum MachineConfigurationFunctionCode : byte
{
    ReadCPUUnitData = 0x01,
    ReadConnectionData = 0x02,
}
